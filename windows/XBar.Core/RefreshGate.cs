namespace XBar.Core;

// Owned by the app's UI thread. Cancellation is advisory; generation checks are authoritative.
public sealed class RefreshGate
{
    public long Generation { get; private set; }
    private bool loaded;
    public void Invalidate() { Generation++; loaded = false; }
    public bool DidLoad(long token) { if (token != Generation) return false; loaded = true; return true; }
    public bool CanAccept(long token) => token == Generation && loaded;
}
public sealed class RefreshRetry
{
    private int attempts;
    public DateTimeOffset Next { get; private set; } = DateTimeOffset.MinValue;
    public bool Begin(DateTimeOffset now)
    {
        if (now < Next) return false;
        Next = now.AddSeconds(Math.Min(30 * Math.Pow(2, Math.Min(attempts++, 4)), 300)); return true;
    }
    public void Reset() { attempts = 0; Next = DateTimeOffset.MinValue; }
}
public static class NavigationPolicy
{
    public static bool Trusted(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var u) && u.Scheme == "https" && u.UserInfo == "" && u.Port == 443 && u.Host is "x.com" or "www.x.com";
    public static bool Allowed(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var u) && u.Scheme == "https" && u.UserInfo == "" && u.Port == 443 && new[] { "x.com", "twitter.com", "appleid.apple.com" }.Any(d => u.Host == d || u.Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
    public static bool CanRecover(string? url, PageReading reading) => Trusted(url) && reading.Status == "off_page" && reading.CanRecover && MetricRecord.ValidAccount(reading.Account);
}
