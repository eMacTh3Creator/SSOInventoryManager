# SSO Inventory Manager

A Windows desktop application for enterprise IT administrators to inventory and manage SSO applications across **Azure AD / Entra ID** and **ADFS** from a single pane of glass.

Built with WPF (.NET 8), Material Design, and the MVVM pattern.

---

## Features

- **Azure / Entra ID** — Queries Microsoft Graph for all service principals, app registrations, SSO modes, sign-in activity, and role assignments
- **ADFS** — Queries ADFS relying party trusts via PowerShell remoting (`Get-AdfsRelyingPartyTrust`)
- **Sortable & filterable DataGrids** with live search across all columns
- **Detail pane** — Click any row to view all fields expanded and copyable
- **Flag for Review** — Toggle per-row highlighting (persisted locally)
- **Notes** — Free-text notes per application (persisted in local SQLite)
- **Export to CSV** — Exports the currently filtered tab
- **Export to Excel** — Both tabs exported to a single `.xlsx` workbook with formatted headers and auto-column widths
- **Dark themed UI** — Dense, professional Material Design interface
- **Encrypted config** — Connection settings stored locally using Windows DPAPI

---

## Prerequisites

### Azure AD / Entra ID

You need an **App Registration** in your Azure tenant with the following **Application permissions** (not Delegated) granted admin consent:

| Permission | Purpose |
|---|---|
| `Application.Read.All` | Read service principals and app registrations |
| `Directory.Read.All` | Read directory data, role assignments |
| `AuditLog.Read.All` | Read sign-in activity (last sign-in timestamps) |

> **Note:** If `AuditLog.Read.All` is not granted, the app will still work — the "Last Sign-In" column will display "N/A" and a warning banner will appear.

### ADFS

- **PowerShell remoting** must be enabled on the ADFS server (`Enable-PSRemoting`)
- The machine running SSO Inventory Manager must have network access to the ADFS server on port **5985** (WinRM HTTP)
- The current Windows user (or prompted credential) must have permission to run `Get-AdfsRelyingPartyTrust` on the ADFS server
- ADFS is optional — if not configured, only the Azure tab will populate

---

## How to Register an Azure App Registration

1. Go to [Azure Portal](https://portal.azure.com) > **Azure Active Directory** > **App registrations** > **New registration**
2. Name: `SSO Inventory Manager` (or any name)
3. Supported account types: **Accounts in this organizational directory only**
4. Redirect URI: Leave blank (not needed for device code or client credentials)
5. Click **Register**

### For Device Code Flow

6. Go to **Authentication** > Under **Advanced settings**, set **Allow public client flows** to **Yes**
7. Note the **Application (client) ID** and **Directory (tenant) ID**

### For Service Principal (Client Secret)

6. Go to **Certificates & secrets** > **New client secret**
7. Copy the secret **Value** immediately (it won't be shown again)
8. Note the **Application (client) ID**, **Directory (tenant) ID**, and **Client Secret**

### Grant API Permissions

8. Go to **API permissions** > **Add a permission** > **Microsoft Graph** > **Application permissions**
9. Add: `Application.Read.All`, `Directory.Read.All`, `AuditLog.Read.All`
10. Click **Grant admin consent for [your tenant]**

---

## Running the Application

### Device Code Flow

1. Launch the application
2. Enter your **Tenant ID** and **Client ID**
3. Select **Device Code Flow**
4. (Optional) Enter your ADFS server hostname
5. Click **Connect**
6. Follow the device code prompt — open the URL in a browser and enter the code
7. Data will load automatically after authentication

### Service Principal

1. Launch the application
2. Enter your **Tenant ID** and **Client ID**
3. Select **Client Secret (Service Principal)**
4. Enter the **Client Secret**
5. (Optional) Enter your ADFS server hostname
6. Click **Connect**

> Connection settings are encrypted and stored locally using DPAPI. Click **Clear** to remove saved credentials.

---

## Building from Source

### Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows)
- Visual Studio 2022+ or the `dotnet` CLI

### Build

```bash
cd SSOInventoryManager
dotnet build
```

### Run

```bash
dotnet run --project SSOInventoryManager
```

---

## Publishing as a Self-Contained `.exe`

To deploy to a server or workstation without the .NET runtime installed:

```bash
dotnet publish SSOInventoryManager/SSOInventoryManager.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish
```

This produces a single `SSOInventoryManager.exe` in the `./publish` folder that includes the .NET runtime.

### Publish options

| Flag | Purpose |
|---|---|
| `-r win-x64` | Target 64-bit Windows |
| `--self-contained true` | Bundle the .NET runtime |
| `-p:PublishSingleFile=true` | Single `.exe` output |
| `-p:PublishTrimmed=true` | (Optional) Reduce size by trimming unused code |

---

## Data Storage

| Data | Location |
|---|---|
| Encrypted connection settings | `%LOCALAPPDATA%\SSOInventoryManager\config.dat` |
| SQLite database (notes, flags) | `%LOCALAPPDATA%\SSOInventoryManager\appdata.db` |

Notes and review flags are keyed by App ID (Azure) or Identifier (ADFS) and persist across data refreshes.

---

## Architecture

```
SSOInventoryManager/
├── Models/
│   ├── ObservableBase.cs       # INotifyPropertyChanged base
│   ├── AzureApp.cs             # Azure/Entra application model
│   ├── AdfsApp.cs              # ADFS relying party trust model
│   └── ConnectionSettings.cs   # Auth configuration model
├── ViewModels/
│   ├── BaseViewModel.cs        # ViewModel base class
│   ├── LoginViewModel.cs       # Login/connection form logic
│   └── MainViewModel.cs        # Main application logic
├── Views/
│   ├── LoginView.xaml          # Login screen
│   ├── DetailPane.xaml         # Slide-in detail panel
│   └── (inline in MainWindow)  # DataGrid tabs
├── Services/
│   ├── ConfigService.cs        # DPAPI-encrypted config storage
│   ├── DatabaseService.cs      # SQLite for notes & flags
│   ├── GraphService.cs         # Microsoft Graph API queries
│   ├── AdfsService.cs          # ADFS PowerShell remoting
│   └── ExportService.cs        # CSV and Excel export
├── Converters/
│   └── BooleanConverters.cs    # WPF value converters
├── Helpers/
│   └── RelayCommand.cs         # ICommand implementations
├── App.xaml                    # Material Design dark theme
├── App.xaml.cs                 # Global error handling
└── MainWindow.xaml             # Primary application window
```

---

## Troubleshooting

### "ADFS query failed" warning
- Verify PowerShell remoting is enabled: `Enter-PSSession -ComputerName adfs.contoso.com`
- Ensure WinRM port 5985 is open
- Ensure the running user has ADFS admin rights

### "Last Sign-In" shows N/A
- Grant `AuditLog.Read.All` application permission and admin consent
- Sign-in activity requires an Azure AD P1/P2 license

### Authentication fails with Device Code
- Ensure **Allow public client flows** is set to **Yes** in the app registration
- Verify the Tenant ID and Client ID are correct

### Application crashes on launch
- Ensure .NET 8 Desktop Runtime is installed (if not using self-contained publish)
- Check Windows Event Viewer for .NET runtime errors
