using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FindFiles.Models;

namespace FindFiles.Services;

public interface IFileSearchService
{
    IAsyncEnumerable<SearchResult> SearchAsync(
        string directory, 
        string namePattern, 
        string contentPattern, 
        bool useRegex, 
        bool recursive,
        bool excludeBinaryFiles,
        bool excludeHidden,
        IProgress<string>? progress,
        CancellationToken token);
}
