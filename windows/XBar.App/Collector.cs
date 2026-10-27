using System.Collections.Immutable;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using XBar.Core;

namespace XBar.App;

internal sealed class Collector : IDisposable
{
    private const string Overview = "https://x.com/i/account_analytics";
    private readonly StateStore store;
    private readonly WebSession web;
    private readonly Action<BrowserSurface>? prepareSurface;
    private readonly RefreshGate gate = new();
    private readonly RefreshRetry retry = new();
    private readonly PostLedger ledger = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private CancellationTokenSource? scan;
    private BrowserSurface? overview, content, browser;
    private Task? running;
    private bool pendingRefresh, pendingManual;
    private readonly Func<bool> networkAvailable;
    internal TimeSpan ScanDeadline { get; set; } = TimeSpan.FromSeconds(100);
    internal bool IsRunning => running is { IsCompleted: false };
    private bool verifyingLogin;
    private bool sleeping, disposed, disconnecting;
    private bool wasOnline;
    private string environmentKey = "";
    public AppSettings Settings { get; private set; }
    public Snapshot State { get; private set; }
    public event Action? Changed;
    public event Action<Exception>? Failed;
    public Collector(StateStore store, Action<BrowserSurface>? prepareSurface = null, Func<bool>? networkAvailable = null)
    {
        this.store = store; this.prepareSurface = prepareSurface; web = new(store.Root); Settings = store.LoadSettings();
        this.networkAvailable = networkAvailable ?? NetworkInterface.GetIsNetworkAvailable; wasOnline = this.networkAvailable();
        environmentKey = MetricRecord.Day(DateTimeOffset.UtcNow, TimeZoneInfo.Local) + "|" + MetricRecord.ZoneId(TimeZoneInfo.Local);
        State = new() { Settings = Settings, Status = Settings.Connected ? CollectionStatus.Loading : CollectionStatus.Disconnected };
        Publish(); timer.Tick += (_, _) => Tick(); timer.Start();
    }
    public void UpdateSettings(AppSettings value)
    {
        var previous = Settings; Settings = value.Normalize(); store.Write("settings.json", Settings);
        if (Settings.Period != previous.Period)
        {
            Invalidate(false); ledger.Reset(); State = State with { Posts = [], Surges = [], PostsAt = null, PostsScope = null, PostsCurrent = false }; retry.Reset();
        }
        State = State with { Settings = Settings, Posts = ledger.TopFive(Settings.Growth).ToImmutableArray() };
        if (!Settings.Effects) State = State with { Surges = [] };
        Publish(); if (Settings.Period != previous.Period) RequestRefresh(true);
    }
    private void Publish()
    {
        State = State with { Sequence = State.Sequence + 1, Generation = gate.Generation, Settings = Settings, Heartbeat = DateTimeOffset.UtcNow };
        store.Write("snapshot.json", State); Changed?.Invoke();
    }
    private void Tick()
    {
        if (disposed || disconnecting) return;
        try
        {
            var now = DateTimeOffset.UtcNow; var zone = TimeZoneInfo.Local;
            var key = MetricRecord.Day(now, zone) + "|" + MetricRecord.ZoneId(zone);
            bool online = networkAvailable();
            if (key != environmentKey || online != wasOnline)
            {
                environmentKey = key; wasOnline = online; Invalidate(key != State.Today?.DayKey + "|" + State.Today?.TimeZone);
                ledger.Reset(); State = State with { Posts = [], PostsAt = null, PostsScope = null, Surges = [], PostsCurrent = false }; retry.Reset();
            }
            if (Settings.Connected && State.Status != CollectionStatus.NeedsLogin && !sleeping && online && browser?.UserVisible != true && (State.Today is null || !State.Fresh(now, zone) || !State.PostsCurrent || State.PostsAt is null || now - State.PostsAt >= TimeSpan.FromMinutes(10))) RequestRefresh(false);
            if (Settings.Connected && (sleeping || !online)) State = State with { Status = sleeping ? CollectionStatus.Sleeping : CollectionStatus.Offline };
            Publish();
        }
        catch (Exception e) { Failed?.Invoke(e); }
    }
    internal void CheckEnvironment() => Tick();
    public void SleepChanged(bool value)
    {
        sleeping = value; Invalidate(false); ledger.Reset(); retry.Reset();
        State = State with { Status = !Settings.Connected ? CollectionStatus.Disconnected : value ? CollectionStatus.Sleeping : CollectionStatus.Loading, Surges = [], PostsCurrent = false }; Publish();
        if (!value) RequestRefresh(false);
    }
    public void RequestRefresh(bool manual)
    {
        if (disposed || disconnecting || sleeping || !Settings.Connected) return;
        if (running is { IsCompleted: false })
        { if (scan?.IsCancellationRequested == true) { pendingRefresh = true; pendingManual |= manual; } return; }
        if (!networkAvailable()) { State = State with { Status = CollectionStatus.Offline }; Publish(); return; }
        if (!manual && browser?.UserVisible == true) return;
        if (manual) retry.Reset();
        if (!retry.Begin(DateTimeOffset.UtcNow)) return;
        running = CollectAsync(manual);
    }
    private void Invalidate(bool clear)
    {
        // NavigateAsync owns stopping its canceled navigation. A second unpaired Stop
        // can reach WebView2 after the next navigation has already started.
        gate.Invalidate(); scan?.Cancel();
        State = State with { Status = Settings.Connected ? CollectionStatus.Loading : CollectionStatus.Disconnected, Today = clear ? null : State.Today, PostsCurrent = false, PostsScope = clear ? null : State.PostsScope };
    }
    private async Task CollectAsync(bool manual)
    {
        var previousPostsCurrent = State.PostsCurrent;
        var collectPosts = manual || State.PostsAt is null || DateTimeOffset.UtcNow - State.PostsAt >= TimeSpan.FromMinutes(10) || !previousPostsCurrent;
        Invalidate(false); var token = gate.Generation;
        scan?.Dispose(); scan = new(); scan.CancelAfter(ScanDeadline); var cancellation = scan.Token;
        State = State with { Status = CollectionStatus.Loading, PostsCurrent = false, IsCollecting = true, RetryAt = null }; Publish();
        try
        {
            overview ??= await CreateSurfaceAsync(true, "X-Bar reader"); cancellation.ThrowIfCancellationRequested();
            await overview.NavigateAsync(Overview, cancellation);
            if (!gate.DidLoad(token)) return;
            PageReading? reading = null; bool recovered = false;
            for (int i = 0; i < 45; i++)
            {
                await Task.Delay(1000, cancellation);
                reading = await overview.ReadAsync<PageReading>("globalThis.TodayBarExtractor?.advance()", cancellation);
                if (reading?.Status is "login_required" or "login_session_detected")
                {
                    State = State with { Today = null, Posts = [], Surges = [], Status = CollectionStatus.NeedsLogin }; ledger.Reset(); Publish(); return;
                }
                if (reading is not null && !recovered && NavigationPolicy.CanRecover(overview.Url, reading))
                { recovered = true; await overview.NavigateAsync(Overview, cancellation); continue; }
                if (reading?.Status == "ready" && MetricRecord.ValidAccount(reading.Account) && MetricRecord.ValidDisplay(reading.Display) && reading.DayKey == MetricRecord.Day(DateTimeOffset.UtcNow, TimeZoneInfo.Local) && reading.TimeZone == MetricRecord.ZoneId(TimeZoneInfo.Local)) break;
                reading = null;
            }
            if (reading is null || !gate.CanAccept(token)) throw new InvalidOperationException("No verified overview reading");
            if (!string.Equals(State.Today?.Account, reading.Account, StringComparison.OrdinalIgnoreCase))
            { ledger.Reset(); State = State with { Posts = [], Surges = [], PostsAt = null, PostsScope = null }; collectPosts = true; }
            var now = DateTimeOffset.UtcNow;
            State = State with { Status = CollectionStatus.Ready, Today = new(reading.Display!, reading.DayKey!, reading.TimeZone!, reading.Account!, now), Detail = "" };
            if (verifyingLogin) { verifyingLogin = false; browser?.Window.Hide(); }
            Publish();
            if (!collectPosts) { retry.Reset(); State = State with { PostsCurrent = previousPostsCurrent }; Publish(); return; }
            var range = DateRange.For(Settings.Period, now, TimeZoneInfo.Local); var period = Settings.Period;
            content ??= await CreateSurfaceAsync(true, "X-Bar content reader"); cancellation.ThrowIfCancellationRequested();
            await content.NavigateAsync(range.Url, cancellation);
            ContentReading? previous = null;
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(1000, cancellation);
                var current = await content.ReadAsync<ContentReading>("globalThis.TodayBarContentExtractor?.snapshot()", cancellation);
                if (current?.Status is "login_required" or "login_session_detected" || current?.Account is string account && !string.Equals(account, reading.Account, StringComparison.OrdinalIgnoreCase))
                { Invalidate(true); ledger.Reset(); State = State with { Status = CollectionStatus.NeedsLogin, Posts = [], Surges = [] }; Publish(); return; }
                if (current?.ValidFor(reading.Account!, range, MetricRecord.Day(DateTimeOffset.UtcNow, TimeZoneInfo.Local)) != true || DateRange.For(period, DateTimeOffset.UtcNow, TimeZoneInfo.Local) != range) { previous = null; continue; }
                if (current.SameScope(previous) && gate.CanAccept(token))
                {
                    now = DateTimeOffset.UtcNow;
                    var surges = ledger.Ingest(current.Posts!, reading.Account!, period, now, TimeZoneInfo.Local);
                    retry.Reset();
                    State = State with { Posts = ledger.TopFive(Settings.Growth).ToImmutableArray(), PostsAt = now, PostsCurrent = true, PostsScope = new(reading.Account!, period, MetricRecord.Day(now, TimeZoneInfo.Local), MetricRecord.ZoneId(TimeZoneInfo.Local)),
                        Surges = Settings.Effects ? State.Surges.Where(x => now - x.DetectedAt < TimeSpan.FromMinutes(30)).Concat(surges).ToImmutableArray() : [] };
                    Publish(); return;
                }
                previous = current;
            }
            State = State with { Detail = "content.error", PostsCurrent = false, RetryAt = retry.Next }; Publish();
        }
        catch (OperationCanceledException)
        {
            ReleaseReaders();
            if (gate.Generation == token && !disposed && !disconnecting && !sleeping)
            { State = State with { Status = CollectionStatus.Error, Detail = "content.error", RetryAt = retry.Next }; Publish(); Failed?.Invoke(new TimeoutException("Collection deadline exceeded")); }
        }
        catch (Exception e)
        {
            if (cancellation.IsCancellationRequested) { ReleaseReaders(); return; }
            ReleaseReaders();
            if (gate.Generation == token)
            {
                State = State with { Status = State.Today is not null && State.Status == CollectionStatus.Ready ? CollectionStatus.Ready : CollectionStatus.Error, Detail = "content.error", PostsCurrent = false };
                State = State with { RetryAt = retry.Next }; Publish(); Failed?.Invoke(e);
            }
        }
        finally
        {
            if (!disposed && !disconnecting) { State = State with { IsCollecting = false }; Publish(); }
            if (pendingRefresh && !disposed && !disconnecting)
            { var manualNext = pendingManual; pendingRefresh = pendingManual = false; _ = Dispatcher.CurrentDispatcher.BeginInvoke(() => RequestRefresh(manualNext)); }
        }
    }
    private void ReleaseReaders()
    {
        // A canceled Chromium navigation can leave a pending abort on its controller.
        // Recreate readers for the next scope; the shared login environment survives.
        if (overview is not null) web.Release(overview);
        if (content is not null) web.Release(content);
        overview = content = null;
    }
    public async Task OpenBrowserAsync(string? postId = null, bool login = false)
    {
        if (disconnecting) return;
        if (Settings.PendingSessionClear)
        {
            await web.ClearAsync(); overview = content = browser = null;
            Settings = Settings with { PendingSessionClear = false, Connected = false }; store.Write("settings.json", Settings);
        }
        // A stale login command must not discard a working session or reopen sign-in.
        login = login && (!Settings.Connected || State.Status == CollectionStatus.NeedsLogin);
        if (login) { Invalidate(true); Publish(); }
        if (browser?.Failed == true) { web.Release(browser); browser = null; }
        browser ??= await CreateSurfaceAsync(false, "X-Bar · X");
        browser.SetLanguage(Settings.Language, login);
        browser.VerifyRequested = () => _ = VerifyConnectionAsync();
        browser.Window.Show(); browser.Window.Activate();
        await browser.NavigateAsync(postId is not null ? "https://x.com/i/account_analytics/content/" + postId : login ? "https://x.com/i/jf/onboarding/web?redirect_after_login=%2Fi%2Faccount_analytics&mode=login" : Overview, CancellationToken.None);
    }
    public async Task DisconnectAsync()
    {
        if (disconnecting) return;
        disconnecting = true; verifyingLogin = false; pendingRefresh = pendingManual = false; Invalidate(true); ledger.Reset();
        Settings = Settings with { Connected = false, PendingSessionClear = true }; store.Write("settings.json", Settings);
        State = new() { Settings = Settings, Status = CollectionStatus.Disconnected }; Publish();
        try { if (running is not null) await running; await web.ClearAsync(); overview = content = browser = null;
            Settings = Settings with { PendingSessionClear = false }; store.Write("settings.json", Settings); Publish(); }
        finally { disconnecting = false; }
    }
    private async Task VerifyConnectionAsync()
    {
        if (verifyingLogin || disconnecting) return;
        verifyingLogin = true;
        try
        {
            if (running is not null) await running;
            if (!verifyingLogin || disconnecting) return;
            Settings = Settings with { Connected = true }; store.Write("settings.json", Settings);
            RequestRefresh(true);
            if (running is not null) await running;
        }
        catch (Exception e) { Failed?.Invoke(e); }
        finally { verifyingLogin = false; }
    }
    private async Task<BrowserSurface> CreateSurfaceAsync(bool reader, string title)
    { var surface = await web.CreateAsync(reader, title); prepareSurface?.Invoke(surface); return surface; }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; timer.Stop(); Invalidate(false);
        State = State with { Status = Settings.Connected ? CollectionStatus.Stopped : CollectionStatus.Disconnected, IsCollecting = false }; Publish();
        web.Dispose(); scan?.Dispose();
    }
}
