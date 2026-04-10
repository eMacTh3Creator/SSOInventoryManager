namespace SSOInventoryManager.Models;

public class ConnectionSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool UseDeviceCodeFlow { get; set; } = true;
    public string AdfsServer { get; set; } = string.Empty;

    public bool HasAzureConfig => !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId);
    public bool HasAdfsConfig => !string.IsNullOrWhiteSpace(AdfsServer);
}
