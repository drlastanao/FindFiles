namespace FindFiles.Models;

public class SearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public long LineNumber { get; set; } = -1; // -1 if matched by filename
    public string MatchPreview { get; set; } = string.Empty;
    public bool IsSkipped { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
