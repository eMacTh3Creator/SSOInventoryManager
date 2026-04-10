namespace SSOInventoryManager.Models;

public class AdfsApp : ObservableBase
{
    public string DisplayName { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool MonitoringEnabled { get; set; }
    public string LastUpdated { get; set; } = string.Empty;
    public string EndpointUrls { get; set; } = string.Empty;
    public int IssuanceRulesCount { get; set; }

    private string _notes = string.Empty;
    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    private bool _flaggedForReview;
    public bool FlaggedForReview
    {
        get => _flaggedForReview;
        set => SetProperty(ref _flaggedForReview, value);
    }

    public bool MatchesFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        var f = filter.ToLowerInvariant();
        return DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase)
            || Identifier.Contains(f, StringComparison.OrdinalIgnoreCase)
            || Protocol.Contains(f, StringComparison.OrdinalIgnoreCase)
            || EndpointUrls.Contains(f, StringComparison.OrdinalIgnoreCase)
            || Notes.Contains(f, StringComparison.OrdinalIgnoreCase);
    }
}
