namespace XBar.Core;

public sealed class PostLedger
{
    private sealed record Baseline(DisplayCount Count, DateTimeOffset At);
    private readonly Dictionary<string, Baseline> baselines = [];
    private ImpressionDetector detector = new();
    private string? context;
    public IReadOnlyList<RankedPost> Rows { get; private set; } = [];
    public void Reset() { baselines.Clear(); detector = new(); context = null; Rows = []; }
    public IReadOnlyList<ImpressionSurge> Ingest(IEnumerable<ContentPost> posts, string account, ContentPeriod period, DateTimeOffset now, TimeZoneInfo zone)
    {
        var key = $"{account.ToLowerInvariant()}|{MetricRecord.Day(now, zone)}|{MetricRecord.ZoneId(zone)}|{period}";
        if (context != key) { Reset(); context = key; }
        var seen = new HashSet<string>(); var rows = new List<RankedPost>();
        foreach (var post in posts.Where(p => p.IsValid && seen.Add(p.Id)))
        {
            var count = DisplayCount.Parse(post.Impressions)!;
            if (!baselines.TryGetValue(post.Id, out var baseline) || count.Value < baseline.Count.Value) baselines[post.Id] = baseline = new(count, now);
            rows.Add(new(post, now > baseline.At ? count.Value - baseline.Count.Value : null, count.Approximate || baseline.Count.Approximate, baseline.At));
        }
        foreach (var id in baselines.Keys.Where(id => !seen.Contains(id)).ToArray()) baselines.Remove(id);
        Rows = rows;
        return detector.Observe(rows.Select(x => x.Post), now);
    }
    public IReadOnlyList<RankedPost> TopFive(bool growth) => Rows.Where(p => !growth || p.Increase.HasValue).OrderByDescending(p => growth ? p.Increase!.Value : DisplayCount.Parse(p.Post.Impressions)!.Value).ThenByDescending(p => p.Post.Id, StringComparer.Ordinal).Take(5).ToArray();
}
