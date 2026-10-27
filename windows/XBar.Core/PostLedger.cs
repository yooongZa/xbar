namespace XBar.Core;

public sealed class PostLedger
{
    private sealed record Baseline(DisplayCount Count, DateTimeOffset At);
    private sealed record Sample(long Count, DateTimeOffset At, double? Rate = null);
    private readonly Dictionary<string, Baseline> baselines = [];
    private readonly Dictionary<string, Sample> samples = [];
    private readonly Dictionary<string, DateTimeOffset> alerts = [];
    private string? context;
    public IReadOnlyList<RankedPost> Rows { get; private set; } = [];
    public void Reset() { baselines.Clear(); samples.Clear(); alerts.Clear(); context = null; Rows = []; }
    public IReadOnlyList<RepostSurge> Ingest(IEnumerable<ContentPost> posts, string account, ContentPeriod period, DateTimeOffset now, TimeZoneInfo zone)
    {
        var key = $"{account.ToLowerInvariant()}|{MetricRecord.Day(now, zone)}|{MetricRecord.ZoneId(zone)}|{period}";
        if (context != key) { Reset(); context = key; }
        foreach (var id in alerts.Where(x => now - x.Value >= TimeSpan.FromMinutes(30)).Select(x => x.Key).ToArray()) alerts.Remove(id);
        var seen = new HashSet<string>(); var rows = new List<RankedPost>(); var events = new List<RepostSurge>();
        foreach (var post in posts.Where(p => p.IsValid && seen.Add(p.Id)))
        {
            var count = DisplayCount.Parse(post.Impressions)!;
            if (!baselines.TryGetValue(post.Id, out var baseline) || count.Value < baseline.Count.Value) baselines[post.Id] = baseline = new(count, now);
            rows.Add(new(post, now > baseline.At ? count.Value - baseline.Count.Value : null, count.Approximate || baseline.Count.Approximate, baseline.At));
            if (DisplayCount.Parse(post.Reposts)!.ExactInteger is not long reposts) { samples.Remove(post.Id); continue; }
            if (!samples.TryGetValue(post.Id, out var old) || now <= old.At || now - old.At > TimeSpan.FromMinutes(15) || reposts < old.Count) { samples[post.Id] = new(reposts, now); continue; }
            var elapsed = (now - old.At).TotalSeconds;
            if (elapsed < 300) continue;
            var delta = reposts - old.Count; var rate = delta / elapsed * 600;
            if (delta >= 5 && rate >= 5 && old.Rate is double prior && rate >= Math.Max(prior, 1) * 3 && !alerts.ContainsKey(post.Id))
            { alerts[post.Id] = now; events.Add(new(post, delta, (int)Math.Round(elapsed / 60, MidpointRounding.AwayFromZero), now)); }
            samples[post.Id] = new(reposts, now, rate);
        }
        foreach (var id in baselines.Keys.Where(id => !seen.Contains(id)).ToArray()) baselines.Remove(id);
        foreach (var id in samples.Keys.Where(id => !seen.Contains(id)).ToArray()) samples.Remove(id);
        Rows = rows;
        return events;
    }
    public IReadOnlyList<RankedPost> TopFive(bool growth) => Rows.Where(p => !growth || p.Increase.HasValue).OrderByDescending(p => growth ? p.Increase!.Value : DisplayCount.Parse(p.Post.Impressions)!.Value).ThenByDescending(p => p.Post.Id, StringComparer.Ordinal).Take(5).ToArray();
}
