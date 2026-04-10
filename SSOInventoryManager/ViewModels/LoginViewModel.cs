using System.Windows.Input;
using SSOInventoryManager.Helpers;
using SSOInventoryManager.Models;
using SSOInventoryManager.Services;

namespace SSOInventoryManager.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private string _tenantId = string.Empty;
    public string TenantId
    {
        get => _tenantId;
        set => SetProperty(ref _tenantId, value);
    }

    private string _clientId = string.Empty;
    public string ClientId
    {
        get => _clientId;
        set => SetProperty(ref _clientId, value);
    }

    private string _clientSecret = string.Empty;
    public string ClientSecret
    {
        get => _clientSecret;
        set => SetProperty(ref _clientSecret, value);
    }

    private bool _useDeviceCodeFlow = true;
    public bool UseDeviceCodeFlow
    {
        get => _useDeviceCodeFlow;
        set
        {
            SetProperty(ref _useDeviceCodeFlow, value);
            OnPropertyChanged(nameof(UseServicePrincipal));
        }
    }

    public bool UseServicePrincipal
    {
        get => !_useDeviceCodeFlow;
        set => UseDeviceCodeFlow = !value;
    }

    private string _adfsServer = string.Empty;
    public string AdfsServer
    {
        get => _adfsServer;
        set => SetProperty(ref _adfsServer, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isConnecting;
    public bool IsConnecting
    {
        get => _isConnecting;
        set => SetProperty(ref _isConnecting, value);
    }

    private string _deviceCodeMessage = string.Empty;
    public string DeviceCodeMessage
    {
        get => _deviceCodeMessage;
        set => SetProperty(ref _deviceCodeMessage, value);
    }

    public event Action<ConnectionSettings>? ConnectRequested;

    public ICommand ConnectCommand { get; }
    public ICommand ClearConfigCommand { get; }

    public LoginViewModel()
    {
        ConnectCommand = new RelayCommand(Connect);
        ClearConfigCommand = new RelayCommand(ClearConfig);
        LoadSavedSettings();
    }

    private void LoadSavedSettings()
    {
        var settings = ConfigService.Load();
        if (settings == null) return;

        TenantId = settings.TenantId;
        ClientId = settings.ClientId;
        ClientSecret = settings.ClientSecret;
        UseDeviceCodeFlow = settings.UseDeviceCodeFlow;
        AdfsServer = settings.AdfsServer;
    }

    private void Connect()
    {
        if (string.IsNullOrWhiteSpace(TenantId) || string.IsNullOrWhiteSpace(ClientId))
        {
            StatusMessage = "Tenant ID and Client ID are required.";
            return;
        }

        if (!UseDeviceCodeFlow && string.IsNullOrWhiteSpace(ClientSecret))
        {
            StatusMessage = "Client Secret is required for Service Principal auth.";
            return;
        }

        var settings = new ConnectionSettings
        {
            TenantId = TenantId.Trim(),
            ClientId = ClientId.Trim(),
            ClientSecret = ClientSecret.Trim(),
            UseDeviceCodeFlow = UseDeviceCodeFlow,
            AdfsServer = AdfsServer.Trim()
        };

        ConfigService.Save(settings);
        ConnectRequested?.Invoke(settings);
    }

    private void ClearConfig()
    {
        ConfigService.Delete();
        TenantId = string.Empty;
        ClientId = string.Empty;
        ClientSecret = string.Empty;
        AdfsServer = string.Empty;
        UseDeviceCodeFlow = true;
        StatusMessage = "Saved configuration cleared.";
    }
}
