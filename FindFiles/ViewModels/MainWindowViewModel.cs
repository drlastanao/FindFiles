using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FindFiles.Models;
using FindFiles.Services;

namespace FindFiles.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFileSearchService _searchService;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _directoryPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
    [ObservableProperty] private string _namePattern = string.Empty;
    [ObservableProperty] private string _contentPattern = string.Empty;
    [ObservableProperty] private bool _useRegex = false;
    [ObservableProperty] private bool _recursive = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    private bool _isBusy;
    [ObservableProperty] private SearchResult? _selectedResult;

    [ObservableProperty] private string _statusMessage = "Listo";
    [ObservableProperty] private int _filesScanned;
    
    public bool CanSearch => !IsBusy;

    public ObservableCollection<SearchResult> Results { get; } = new();

    public MainWindowViewModel(IFileSearchService searchService)
    {
        _searchService = searchService;
    }
    
    // Default constructor for design time
    public MainWindowViewModel() : this(new FileSearchService()) { }

    [RelayCommand]
    private async Task Browse()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
            ? desktop.MainWindow 
            : null;

        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Seleccionar Directorio",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            DirectoryPath = folders[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private async Task Search()
    {
        if (IsBusy) return;

        _cts = new CancellationTokenSource();
        IsBusy = true;
        StatusMessage = "Buscando...";
        FilesScanned = 0;
        Results.Clear();

        var progress = new System.Progress<string>(file => 
        {
            FilesScanned++;
            StatusMessage = $"Escaneando: {file}";
        });

        try
        {
            await foreach (var result in _searchService.SearchAsync(
                DirectoryPath, 
                NamePattern, 
                ContentPattern, 
                UseRegex, 
                Recursive, 
                progress,
                _cts.Token))
            {
                Results.Add(result);
            }
            StatusMessage = $"Búsqueda completada. Encontrados {Results.Count} ficheros.";
        }
         catch (System.OperationCanceledException) 
         {
             StatusMessage = "Búsqueda cancelada.";
         }
        finally
        {
            IsBusy = false;
            _cts = null;
        }
    }
}
