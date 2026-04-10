using System.Management.Automation;
using System.Management.Automation.Runspaces;
using SSOInventoryManager.Models;

namespace SSOInventoryManager.Services;

public class AdfsService
{
    private readonly string _adfsServer;

    public AdfsService(string adfsServer)
    {
        _adfsServer = adfsServer;
    }

    public async Task<List<AdfsApp>> GetRelyingPartyTrustsAsync(DatabaseService db)
    {
        return await Task.Run(() =>
        {
            var apps = new List<AdfsApp>();

            var connectionInfo = new WSManConnectionInfo(
                new Uri($"http://{_adfsServer}:5985/wsman"))
            {
                AuthenticationMechanism = AuthenticationMechanism.Kerberos,
                OperationTimeout = 60000,
                OpenTimeout = 30000
            };

            using var runspace = RunspaceFactory.CreateRunspace(connectionInfo);
            runspace.Open();

            using var ps = PowerShell.Create();
            ps.Runspace = runspace;
            ps.AddCommand("Get-AdfsRelyingPartyTrust");

            var results = ps.Invoke();

            if (ps.HadErrors)
            {
                var errors = string.Join(Environment.NewLine,
                    ps.Streams.Error.Select(e => e.ToString()));
                throw new Exception($"ADFS PowerShell errors: {errors}");
            }

            foreach (var result in results)
            {
                var props = result.Properties;

                string name = GetProp<string>(props, "Name") ?? string.Empty;
                var identifiers = GetProp<List<string>>(props, "Identifier");
                string identifier = identifiers != null && identifiers.Count > 0
                    ? string.Join(", ", identifiers)
                    : (GetPropAsString(props, "Identifier") ?? string.Empty);

                // Detect protocol
                string protocol = "Unknown";
                var wsFedEndpoint = GetPropAsString(props, "WSFedEndpoint");
                var samlEndpoints = GetProp<object>(props, "SamlEndpoints");
                bool hasSaml = samlEndpoints != null && samlEndpoints.ToString() != "";

                if (!string.IsNullOrEmpty(wsFedEndpoint) && !hasSaml)
                    protocol = "WS-Federation";
                else if (hasSaml && string.IsNullOrEmpty(wsFedEndpoint))
                    protocol = "SAML";
                else if (!string.IsNullOrEmpty(wsFedEndpoint) && hasSaml)
                    protocol = "WS-Fed + SAML";
                else if (!string.IsNullOrEmpty(wsFedEndpoint))
                    protocol = "WS-Federation";

                // Build endpoint URLs
                var endpoints = new List<string>();
                if (!string.IsNullOrEmpty(wsFedEndpoint))
                    endpoints.Add(wsFedEndpoint);
                if (samlEndpoints is System.Collections.IEnumerable samlList)
                {
                    foreach (var ep in samlList)
                        endpoints.Add(ep.ToString() ?? string.Empty);
                }

                // Count issuance rules
                int issuanceRulesCount = 0;
                var rules = GetPropAsString(props, "IssuanceTransformRules");
                if (!string.IsNullOrWhiteSpace(rules))
                {
                    issuanceRulesCount = rules.Split("=>", StringSplitOptions.RemoveEmptyEntries).Length;
                    if (issuanceRulesCount > 0) issuanceRulesCount--; // adjustment for split behavior
                    if (issuanceRulesCount < 1) issuanceRulesCount = rules.Split('\n')
                        .Count(l => l.Trim().StartsWith("@RuleName"));
                }

                var key = !string.IsNullOrEmpty(identifier) ? identifier : name;
                var (notes, flagged) = db.GetAppData(key, "ADFS");

                apps.Add(new AdfsApp
                {
                    DisplayName = name,
                    Identifier = identifier,
                    Protocol = protocol,
                    Enabled = GetProp<bool>(props, "Enabled"),
                    MonitoringEnabled = GetProp<bool>(props, "MonitoringEnabled"),
                    LastUpdated = GetProp<DateTime?>(props, "LastUpdateTime")?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty,
                    EndpointUrls = string.Join(", ", endpoints),
                    IssuanceRulesCount = Math.Max(issuanceRulesCount, 0),
                    Notes = notes,
                    FlaggedForReview = flagged
                });
            }

            return apps;
        });
    }

    private static T? GetProp<T>(PSMemberInfoCollection<PSPropertyInfo> props, string name)
    {
        try
        {
            var prop = props[name];
            if (prop?.Value is T val) return val;
            return default;
        }
        catch
        {
            return default;
        }
    }

    private static string? GetPropAsString(PSMemberInfoCollection<PSPropertyInfo> props, string name)
    {
        try
        {
            var prop = props[name];
            return prop?.Value?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
