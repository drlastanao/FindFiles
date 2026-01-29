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
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private SearchResult? _selectedResult;

    public ObservableCollection<SearchResult> Results { get; } = new();

    public MainWindowViewModel(IFileSearchService searchService)
    {
        _searchService = searchService;
    }
    
    // Default constructor for design time
    public MainWindowViewModel() : this(new FileSearchService()) { }

    [RelayCommand]
    private async Task Search()
    {
        if (IsBusy)
        {
            _cts?.Cancel();
            IsBusy = false;
            return;
        }

        _cts = new CancellationTokenSource();
        IsBusy = true;
        Results.Clear();

        try
        {
            await foreach (var result in _searchService.SearchAsync(
                DirectoryPath, 
                NamePattern, 
                ContentPattern, 
                UseRegex, 
                Recursive, 
                _cts.Token))
            {
                Results.Add(result);
            }
        }
         catch (System.OperationCanceledException) 
         {
             // Ignore
         }
        finally
        {
            IsBusy = false;
            _cts = null;
        }
    }
}
