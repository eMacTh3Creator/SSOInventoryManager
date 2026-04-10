using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SSOInventoryManager.Models;

namespace SSOInventoryManager.Services;

public static class ConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SSOInventoryManager");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.dat");

    public static void Save(ConnectionSettings settings)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(settings);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(ConfigPath, encrypted);
    }

    public static ConnectionSettings? Load()
    {
        if (!File.Exists(ConfigPath)) return null;

        try
        {
            var encrypted = File.ReadAllBytes(ConfigPath);
            var plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<ConnectionSettings>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Delete()
    {
        if (File.Exists(ConfigPath))
            File.Delete(ConfigPath);
    }
}
