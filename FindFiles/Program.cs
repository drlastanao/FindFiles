using Avalonia;
using System;
using System.Threading.Tasks;

namespace FindFiles;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            if (error.ExceptionObject is Exception ex)
            {
                ShowError(ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, error) =>
        {
            ShowError(error.Exception);
            error.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static void ShowError(Exception ex)
    {
        Console.WriteLine($"FATAL: {ex}");
        
        try
        {
            // Try to use existing dispatcher if available
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                 Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                 {
                     var errWin = new ErrorWindow(ex.ToString());
                     errWin.Show();
                 });
            }
            else
            {
                // If we haven't started yet or are crashing during startup, we might need a synchronous blocking show if possible,
                // or just accept we logged it.
                // Creating a new app/window usage here is risky.
            }
        }
        catch 
        {
            // Last resort logging
            Console.Error.WriteLine(ex);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
