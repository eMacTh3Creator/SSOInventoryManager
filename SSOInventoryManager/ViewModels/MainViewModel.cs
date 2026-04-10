using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using SSOInventoryManager.Helpers;
using SSOInventoryManager.Models;
using SSOInventoryManager.Services;

namespace SSOInventoryManager.ViewModels;

public class MainViewModel : BaseViewModel, IDisposable
{
    private readonly DatabaseService _db = new();
    private GraphService? _graphService;
    private AdfsService? _adfsService;
    private ConnectionSettings? _settings;

    // --- Azure Apps ---
    private ObservableCollection<AzureApp> _azureApps = new();
    public ObservableCollection<AzureApp> AzureApps
    {
        get => _azureApps;
        set
        {
            SetProperty(ref _azureApps, value);
            AzureAppsView = CollectionViewSource.GetDefaultView(value);
            AzureAppsView.Filter = AzureFilter;
            OnPropertyChanged(nameof(AzureAppsView));
        }
    }

    public ICollectionView? AzureAppsView { get; private set; }

    // --- ADFS Apps ---
    private ObservableCollection<AdfsApp> _adfsApps = new();
    public ObservableCollection<AdfsApp> AdfsApps
    {
        get => _adfsApps;
        set
        {
            SetProperty(ref _adfsApps, value);
            AdfsAppsView = CollectionViewSource.GetDefaultView(value);
            AdfsAppsView.Filter = AdfsFilter;
            OnPropertyChanged(nameof(AdfsAppsView));
        }
    }

    public ICollectionView? AdfsAppsView { get; private set; }

    // --- UI State ---
    private bool _isLoggedIn;
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set => SetProperty(ref _isLoggedIn, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    private string _statusText = "Not connected";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _lastSyncTime = string.Empty;
    public string LastSyncTime
    {
        get => _lastSyncTime;
        set => SetProperty(ref _lastSyncTime, value);
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
            AzureAppsView?.Refresh();
            AdfsAppsView?.Refresh();
        }
    }

    private string _warningMessage = string.Empty;
    public string WarningMessage
    {
        get => _warningMessage;
        set => SetProperty(ref _warningMessage, value);
    }

    private bool _showWarning;
    public bool ShowWarning
    {
        get => _showWarning;
        set => SetProperty(ref _showWarning, value);
    }

    // --- Detail Pane ---
    private object? _selectedItem;
    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            SetProperty(ref _selectedItem, value);
            OnPropertyChanged(nameof(IsDetailOpen));
            OnPropertyChanged(nameof(SelectedAzureApp));
            OnPropertyChanged(nameof(SelectedAdfsApp));
            OnPropertyChanged(nameof(IsAzureDetailSelected));
            OnPropertyChanged(nameof(IsAdfsDetailSelected));
        }
    }

    public bool IsDetailOpen => _selectedItem != null;
    public AzureApp? SelectedAzureApp => _selectedItem as AzureApp;
    public AdfsApp? SelectedAdfsApp => _selectedItem as AdfsApp;
    public bool IsAzureDetailSelected => _selectedItem is AzureApp;
    public bool IsAdfsDetailSelected => _selectedItem is AdfsApp;

    private string _deviceCodeMessage = string.Empty;
    public string DeviceCodeMessage
    {
        get => _deviceCodeMessage;
        set => SetProperty(ref _deviceCodeMessage, value);
    }

    private bool _showDeviceCode;
    public bool ShowDeviceCode
    {
        get => _showDeviceCode;
        set => SetProperty(ref _showDeviceCode, value);
    }

    // --- Commands ---
    public ICommand RefreshCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand CloseDetailCommand { get; }
    public ICommand DismissWarningCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand CopyToClipboardCommand { get; }

    public LoginViewModel LoginVm { get; } = new();

    public MainViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(RefreshDataAsync);
        ExportCsvCommand = new RelayCommand(ExportCsv);
        ExportExcelCommand = new RelayCommand(ExportExcel);
        CloseDetailCommand = new RelayCommand(() => SelectedItem = null);
        DismissWarningCommand = new RelayCommand(() => ShowWarning = false);
        DisconnectCommand = new RelayCommand(Disconnect);
        CopyToClipboardCommand = new RelayCommand(p => CopyToClipboard(p));

        LoginVm.ConnectRequested += OnConnectRequested;
    }

    private async void OnConnectRequested(ConnectionSettings settings)
    {
        _settings = settings;
        LoginVm.IsConnecting = true;
        LoginVm.StatusMessage = "Authenticating with Azure AD...";

        _graphService = new GraphService(settings);

        Action<string>? deviceCodeCallback = null;
        if (settings.UseDeviceCodeFlow)
        {
            deviceCodeCallback = msg =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    DeviceCodeMessage = msg;
                    ShowDeviceCode = true;
                    LoginVm.DeviceCodeMessage = msg;
                    LoginVm.StatusMessage = "Waiting for device code authentication...";
                });
            };
        }

        var error = await _graphService.AuthenticateAsync(deviceCodeCallback);
        ShowDeviceCode = false;

        if (error != null)
        {
            LoginVm.StatusMessage = $"Authentication failed: {error}";
            LoginVm.IsConnecting = false;
            return;
        }

        if (settings.HasAdfsConfig)
            _adfsService = new AdfsService(settings.AdfsServer);

        IsLoggedIn = true;
        LoginVm.IsConnecting = false;
        StatusText = $"Connected to tenant {settings.TenantId}";

        await RefreshDataAsync();
    }

    private async Task RefreshDataAsync()
    {
        if (_graphService == null) return;

        IsLoading = true;
        StatusText = "Loading Azure / Entra applications...";

        try
        {
            var azureApps = await _graphService.GetApplicationsAsync(_db);
            AzureApps = new ObservableCollection<AzureApp>(azureApps);

            // Subscribe to changes for auto-save
            foreach (var app in AzureApps)
                app.PropertyChanged += OnAzureAppPropertyChanged;

            if (!_graphService.HasAuditLogPermission)
            {
                WarningMessage = "Missing AuditLog.Read.All permission — Last Sign-In data is unavailable.";
                ShowWarning = true;
            }
        }
        catch (Exception ex)
        {
            ShowError("Azure Query Error", ex.Message);
        }

        if (_adfsService != null)
        {
            StatusText = "Loading ADFS relying party trusts...";
            try
            {
                var adfsApps = await _adfsService.GetRelyingPartyTrustsAsync(_db);
                AdfsApps = new ObservableCollection<AdfsApp>(adfsApps);

                foreach (var app in AdfsApps)
                    app.PropertyChanged += OnAdfsAppPropertyChanged;
            }
            catch (Exception ex)
            {
                WarningMessage = $"ADFS query failed: {ex.Message}";
                ShowWarning = true;
            }
        }

        LastSyncTime = $"Last sync: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        StatusText = $"Connected — {AzureApps.Count} Azure apps, {AdfsApps.Count} ADFS apps";
        IsLoading = false;
    }

    private void OnAzureAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is AzureApp app && (e.PropertyName == nameof(AzureApp.Notes) || e.PropertyName == nameof(AzureApp.FlaggedForReview)))
        {
            _db.SaveAppData(app.AppId, "Azure", app.Notes, app.FlaggedForReview);
        }
    }

    private void OnAdfsAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is AdfsApp app && (e.PropertyName == nameof(AdfsApp.Notes) || e.PropertyName == nameof(AdfsApp.FlaggedForReview)))
        {
            var key = !string.IsNullOrEmpty(app.Identifier) ? app.Identifier : app.DisplayName;
            _db.SaveAppData(key, "ADFS", app.Notes, app.FlaggedForReview);
        }
    }

    private bool AzureFilter(object obj) => obj is AzureApp app && app.MatchesFilter(SearchText);
    private bool AdfsFilter(object obj) => obj is AdfsApp app && app.MatchesFilter(SearchText);

    private void ExportCsv()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = SelectedTabIndex == 0 ? "AzureApps.csv" : "AdfsApps.csv"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            if (SelectedTabIndex == 0)
                ExportService.ExportAzureToCsv(GetFilteredAzureApps(), dlg.FileName);
            else
                ExportService.ExportAdfsToCsv(GetFilteredAdfsApps(), dlg.FileName);

            StatusText = $"Exported to {dlg.FileName}";
        }
        catch (Exception ex)
        {
            ShowError("Export Error", ex.Message);
        }
    }

    private void ExportExcel()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx",
            FileName = "SSOInventory.xlsx"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            ExportService.ExportToExcel(AzureApps, AdfsApps, dlg.FileName);
            StatusText = $"Exported to {dlg.FileName}";
        }
        catch (Exception ex)
        {
            ShowError("Export Error", ex.Message);
        }
    }

    private IEnumerable<AzureApp> GetFilteredAzureApps()
    {
        return AzureApps.Where(a => a.MatchesFilter(SearchText));
    }

    private IEnumerable<AdfsApp> GetFilteredAdfsApps()
    {
        return AdfsApps.Where(a => a.MatchesFilter(SearchText));
    }

    private void Disconnect()
    {
        IsLoggedIn = false;
        _graphService = null;
        _adfsService = null;
        AzureApps.Clear();
        AdfsApps.Clear();
        SelectedItem = null;
        StatusText = "Disconnected";
        LastSyncTime = string.Empty;
    }

    private void CopyToClipboard(object? parameter)
    {
        if (parameter is string text)
            Clipboard.SetText(text);
    }

    private void ShowError(string title, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var result = MessageBox.Show(
                $"{message}\n\nCopy to clipboard?",
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
                Clipboard.SetText(message);
        });
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
