using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FindFiles.Models;

namespace FindFiles.Services;

public class FileSearchService : IFileSearchService
{
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".tiff",
        ".bin", ".exe", ".dll", ".so", ".dylib", ".o", ".obj",
        ".zip", ".tar", ".gz", ".rar", ".7z",
        ".pyc", ".class"
    };

    public async IAsyncEnumerable<SearchResult> SearchAsync(
        string directory,
        string namePattern,
        string contentPattern,
        bool useRegex,
        bool recursive,
        bool excludeBinaryFiles,
        bool excludeHidden,
        IProgress<string>? progress,
        [EnumeratorCancellation] CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            yield break;

        var stack = new Stack<string>();
        stack.Push(directory);

        Regex? nameRegex = null;
        Regex? contentRegex = null;

        if (useRegex)
        {
            if (!string.IsNullOrWhiteSpace(namePattern))
                nameRegex = new Regex(namePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            if (!string.IsNullOrWhiteSpace(contentPattern))
                contentRegex = new Regex(contentPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
        else
        {
             // Prepare content regex for wildcard or exact match
             if (!string.IsNullOrWhiteSpace(contentPattern))
             {
                string regexPattern;
                if (contentPattern.Contains('*') || contentPattern.Contains('?'))
                {
                     regexPattern = Regex.Escape(contentPattern).Replace("\\*", ".*").Replace("\\?", ".");
                }
                else
                {
                    regexPattern = Regex.Escape(contentPattern);
                }
                contentRegex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
             }
        }



        var lastProgressTime = DateTime.MinValue;
        int filesSinceLastReport = 0;

        while (stack.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            string currentDir = stack.Pop();

            IEnumerable<string> files = Enumerable.Empty<string>();
            
            SearchResult? dirError = null;
            try
            {
                // If not using regex and simple pattern, we could use the OS filter, but valid for TopDirectoryOnly
                // However, to keep it simple and consistent with manual recursion, let's just use "*" and filter in memory 
                // OR use the pattern if it's safe. 
                // Let's use "*" to ensure we process errors our way, or simple pattern if possible to reduce memory/overhead.
                // But OS pattern matching is faster.
                string searchPattern = (!useRegex && !string.IsNullOrWhiteSpace(namePattern) && !namePattern.Contains(";") && !namePattern.Contains("|")) 
                                        ? namePattern 
                                        : "*";

                files = Directory.EnumerateFiles(currentDir, searchPattern, SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                dirError = new SearchResult 
                { 
                     FilePath = currentDir, 
                     IsSkipped = true, 
                     ErrorMessage = $"Access Denied/Error: {ex.Message}" 
                };
            }

            if (dirError != null)
            {
                yield return dirError;
                continue;
            }

            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                
                // Throttling progress
                var now = DateTime.UtcNow;
                filesSinceLastReport++;
                if ((now - lastProgressTime).TotalMilliseconds > 250 || filesSinceLastReport > 100)
                {
                    progress?.Report(file);
                    lastProgressTime = now;
                    filesSinceLastReport = 0;
                }

                SearchResult? errorResult = null;
                bool nameMatch = true;

                // Match Name if we didn't use OS pattern
                if (nameRegex != null)
                {
                    if (!nameRegex.IsMatch(Path.GetFileName(file)))
                        nameMatch = false;
                }
                else if (!(!useRegex && !string.IsNullOrWhiteSpace(namePattern) && !namePattern.Contains(";") && !namePattern.Contains("|")))
                {
                   // covered
                }

                if (!nameMatch) continue;

                if (excludeBinaryFiles && BinaryExtensions.Contains(Path.GetExtension(file)))
                {
                    continue;
                }


                // Match Content
                 if (string.IsNullOrWhiteSpace(contentPattern))
                {
                    yield return new SearchResult { FilePath = file };
                }
                else
                {
                    var result = await ProcessFileContentAsync(file, contentRegex, token);
                    if (result != SearchResult.Empty)
                        yield return result;
                }
            }

            if (recursive)
            {
                SearchResult? recError = null;
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(currentDir))
                    {
                        if (excludeHidden)
                        {
                            var dirInfo = new DirectoryInfo(dir);
                            if ((dirInfo.Attributes & FileAttributes.Hidden) != 0)
                            {
                                continue;
                            }
                        }
                        stack.Push(dir);
                    }
                }
                catch (Exception ex)
                {
                     recError = new SearchResult 
                    { 
                         FilePath = currentDir, 
                         IsSkipped = true, 
                         ErrorMessage = $"Directory Access Error: {ex.Message}" 
                    };
                }
                
                if (recError != null)
                {
                    yield return recError;
                }
            }
        }
    }

    private async Task<SearchResult> ProcessFileContentAsync(string file, Regex? contentRegex, CancellationToken token)
    {
         int lineNumber = 0;
         // Use FileStream with shared read to avoid locking issues where possible, though File.ReadLines is easier but less control.
         // Let's use FileStream for max robustness.
         
         try
         {
             using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
             using (var reader = new StreamReader(fs))
             {
                 string? line;
                 while ((line = await reader.ReadLineAsync(token)) != null)
                 {
                     lineNumber++;
                     if (contentRegex != null && contentRegex.IsMatch(line))
                     {
                         return new SearchResult 
                         { 
                             FilePath = file, 
                             LineNumber = lineNumber, 
                             MatchPreview = line.Length > 500 ? line.Substring(0, 500) + "..." : line.Trim() 
                         };
                     }
                 }
             }
         }
         catch (Exception ex)
         {
             return new SearchResult 
             { 
                 FilePath = file, 
                 IsSkipped = true, 
                 ErrorMessage = ex.Message 
             };
         }

         // No match found in file, but read successfully. Use a marker? 
         // The caller expects NO result if no match.
         // But we must return SOMETHING or null? 
         // AsyncEnumerable yields. 
         // Refactor: This method returns Task<SearchResult>. 
         // If no match, we return an "Empty" result?
         // Better: Let's inline this logic or return nullable.
         
         return SearchResult.Empty; // Need to handle this
    }
}
