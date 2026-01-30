using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FindFiles.Services;
using Xunit;

namespace FindFiles.Tests;

public class FileSearchServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly IFileSearchService _service;

    public FileSearchServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FindFilesTest_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
        _service = new FileSearchService();
        SetupTestData();
    }

    private void SetupTestData()
    {
        File.WriteAllText(Path.Combine(_testDir, "test1.txt"), "Hello World");
        File.WriteAllText(Path.Combine(_testDir, "test2.log"), "Error: Something went wrong");
        File.WriteAllText(Path.Combine(_testDir, "notes.md"), "TODO: Fix bugs");
        
        var subDir = Path.Combine(_testDir, "SubDir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "deep.cs"), "public class Deep { }");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task SearchByName_Wildcard_FindsFiles()
    {

        var results = await _service.SearchAsync(_testDir, "*.txt", null, useRegex: false, recursive: true, excludeBinaryFiles: false, excludeHidden: true, progress: null, CancellationToken.None).ToListAsync();
        Assert.Single(results);
        Assert.EndsWith("test1.txt", results[0].FilePath);
    }

    [Fact]
    public async Task SearchByName_Regex_FindsFiles()
    {

        var results = await _service.SearchAsync(_testDir, "^test.*", null, useRegex: true, recursive: false, excludeBinaryFiles: false, excludeHidden: true, progress: null, CancellationToken.None).ToListAsync();
        Assert.Equal(2, results.Count); // test1.txt, test2.log
    }

    [Fact]
    public async Task SearchByContent_Partial_FindsFiles()
    {
        var results = await _service.SearchAsync(_testDir, "", "World", useRegex: false, recursive: false, excludeBinaryFiles: false, excludeHidden: true, progress: null, CancellationToken.None).ToListAsync();
        Assert.Single(results);
        Assert.Contains("Hello World", results[0].MatchPreview);
    }

    [Fact]
    public async Task SearchRecursive_FindsDeepFiles()
    {
        var results = await _service.SearchAsync(_testDir, "*.cs", null, useRegex: false, recursive: true, excludeBinaryFiles: false, excludeHidden: true, progress: null, CancellationToken.None).ToListAsync();
        Assert.Single(results);
        Assert.EndsWith("deep.cs", results[0].FilePath);
    }
    
    [Fact]
    public async Task SearchByContent_Regex_FindsFiles()
    {
        var results = await _service.SearchAsync(_testDir, "", "TODO:.*", useRegex: true, recursive: false, excludeBinaryFiles: false, excludeHidden: true, progress: null, CancellationToken.None).ToListAsync();
        Assert.Single(results);
        Assert.EndsWith("notes.md", results[0].FilePath);
    }
    [Fact]
    public async Task Search_UserScenario_CrashReproduction()
    {
        // Setup
        var file = Path.Combine(_testDir, "page1.txt");
        File.WriteAllText(file, "Line with 315 number");
        
        var results = await _service.SearchAsync(_testDir, "page*", "315", useRegex: false, recursive: true, excludeBinaryFiles: false, excludeHidden: true, progress: null, CancellationToken.None).ToListAsync();
        
        Assert.Single(results);
    }

    [Fact]
    public async Task Search_LockedFile_HandlesGracefully()
    {
        var lockedFile = Path.Combine(_testDir, "locked.txt");
        File.WriteAllText(lockedFile, "This content is secretive");

        // Open with FileShare.None to prevent other processes (the search service) from reading
        using (var fs = File.Open(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
             // Search while locked. We expect it to try reading content because we provide a content pattern.
             var results = await _service.SearchAsync(_testDir, "locked.txt", "secretive", useRegex: false, recursive: false, excludeBinaryFiles: false, excludeHidden: true, progress: null, CancellationToken.None).ToListAsync();
             
             Assert.Single(results);
             var result = results[0];
             Assert.True(result.IsSkipped, "Expected result to be marked as skipped");
             Assert.NotNull(result.ErrorMessage);
             Assert.EndsWith("locked.txt", result.FilePath);
        }
    }
    [Fact]
    public async Task Search_InaccessibleDirectory_HandlesGracefully()
    {
        // On Linux/Unix we can simulate this with chmod.
        // On Windows it's harder without ACLs. 
        // Assuming Linux based on user OS info.
        
        var secretDir = Path.Combine(_testDir, "SecretDir");
        Directory.CreateDirectory(secretDir);
        var secretFile = Path.Combine(secretDir, "secret.txt");
        File.WriteAllText(secretFile, "content");
        
        try 
        {
            // Remove permissions (chmod 000)
            File.SetUnixFileMode(secretDir, UnixFileMode.None);
        }
        catch (PlatformNotSupportedException)
        {
            // Skip test on Windows or non-Unix if cannot set mode easily
            return; 
        }

        try
        {
            var results = await _service.SearchAsync(_testDir, "secret.txt", null, useRegex: false, recursive: true, excludeBinaryFiles: false, excludeHidden: true, progress: null, CancellationToken.None).ToListAsync();
            
            // Should contain a result for the directory being skipped/error
            // Because we yield an error result for the directory failure.
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.IsSkipped && r.FilePath.Contains("SecretDir"));
        }
        finally
        {
            // Restore permissions so cleanup can delete it
            File.SetUnixFileMode(secretDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
    [Fact]
    public async Task Search_ExcludeBinaryFiles_FiltersCorrectly()
    {
        var exeFile = Path.Combine(_testDir, "app.exe");
        File.WriteAllText(exeFile, "binary content");

        // Search with excludeBinaryFiles: false
        var resultsWithBinary = await _service.SearchAsync(_testDir, "*", null, useRegex: false, recursive: false, excludeBinaryFiles: false, excludeHidden: false, progress: null, CancellationToken.None).ToListAsync();
        Assert.Contains(resultsWithBinary, r => r.FilePath.EndsWith("app.exe"));

        // Search with excludeBinaryFiles: true
        var resultsWithoutBinary = await _service.SearchAsync(_testDir, "*", null, useRegex: false, recursive: false, excludeBinaryFiles: true, excludeHidden: false, progress: null, CancellationToken.None).ToListAsync();
        Assert.DoesNotContain(resultsWithoutBinary, r => r.FilePath.EndsWith("app.exe"));
    }

    [Fact]
    public async Task Search_HiddenDirectory_SkipsIfRequested()
    {
        var hiddenDir = Path.Combine(_testDir, ".HiddenDir");
        Directory.CreateDirectory(hiddenDir);
        // On Windows we need to set the attribute explicitly. On Linux the dot prefix is enough but SetAttributes is harmless or ignored.
        File.SetAttributes(hiddenDir, FileAttributes.Hidden); 
        var hiddenFile = Path.Combine(hiddenDir, "hidden.txt");
        File.WriteAllText(hiddenFile, "You can't see me");

         // Search with excludeHidden: true
        var resultsExcluded = await _service.SearchAsync(_testDir, "*", null, useRegex: false, recursive: true, excludeBinaryFiles: false, excludeHidden: true, progress: null, CancellationToken.None).ToListAsync();
        Assert.DoesNotContain(resultsExcluded, r => r.FilePath.Contains(".HiddenDir"));

         // Search with excludeHidden: false
        var resultsIncluded = await _service.SearchAsync(_testDir, "*", null, useRegex: false, recursive: true, excludeBinaryFiles: false, excludeHidden: false, progress: null, CancellationToken.None).ToListAsync();
        Assert.Contains(resultsIncluded, r => r.FilePath.EndsWith("hidden.txt"));
    }
}

