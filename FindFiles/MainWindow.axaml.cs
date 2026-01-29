using Avalonia.Controls;
using FindFiles.Models;
using FindFiles.ViewModels;

namespace FindFiles;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Simple DI/Initialization
        // Ideally checking for design mode or proper DI
        DataContext = new MainWindowViewModel();
    }

    private void OnResultDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedResult != null)
        {
            var result = vm.SelectedResult;
            if (System.IO.File.Exists(result.FilePath))
            {
                var viewer = new FileViewerWindow(
                    result.FilePath, 
                    (int)result.LineNumber, 
                    vm.ContentPattern,
                    vm.UseRegex);
                
                viewer.Show();
            }
        }
    }
}