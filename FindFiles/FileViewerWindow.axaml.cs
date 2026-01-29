using Avalonia.Controls;
using AvaloniaEdit;
using FindFiles.Utils;
using System.IO;

namespace FindFiles;

public partial class FileViewerWindow : Window
{
    public FileViewerWindow()
    {
        InitializeComponent();
    }

    public FileViewerWindow(string filePath, int lineNumber, string contentPattern, bool useRegex)
    {
        InitializeComponent();
        
        var editor = this.FindControl<TextEditor>("Editor");
        if (editor != null && File.Exists(filePath))
        {
             // Use Document property instead of Load method
             editor.Document = new AvaloniaEdit.Document.TextDocument(File.ReadAllText(filePath));
             
             if (!string.IsNullOrWhiteSpace(contentPattern))
             {
                 try 
                 {
                     var highlighter = new SearchTermHighlighter(contentPattern, useRegex);
                     editor.TextArea.TextView.LineTransformers.Add(highlighter);
                 }
                 catch { /* Ignore invalid regex */ }
             }

             if (lineNumber > 0)
             {
                 editor.ScrollTo(lineNumber, 1);
                 editor.TextArea.Caret.Line = lineNumber;
                 editor.TextArea.Caret.Column = 1;
                 // Highlight the line specifically? Or rely on content highlight?
                 // User said "content highlighted".
                 // But also "marcado en un color que se vea bien".
             }
        }
        Title = $"Visor de Ficheros - {filePath}";
    }
}
