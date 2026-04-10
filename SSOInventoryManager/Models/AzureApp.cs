namespace SSOInventoryManager.Models;

public class AzureApp : ObservableBase
{
    public string DisplayName { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string SsoMode { get; set; } = string.Empty;
    public string SignInAudience { get; set; } = string.Empty;
    public string PublisherDomain { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int AssignedUsersGroups { get; set; }
    public string LastSignIn { get; set; } = "N/A";
    public string HomepageUrl { get; set; } = string.Empty;
    public string ReplyUrls { get; set; } = string.Empty;
    public string AppType { get; set; } = string.Empty;

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
            || AppId.Contains(f, StringComparison.OrdinalIgnoreCase)
            || SsoMode.Contains(f, StringComparison.OrdinalIgnoreCase)
            || SignInAudience.Contains(f, StringComparison.OrdinalIgnoreCase)
            || PublisherDomain.Contains(f, StringComparison.OrdinalIgnoreCase)
            || HomepageUrl.Contains(f, StringComparison.OrdinalIgnoreCase)
            || ReplyUrls.Contains(f, StringComparison.OrdinalIgnoreCase)
            || AppType.Contains(f, StringComparison.OrdinalIgnoreCase)
            || Notes.Contains(f, StringComparison.OrdinalIgnoreCase);
    }
}
