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
    public async IAsyncEnumerable<SearchResult> SearchAsync(
        string directory,
        string namePattern,
        string contentPattern,
        bool useRegex,
        bool recursive,
        IProgress<string>? progress,
        [EnumeratorCancellation] CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            yield break;

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        IEnumerable<string> files;

        // Optimization: utilize OS file enumeration for simple wildcards if searching by name and not using regex
        if (!useRegex && !string.IsNullOrWhiteSpace(namePattern) && !namePattern.Contains(";") && !namePattern.Contains("|"))
        {
             try
             {
                 files = Directory.EnumerateFiles(directory, namePattern, searchOption);
             }
             catch (UnauthorizedAccessException) 
             { 
                 yield break; // simplistic error handling
             }
        }
        else
        {
            try
            {
                files = Directory.EnumerateFiles(directory, "*", searchOption);
            }
             catch (UnauthorizedAccessException) 
             { 
                 yield break;
             }
        }

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
            // Convert wildcards to regex for in-memory filtering if needed
            // If we didn't use OS enumeration (e.g. we used it but now double checking, or namePattern was empty but we need it? No.)
            // Actually, if we used OS enumeration for namePattern, we don't need to check name again technically, but doing so provides consistency if OS differs.
            // But let's assume OS is correct for name.
            
            // For content, we certainly need a matcher.
            if (!string.IsNullOrWhiteSpace(contentPattern))
            {
                var pattern = "^" + Regex.Escape(contentPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                // Note: User said "approximación (prueb*)". This usually means 'Starts with prueb'.
                // If content search is "prueb*", regex: "^prueb.*$" or just "prueb.*"?
                // "Grep" search usually finds substring.
                // If I search "foo", I find "foobar".
                // So Wildcard in content: "*foo*" ?
                // Let's treat "Wildcard" mode for Content as: 
                // The user string IS a wildcard pattern that must match the line? Or substring?
                // Usually "Text Content" search is "Contains" unless wildcards are used.
                // If user types "prueb*", they mean a word starting with prueb.
                // I will use regex conversion:
                // If pattern contains * or ?, assume wildcard pattern.
                // Else, assume "Contains" (substring).
                
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

        var enumerator = files.GetEnumerator();
        string file = null!;
        while (true)
        {
            try
            {
                if (!enumerator.MoveNext()) break;
                file = enumerator.Current;
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; } // Handle cases like "No such device or address" during enumeration if possible, though mostly happens on access
            catch (System.Security.SecurityException) { continue; }

            progress?.Report(file);
            token.ThrowIfCancellationRequested();

            SearchResult? errorResult = null;

            try
            {
                // Filter by Name
                if (nameRegex != null)
                {
                    if (!nameRegex.IsMatch(Path.GetFileName(file)))
                        continue;
                }
            }
            catch (Exception ex) 
            {
                errorResult = new SearchResult 
                { 
                    FilePath = file, 
                    IsSkipped = true, 
                    ErrorMessage = ex.Message 
                };
            } 

            if (errorResult != null)
            {
                yield return errorResult;
                continue;
            }

            // Filter by Content
            if (string.IsNullOrWhiteSpace(contentPattern))
            {
                yield return new SearchResult { FilePath = file };
            }
            else
            {
                // Read file
                int lineNumber = 0;
                IEnumerator<string>? lineEnumerator = null;
                
                try
                {
                     // File.ReadLines returns lazy enum, GetEnumerator opens the file
                     lineEnumerator = File.ReadLines(file).GetEnumerator();
                }
                catch (Exception ex) 
                { 
                    errorResult = new SearchResult 
                    { 
                        FilePath = file, 
                        IsSkipped = true, 
                        ErrorMessage = ex.Message 
                    };
                }

                if (errorResult != null)
                {
                    yield return errorResult;
                    continue;
                }

                if (lineEnumerator == null) continue; // Should not happen given logic above

                using (lineEnumerator) 
                {
                    while (true)
                    {
                        string line = null!;
                        bool hasMore = false;
                        SearchResult? lineError = null;

                        try
                        {
                            hasMore = lineEnumerator.MoveNext();
                            if (hasMore) line = lineEnumerator.Current;
                        }
                        catch (Exception ex) 
                        { 
                            lineError = new SearchResult 
                            { 
                                FilePath = file, 
                                IsSkipped = true, 
                                ErrorMessage = $"Error reading line: {ex.Message}" 
                            };
                        }

                        if (lineError != null)
                        {
                            yield return lineError;
                            break; // Stop reading this file
                        }

                        if (!hasMore) break;

                        lineNumber++;
                        
                        // Check cancellation occasionally but safe here
                        if (token.IsCancellationRequested)
                        {
                            token.ThrowIfCancellationRequested(); 
                        }
                        
                        bool isMatch = false;
                        try
                        {
                            if (contentRegex != null && contentRegex.IsMatch(line))
                                isMatch = true;
                        }
                        catch (Exception) { } 

                        if (isMatch)
                        {
                            yield return new SearchResult 
                            { 
                                FilePath = file, 
                                LineNumber = lineNumber, 
                                MatchPreview = line.Trim() 
                            };
                        }
                    }
                }
            }
        }
    }
}
