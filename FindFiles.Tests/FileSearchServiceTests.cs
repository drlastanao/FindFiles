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
        var results = await _service.SearchAsync(_testDir, "*.txt", null, useRegex: false, recursive: false, progress: null, CancellationToken.None).ToListAsync();
        Assert.Single(results);
        Assert.EndsWith("test1.txt", results[0].FilePath);
    }

    [Fact]
    public async Task SearchByName_Regex_FindsFiles()
    {
        var results = await _service.SearchAsync(_testDir, "^test.*", null, useRegex: true, recursive: false, progress: null, CancellationToken.None).ToListAsync();
        Assert.Equal(2, results.Count); // test1.txt, test2.log
    }

    [Fact]
    public async Task SearchByContent_Partial_FindsFiles()
    {
        var results = await _service.SearchAsync(_testDir, "", "World", useRegex: false, recursive: false, progress: null, CancellationToken.None).ToListAsync();
        Assert.Single(results);
        Assert.Contains("Hello World", results[0].MatchPreview);
    }

    [Fact]
    public async Task SearchRecursive_FindsDeepFiles()
    {
        var results = await _service.SearchAsync(_testDir, "*.cs", null, useRegex: false, recursive: true, progress: null, CancellationToken.None).ToListAsync();
        Assert.Single(results);
        Assert.EndsWith("deep.cs", results[0].FilePath);
    }
    
    [Fact]
    public async Task SearchByContent_Regex_FindsFiles()
    {
        var results = await _service.SearchAsync(_testDir, "", "TODO:.*", useRegex: true, recursive: false, progress: null, CancellationToken.None).ToListAsync();
        Assert.Single(results);
        Assert.EndsWith("notes.md", results[0].FilePath);
    }
}
