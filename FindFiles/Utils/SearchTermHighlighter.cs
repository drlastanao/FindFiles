using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using System.Text.RegularExpressions;

namespace FindFiles.Utils;

public class SearchTermHighlighter : DocumentColorizingTransformer
{
    private readonly Regex _regex;

    public SearchTermHighlighter(string pattern, bool useRegex)
    {
        RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase;
        if (useRegex)
        {
            _regex = new Regex(pattern, options);
        }
        else
        {
            // Convert * and ? wildcards to regex for highlighting or just simple literal if no wildcards?
            // "Approximate search" (wildcards) logic from service:
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
               string regexPattern = Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".");
               _regex = new Regex(regexPattern, options);
            }
            else
            {
                _regex = new Regex(Regex.Escape(pattern), options);
            }
        }
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        string text = CurrentContext.Document.GetText(line);
        var matches = _regex.Matches(text);

        foreach (Match match in matches)
        {
            ChangeLinePart(
                line.Offset + match.Index,
                line.Offset + match.Index + match.Length,
                element =>
                {
                    element.TextRunProperties.SetBackgroundBrush(Brushes.Yellow);
                    element.TextRunProperties.SetForegroundBrush(Brushes.Black);
                });
        }
    }
}
