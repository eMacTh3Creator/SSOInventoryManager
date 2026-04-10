using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using SSOInventoryManager.Models;

namespace SSOInventoryManager.Services;

public class GraphService
{
    private GraphServiceClient? _client;
    private readonly ConnectionSettings _settings;
    private bool _hasAuditLogPermission = true;

    public bool HasAuditLogPermission => _hasAuditLogPermission;

    public GraphService(ConnectionSettings settings)
    {
        _settings = settings;
    }

    public async Task<string?> AuthenticateAsync(Action<string>? deviceCodeCallback = null)
    {
        try
        {
            string[] scopes = ["https://graph.microsoft.com/.default"];

            if (_settings.UseDeviceCodeFlow)
            {
                var app = PublicClientApplicationBuilder
                    .Create(_settings.ClientId)
                    .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
                    .Build();

                var result = await app.AcquireTokenWithDeviceCode(scopes, callback =>
                {
                    deviceCodeCallback?.Invoke(callback.Message);
                    return Task.CompletedTask;
                }).ExecuteAsync();

                var tokenProvider = new BaseBearerTokenAuthenticationProvider(
                    new TokenProvider(result.AccessToken));
                _client = new GraphServiceClient(tokenProvider);
            }
            else
            {
                var app = ConfidentialClientApplicationBuilder
                    .Create(_settings.ClientId)
                    .WithClientSecret(_settings.ClientSecret)
                    .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
                    .Build();

                var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();

                var tokenProvider = new BaseBearerTokenAuthenticationProvider(
                    new TokenProvider(result.AccessToken));
                _client = new GraphServiceClient(tokenProvider);
            }

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<List<AzureApp>> GetApplicationsAsync(DatabaseService db)
    {
        if (_client == null) throw new InvalidOperationException("Not authenticated.");

        var apps = new List<AzureApp>();

        // Get all service principals
        var spResponse = await _client.ServicePrincipals.GetAsync(config =>
        {
            config.QueryParameters.Top = 999;
            config.QueryParameters.Select = new[]
            {
                "displayName", "appId", "preferredSingleSignOnMode",
                "accountEnabled", "homepage", "replyUrls", "tags",
                "signInActivity"
            };
        });

        var servicePrincipals = new List<ServicePrincipal>();
        var pageIterator = Microsoft.Graph.PageIterator<ServicePrincipal, ServicePrincipalCollectionResponse>
            .CreatePageIterator(_client, spResponse!, sp =>
            {
                servicePrincipals.Add(sp);
                return true;
            });
        await pageIterator.IterateAsync();

        // Build lookup of app registrations for signInAudience and publisherDomain
        var appRegLookup = new Dictionary<string, Application>();
        try
        {
            var appResponse = await _client.Applications.GetAsync(config =>
            {
                config.QueryParameters.Top = 999;
                config.QueryParameters.Select = new[] { "appId", "signInAudience", "publisherDomain" };
            });

            var appIterator = Microsoft.Graph.PageIterator<Application, ApplicationCollectionResponse>
                .CreatePageIterator(_client, appResponse!, app =>
                {
                    if (app.AppId != null)
                        appRegLookup[app.AppId] = app;
                    return true;
                });
            await appIterator.IterateAsync();
        }
        catch { /* May not have permission */ }

        foreach (var sp in servicePrincipals)
        {
            var appId = sp.AppId ?? string.Empty;

            // Get role assignment count
            int assignmentCount = 0;
            try
            {
                var assignments = await _client.ServicePrincipals[sp.Id].AppRoleAssignedTo.GetAsync(config =>
                {
                    config.QueryParameters.Top = 999;
                });
                assignmentCount = assignments?.Value?.Count ?? 0;
            }
            catch { }

            // Determine last sign-in
            string lastSignIn = "N/A";
            try
            {
                var activity = sp.SignInActivity;
                if (activity?.LastSignInDateTime != null)
                    lastSignIn = activity.LastSignInDateTime.Value.ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                _hasAuditLogPermission = false;
            }

            // Determine app type from tags
            string appType = "Enterprise App";
            if (sp.Tags != null && sp.Tags.Contains("WindowsAzureActiveDirectoryIntegratedApp"))
                appType = "Enterprise App";
            else if (sp.Tags != null && sp.Tags.Contains("WindowsAzureActiveDirectoryGalleryApplicationNonPrimaryV1"))
                appType = "Gallery App";
            if (appRegLookup.ContainsKey(appId))
                appType = "App Registration";

            // Get app registration details
            appRegLookup.TryGetValue(appId, out var appReg);

            var (notes, flagged) = db.GetAppData(appId, "Azure");

            apps.Add(new AzureApp
            {
                DisplayName = sp.DisplayName ?? string.Empty,
                AppId = appId,
                SsoMode = sp.PreferredSingleSignOnMode ?? "none",
                SignInAudience = appReg?.SignInAudience ?? string.Empty,
                PublisherDomain = appReg?.PublisherDomain ?? string.Empty,
                Enabled = sp.AccountEnabled ?? false,
                AssignedUsersGroups = assignmentCount,
                LastSignIn = lastSignIn,
                HomepageUrl = sp.Homepage ?? string.Empty,
                ReplyUrls = sp.ReplyUrls != null ? string.Join(", ", sp.ReplyUrls) : string.Empty,
                AppType = appType,
                Notes = notes,
                FlaggedForReview = flagged
            });
        }

        return apps;
    }

    private class TokenProvider : IAccessTokenProvider
    {
        private readonly string _token;
        public TokenProvider(string token) => _token = token;
        public AllowedHostsValidator AllowedHostsValidator { get; } = new();

        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_token);
    }
}
