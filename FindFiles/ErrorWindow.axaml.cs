using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FindFiles;

public partial class ErrorWindow : Window
{
    public ErrorWindow()
    {
        InitializeComponent();
    }

    public ErrorWindow(string message)
    {
        InitializeComponent();
        this.FindControl<TextBox>("ErrorText").Text = message;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
