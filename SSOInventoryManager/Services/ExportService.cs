using System.IO;
using System.Text;
using ClosedXML.Excel;
using SSOInventoryManager.Models;

namespace SSOInventoryManager.Services;

public static class ExportService
{
    public static void ExportAzureToCsv(IEnumerable<AzureApp> apps, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Display Name,App ID,SSO Mode,Sign-In Audience,Publisher Domain,Enabled,Assigned Users/Groups,Last Sign-In,Homepage URL,Reply URLs,App Type,Flagged,Notes");

        foreach (var app in apps)
        {
            sb.AppendLine(string.Join(",",
                Escape(app.DisplayName),
                Escape(app.AppId),
                Escape(app.SsoMode),
                Escape(app.SignInAudience),
                Escape(app.PublisherDomain),
                app.Enabled,
                app.AssignedUsersGroups,
                Escape(app.LastSignIn),
                Escape(app.HomepageUrl),
                Escape(app.ReplyUrls),
                Escape(app.AppType),
                app.FlaggedForReview,
                Escape(app.Notes)));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    public static void ExportAdfsToCsv(IEnumerable<AdfsApp> apps, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Display Name,Identifier,Protocol,Enabled,Monitoring Enabled,Last Updated,Endpoint URLs,Issuance Rules Count,Flagged,Notes");

        foreach (var app in apps)
        {
            sb.AppendLine(string.Join(",",
                Escape(app.DisplayName),
                Escape(app.Identifier),
                Escape(app.Protocol),
                app.Enabled,
                app.MonitoringEnabled,
                Escape(app.LastUpdated),
                Escape(app.EndpointUrls),
                app.IssuanceRulesCount,
                app.FlaggedForReview,
                Escape(app.Notes)));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    public static void ExportToExcel(IEnumerable<AzureApp> azureApps, IEnumerable<AdfsApp> adfsApps, string filePath)
    {
        using var workbook = new XLWorkbook();

        // Azure sheet
        var azureSheet = workbook.AddWorksheet("Azure Entra Apps");
        var azureHeaders = new[] { "Display Name", "App ID", "SSO Mode", "Sign-In Audience", "Publisher Domain",
            "Enabled", "Assigned Users/Groups", "Last Sign-In", "Homepage URL", "Reply URLs", "App Type", "Flagged", "Notes" };

        for (int i = 0; i < azureHeaders.Length; i++)
        {
            var cell = azureSheet.Cell(1, i + 1);
            cell.Value = azureHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x33, 0x33, 0x33);
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var app in azureApps)
        {
            azureSheet.Cell(row, 1).Value = app.DisplayName;
            azureSheet.Cell(row, 2).Value = app.AppId;
            azureSheet.Cell(row, 3).Value = app.SsoMode;
            azureSheet.Cell(row, 4).Value = app.SignInAudience;
            azureSheet.Cell(row, 5).Value = app.PublisherDomain;
            azureSheet.Cell(row, 6).Value = app.Enabled ? "Yes" : "No";
            azureSheet.Cell(row, 7).Value = app.AssignedUsersGroups;
            azureSheet.Cell(row, 8).Value = app.LastSignIn;
            azureSheet.Cell(row, 9).Value = app.HomepageUrl;
            azureSheet.Cell(row, 10).Value = app.ReplyUrls;
            azureSheet.Cell(row, 11).Value = app.AppType;
            azureSheet.Cell(row, 12).Value = app.FlaggedForReview ? "Yes" : "No";
            azureSheet.Cell(row, 13).Value = app.Notes;

            if (app.FlaggedForReview)
            {
                var rowRange = azureSheet.Range(row, 1, row, azureHeaders.Length);
                rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0xFF, 0xEB, 0x3B);
                rowRange.Style.Font.FontColor = XLColor.Black;
            }
            row++;
        }
        azureSheet.Columns().AdjustToContents(1, 100);

        // ADFS sheet
        var adfsSheet = workbook.AddWorksheet("ADFS Apps");
        var adfsHeaders = new[] { "Display Name", "Identifier", "Protocol", "Enabled", "Monitoring Enabled",
            "Last Updated", "Endpoint URLs", "Issuance Rules Count", "Flagged", "Notes" };

        for (int i = 0; i < adfsHeaders.Length; i++)
        {
            var cell = adfsSheet.Cell(1, i + 1);
            cell.Value = adfsHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x33, 0x33, 0x33);
            cell.Style.Font.FontColor = XLColor.White;
        }

        row = 2;
        foreach (var app in adfsApps)
        {
            adfsSheet.Cell(row, 1).Value = app.DisplayName;
            adfsSheet.Cell(row, 2).Value = app.Identifier;
            adfsSheet.Cell(row, 3).Value = app.Protocol;
            adfsSheet.Cell(row, 4).Value = app.Enabled ? "Yes" : "No";
            adfsSheet.Cell(row, 5).Value = app.MonitoringEnabled ? "Yes" : "No";
            adfsSheet.Cell(row, 6).Value = app.LastUpdated;
            adfsSheet.Cell(row, 7).Value = app.EndpointUrls;
            adfsSheet.Cell(row, 8).Value = app.IssuanceRulesCount;
            adfsSheet.Cell(row, 9).Value = app.FlaggedForReview ? "Yes" : "No";
            adfsSheet.Cell(row, 10).Value = app.Notes;

            if (app.FlaggedForReview)
            {
                var rowRange = adfsSheet.Range(row, 1, row, adfsHeaders.Length);
                rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0xFF, 0xEB, 0x3B);
                rowRange.Style.Font.FontColor = XLColor.Black;
            }
            row++;
        }
        adfsSheet.Columns().AdjustToContents(1, 100);

        workbook.SaveAs(filePath);
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return $"\"{value}\"";
    }
}
