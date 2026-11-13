namespace XBar.Core;

public sealed class ImpressionDetector
{
    private sealed record Sample(DisplayCount Count, double RoundingUnit, DateTimeOffset At, double LatestValue, DateTimeOffset LatestAt);
    private sealed record Observation(ContentPost Post, double Delta, double Rate, double MinimumRate, double Elapsed, bool Approximate);
    private sealed record NormalRate(double Rate, DateTimeOffset At);
    private readonly Dictionary<string, Sample> samples = [];
    private readonly Dictionary<string, DateTimeOffset> alerts = [];
    private readonly List<NormalRate> history = [];

    public IReadOnlyList<ImpressionSurge> Observe(IEnumerable<ContentPost> posts, DateTimeOffset now)
    {
        foreach (var id in alerts.Where(x => now < x.Value || now - x.Value >= TimeSpan.FromMinutes(30)).Select(x => x.Key).ToArray()) alerts.Remove(id);
        history.RemoveAll(x => now < x.At || now - x.At >= TimeSpan.FromHours(1));
        var observations = new List<Observation>();
        var seen = new HashSet<string>();
        foreach (var post in posts.Where(p => p.IsValid && seen.Add(p.Id)))
        {
            var count = DisplayCount.Parse(post.Impressions)!;
            var roundingUnit = DisplayCount.RoundingUnit(post.Impressions);
            var current = new Sample(count, roundingUnit, now, count.Value, now);
            if (!samples.TryGetValue(post.Id, out var old)) { samples[post.Id] = current; continue; }
            var elapsed = (now - old.At).TotalSeconds;
            var gap = (now - old.LatestAt).TotalSeconds;
            var approximate = count.Approximate || old.Count.Approximate;
            if (gap <= 0 || gap > 900 || elapsed > 3600 || (elapsed > 900 && !approximate) || count.Value < old.LatestValue)
            { samples[post.Id] = current; continue; }
            var continued = old with { LatestValue = count.Value, LatestAt = now };
            if (elapsed < 300) { samples[post.Id] = continued; continue; }
            var delta = count.Value - old.Count.Value;
            // Reserve one display unit at each endpoint for rounding or truncation.
            var minimumDelta = Math.Max(0, delta - roundingUnit - old.RoundingUnit);
            // Keep coarse counts across consecutive captures until growth is measurable.
            if (approximate && minimumDelta == 0 && elapsed < 3600) { samples[post.Id] = continued; continue; }
            observations.Add(new(post, delta, delta / elapsed * 600, minimumDelta / elapsed * 600, elapsed, approximate));
            samples[post.Id] = current;
        }
        foreach (var id in samples.Keys.Where(id => !seen.Contains(id)).ToArray()) samples.Remove(id);
        var normalRate = Median(history.Select(x => x.Rate));
        var events = new List<ImpressionSurge>();
        foreach (var observation in observations)
        {
            var reference = normalRate ?? Median(observations.Where(x => x.Post.Id != observation.Post.Id && x.MinimumRate > 0).Select(x => x.Rate)) ?? 0;
            var qualifies = observation.MinimumRate >= Math.Max(1000, reference * 5);
            if (qualifies)
            {
                if (!alerts.ContainsKey(observation.Post.Id))
                {
                    alerts[observation.Post.Id] = now;
                    events.Add(new(observation.Post, (long)observation.Delta, (int)Math.Round(observation.Elapsed / 60, MidpointRounding.AwayFromZero), now, observation.Approximate));
                }
            }
            else if (observation.MinimumRate > 0) history.Add(new(observation.Rate, now));
        }
        // Twenty posts, observed at most once per five minutes over one hour.
        if (history.Count > 240) history.RemoveRange(0, history.Count - 240);
        return events;
    }

    private static double? Median(IEnumerable<double> values)
    {
        var sorted = values.Where(x => x > 0).Order().ToArray();
        if (sorted.Length == 0) return null;
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }
}
