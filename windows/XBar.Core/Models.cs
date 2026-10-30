using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace XBar.Core;

public enum DisplayMode { Desktop, Windows, Both }
public enum ContentPeriod { Today, TwoDays, Week }
public enum CollectionStatus { Disconnected, Loading, Ready, Offline, Sleeping, NeedsLogin, Error, Stopped }
public sealed record DesktopBounds(double Left = 60, double Top = 60, double Width = 350, double Height = 440);
public sealed record AppSettings
{
    public DisplayMode Mode { get; init; } = DisplayMode.Desktop;
    public ContentPeriod Period { get; init; }
    public int RefreshMinutes { get; init; } = 10;
    public string Language { get; init; } = "auto";
    public bool AlwaysOnTop { get; init; }
    public bool Growth { get; init; }
    public bool Effects { get; init; } = true;
    public bool ShowPosts { get; init; } = true;
    public bool Connected { get; init; }
    public bool PendingSessionClear { get; init; }
    public DesktopBounds Bounds { get; init; } = new();
    public AppSettings Normalize() => this with {
        Mode = Enum.IsDefined(Mode) ? Mode : DisplayMode.Desktop,
        Period = Enum.IsDefined(Period) ? Period : ContentPeriod.Today,
        RefreshMinutes = RefreshMinutes is 5 or 10 or 30 ? RefreshMinutes : 10,
        Language = Language is "auto" or "ko" or "en" or "ja" ? Language : "auto",
        Bounds = Bounds is not null && double.IsFinite(Bounds.Left) && double.IsFinite(Bounds.Top) &&
            double.IsFinite(Bounds.Width) && double.IsFinite(Bounds.Height)
            ? Bounds with { Width = Math.Clamp(Bounds.Width, 300, 700), Height = Math.Clamp(Bounds.Height, 210, 900) } : new()
    };
}
public sealed record DateRange(string From, string To)
{
    public string Key => $"{From}|{To}";
    public string Url => $"https://x.com/i/account_analytics/content?type=posts&sort=impressions&dir=desc&from={From}&to={To}";
    public static DateRange For(ContentPeriod period, DateTimeOffset now, TimeZoneInfo zone)
    {
        var day = TimeZoneInfo.ConvertTime(now, zone).Date;
        return new(day.AddDays(period == ContentPeriod.Week ? -6 : period == ContentPeriod.TwoDays ? -1 : 0).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
public sealed record MetricRecord(string Display, string DayKey, string TimeZone, string Account, DateTimeOffset CapturedAt)
{
    private static readonly Regex Number = new(@"\A(?:0|[1-9]\d{0,14}|[1-9]\d{0,2}(?:,\d{3})+)(?:\.\d{1,3})?(?: ?[KMBkmb천만억万億])?\z", RegexOptions.CultureInvariant);
    public static bool ValidDisplay(string? value) => value is { Length: <= 24 } && Number.IsMatch(value);
    public static bool ValidAccount(string? account) => account is not null && Regex.IsMatch(account, @"\A@[A-Za-z0-9_]{1,15}\z");
    public static string Day(DateTimeOffset now, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(now, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string ZoneId(TimeZoneInfo zone) => TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var id) ? id : zone.Id;
    public bool Current(DateTimeOffset now, TimeZoneInfo zone) => DayKey == Day(now, zone) && TimeZone == ZoneId(zone);
}
public sealed record DisplayCount(double Value, bool Approximate)
{
    public long? ExactInteger => Approximate ? null : (long)Value;
    public static DisplayCount? Parse(string? text)
    {
        if (!MetricRecord.ValidDisplay(text)) return null;
        var compact = text!.Replace(",", "").Replace(" ", "");
        var multiplier = compact[^1] switch { 'K' or 'k' or '천' => 1e3, 'M' or 'm' => 1e6, 'B' or 'b' => 1e9, '만' or '万' => 1e4, '억' or '億' => 1e8, _ => 1d };
        if (!double.TryParse(multiplier == 1 ? compact : compact[..^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value * multiplier > 9e15) return null;
        return new(value * multiplier, multiplier != 1 || compact.Contains('.'));
    }
}
public sealed record ContentPost(string Id, string Title, string Impressions, string Likes, string Reposts)
{
    [JsonIgnore] public bool IsValid => Id is not null && Regex.IsMatch(Id, @"\A[0-9]{10,25}\z") && !string.IsNullOrEmpty(Title) && new StringInfo(Title).LengthInTextElements <= 500 && DisplayCount.Parse(Impressions) is not null && DisplayCount.Parse(Likes) is not null && DisplayCount.Parse(Reposts) is not null;
    [JsonIgnore] public string AnalyticsUrl => $"https://x.com/i/account_analytics/content/{Id}";
}
public sealed record RankedPost(ContentPost Post, double? Increase, bool Approximate, DateTimeOffset ObservedSince);
public sealed record RepostSurge(ContentPost Post, long Delta, int Minutes, DateTimeOffset DetectedAt);
public sealed record ContentScope(string Account, ContentPeriod Period, string DayKey, string TimeZone);
public sealed record PageReading(string Status, string? Display = null, string? DayKey = null, string? TimeZone = null, string? Account = null, bool CanRecover = false, string? Action = null);
public sealed record ContentReading(string Status, string? Account, string? DayKey, DateRange? Range, ContentPost[]? Posts)
{
    public bool ValidFor(string account, DateRange range, string day) => Status is "ready" or "empty" &&
        string.Equals(Account, account, StringComparison.OrdinalIgnoreCase) && DayKey == day && Range == range &&
        Posts is { Length: <= 20 } && (Status == "empty") == (Posts.Length == 0) && Posts.All(p => p is not null && p.IsValid) && Posts.Select(p => p.Id).Distinct().Count() == Posts.Length;
    public bool SameScope(ContentReading? old) => old is not null && Posts is not null && old.Posts is not null && Status == old.Status && string.Equals(Account, old.Account, StringComparison.OrdinalIgnoreCase) && DayKey == old.DayKey && Range == old.Range && Posts.Select(p => p.Id).ToHashSet().SetEquals(old.Posts.Select(p => p.Id));
}
public sealed record Snapshot
{
    public int Schema { get; init; } = 1;
    public long Sequence { get; init; }
    public long Generation { get; init; }
    public AppSettings Settings { get; init; } = new();
    public CollectionStatus Status { get; init; } = CollectionStatus.Disconnected;
    public MetricRecord? Today { get; init; }
    public ImmutableArray<RankedPost> Posts { get; init; } = [];
    public ImmutableArray<RepostSurge> Surges { get; init; } = [];
    public DateTimeOffset? PostsAt { get; init; }
    public bool PostsCurrent { get; init; }
    public ContentScope? PostsScope { get; init; }
    public bool IsCollecting { get; init; }
    public DateTimeOffset? RetryAt { get; init; }
    public DateTimeOffset Heartbeat { get; init; }
    public string Detail { get; init; } = "";
    public bool Fresh(DateTimeOffset now, TimeZoneInfo zone) => Status == CollectionStatus.Ready && Today is not null && Today.Current(now, zone) && now >= Today.CapturedAt && now - Today.CapturedAt <= TimeSpan.FromMinutes(Settings.RefreshMinutes) + TimeSpan.FromSeconds(30) && now - Heartbeat < TimeSpan.FromSeconds(90) && now >= Heartbeat;
    public bool CanShowMetric(DateTimeOffset now, TimeZoneInfo zone) => Settings.Connected &&
        Status is not (CollectionStatus.Disconnected or CollectionStatus.NeedsLogin) &&
        Today is { } record && record.Current(now, zone) && MetricRecord.ValidAccount(record.Account) &&
        MetricRecord.ValidDisplay(record.Display) && now >= record.CapturedAt &&
        now - record.CapturedAt <= TimeSpan.FromMinutes(Settings.RefreshMinutes) + TimeSpan.FromSeconds(30);
    public bool CanShowPosts(DateTimeOffset now, TimeZoneInfo zone) => CanShowMetric(now, zone) &&
        PostsScope is { } scope && string.Equals(scope.Account, Today!.Account, StringComparison.OrdinalIgnoreCase) &&
        scope.Period == Settings.Period && scope.DayKey == MetricRecord.Day(now, zone) && scope.TimeZone == MetricRecord.ZoneId(zone) &&
        PostsAt is { } captured && now >= captured && now - captured <= TimeSpan.FromMinutes(10.5);
}
