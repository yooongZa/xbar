using System.Collections.Immutable;
using System.Text.Json;
using Xunit;

namespace XBar.Core.Tests;

public sealed class RegressionTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-26T02:00:00Z");
    private static ContentPost Post(string id = "2111348825839611989", string impressions = "1,000", string rt = "10") => new(id, "A post", impressions, "5", rt);
    private static Snapshot Ready() => new() { Settings = new() { Mode = DisplayMode.Both, Connected = true }, Status = CollectionStatus.Ready, Today = new("1,234", "2026-10-26", "Etc/UTC", "@fixture", Start), Heartbeat = Start };
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Etc/UTC");

    [Theory]
    [InlineData("1.2만", 12000, true)] [InlineData("12,345", 12345, false)]
    [InlineData("1.2万", 12000, true)] [InlineData("2億", 200000000, true)]
    [InlineData("1K", 1000, true)] [InlineData("1.5B", 1500000000, true)] [InlineData("0", 0, false)]
    public void CountsMatchMacRules(string text, double value, bool approximate)
    { Assert.Equal(new DisplayCount(value, approximate), DisplayCount.Parse(text)); }
    [Theory]
    [InlineData("NaN")] [InlineData("999999999999999B")] [InlineData("-75%")] [InlineData("12K\n")] [InlineData("01")] [InlineData("1,23")]
    public void InvalidCountsRejected(string text) => Assert.Null(DisplayCount.Parse(text));
    [Theory]
    [InlineData("../../home")] [InlineData("123?redirect=evil")] [InlineData("https://evil.example")] [InlineData("2111348825839611989\n")]
    public void InvalidPostLinksRejected(string id)
    { Assert.False(Post(id).IsValid); Assert.False(new AppCommand("post", id).IsValid); }

    [Fact] public void SurgeNeedsThreeSamplesAndCooldown()
    {
        var ledger = new PostLedger();
        IReadOnlyList<RepostSurge> Ingest(int minute, string rt) => ledger.Ingest([Post(rt: rt)], "@one", ContentPeriod.Today, Start.AddMinutes(minute), Zone);
        Assert.Empty(Ingest(0, "10")); Assert.Empty(Ingest(10, "11"));
        Assert.Equal(6, Assert.Single(Ingest(20, "17")).Delta); Assert.Empty(Ingest(30, "40"));
    }
    [Fact] public void ManualRefreshKeepsObservationWindow()
    {
        var l = new PostLedger();
        void Ingest(int m, string rt) => l.Ingest([Post(rt: rt)], "@one", ContentPeriod.Today, Start.AddMinutes(m), Zone);
        Ingest(0, "10"); Ingest(10, "10"); Ingest(11, "12");
        Assert.Equal(6, Assert.Single(l.Ingest([Post(rt: "16")], "@one", ContentPeriod.Today, Start.AddMinutes(20), Zone)).Delta);
    }
    [Fact] public void ApproximateCountersAndSleepNeverTriggerSurge()
    {
        var l = new PostLedger();
        for (int i = 0; i < 4; i++) Assert.Empty(l.Ingest([Post(rt: $"{i + 1}K")], "@one", ContentPeriod.Today, Start.AddMinutes(i * 10), Zone));
        l.Reset(); l.Ingest([Post()], "@one", ContentPeriod.Today, Start, Zone);
        l.Ingest([Post(rt: "11")], "@one", ContentPeriod.Today, Start.AddMinutes(10), Zone);
        Assert.Empty(l.Ingest([Post(rt: "100")], "@one", ContentPeriod.Today, Start.AddHours(1), Zone));
    }
    [Fact] public void RankingSeparatesLifetimeAndObservedGrowth()
    {
        var l = new PostLedger(); var a = "2111348825839611989"; var b = "2111348825839611988";
        l.Ingest([Post(a, "900K"), Post(b, "100")], "@one", ContentPeriod.Today, Start, Zone);
        Assert.Empty(l.TopFive(true));
        l.Ingest([Post(a, "900K"), Post(b, "150")], "@one", ContentPeriod.Today, Start.AddMinutes(10), Zone);
        Assert.Equal(a, l.TopFive(false)[0].Post.Id); Assert.Equal(b, l.TopFive(true)[0].Post.Id); Assert.Equal(50, l.TopFive(true)[0].Increase);
    }
    [Fact] public void ContextChangesAndDisappearingRowsResetBaseline()
    {
        var l = new PostLedger();
        l.Ingest([Post()], "@one", ContentPeriod.Today, Start, Zone);
        l.Ingest([Post(impressions: "1100")], "@two", ContentPeriod.Today, Start.AddMinutes(10), Zone);
        Assert.Null(l.Rows[0].Increase);
        l.Ingest([Post(impressions: "1200")], "@two", ContentPeriod.Week, Start.AddMinutes(20), Zone);
        Assert.Null(l.Rows[0].Increase);
        l.Ingest([], "@two", ContentPeriod.Week, Start.AddMinutes(30), Zone);
        l.Ingest([Post()], "@two", ContentPeriod.Week, Start.AddMinutes(40), Zone); Assert.Null(l.Rows[0].Increase);
        l.Ingest([Post()], "@two", ContentPeriod.Week, Start.AddDays(1), Zone); Assert.Null(l.Rows[0].Increase);
    }
    [Fact] public void CounterCorrectionAndDuplicatesDoNotInventGrowth()
    {
        var l = new PostLedger(); l.Ingest([Post(), Post(), Post("bad")], "@one", ContentPeriod.Today, Start, Zone); Assert.Single(l.Rows);
        l.Ingest([Post(impressions: "900")], "@one", ContentPeriod.Today, Start.AddMinutes(10), Zone); Assert.Null(l.Rows[0].Increase);
    }
    [Theory]
    [InlineData("2027-01-01T01:00:00Z", ContentPeriod.Week, "2026-12-26")]
    [InlineData("2028-03-01T01:00:00Z", ContentPeriod.TwoDays, "2028-02-29")]
    public void CalendarRangesCrossYearsAndLeapDays(string now, ContentPeriod period, string expected) => Assert.Equal(expected, DateRange.For(period, DateTimeOffset.Parse(now), Zone).From);
    [Fact] public void CalendarUsesLocalDayAcrossDst() => Assert.Equal("2026-03-08", DateRange.For(ContentPeriod.TwoDays, DateTimeOffset.Parse("2026-03-09T07:30:00Z"), TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles")).From);

    [Fact] public void ScopeStabilizationAllowsCountsButNotAccountOrIdsToChange()
    {
        var range = new DateRange("2026-10-26", "2026-10-26");
        var first = new ContentReading("ready", "@one", range.To, range, [Post()]);
        var second = first with { Account = "@ONE", Posts = [Post(impressions: "1001")] };
        Assert.True(second.SameScope(first)); Assert.True(second.ValidFor("@one", range, range.To));
        Assert.False((second with { Account = "@two" }).SameScope(first));
        Assert.False((second with { Posts = [Post("2111348825839611988")] }).SameScope(first));
        Assert.False((second with { Posts = [Post(), Post()] }).ValidFor("@one", range, range.To));
    }
    [Fact] public void OldNavigationCannotPublishAfterInvalidation()
    {
        var gate = new RefreshGate(); var old = gate.Generation; Assert.False(gate.CanAccept(old));
        Assert.True(gate.DidLoad(old)); gate.Invalidate(); Assert.False(gate.DidLoad(old)); Assert.False(gate.CanAccept(old));
        Assert.True(gate.DidLoad(gate.Generation)); Assert.True(gate.CanAccept(gate.Generation));
    }
    [Fact] public void RetriesBackOffWithoutTightReloadLoop()
    {
        var retry = new RefreshRetry(); Assert.True(retry.Begin(Start)); Assert.False(retry.Begin(Start.AddSeconds(1)));
        Assert.True(retry.Begin(Start.AddSeconds(30))); Assert.Equal(Start.AddSeconds(90), retry.Next);
        retry.Reset(); Assert.True(retry.Begin(Start.AddSeconds(31)));
    }
    [Fact] public void FreshnessRejectsCrashOldDayAndFutureTimestamps()
    {
        var ready = Ready(); Assert.True(ready.Fresh(Start, Zone));
        Assert.False(ready.Fresh(Start.AddMinutes(2), Zone));
        Assert.False((ready with { Heartbeat = Start.AddMinutes(11) }).Fresh(Start.AddMinutes(11), Zone));
        Assert.False(ready.Fresh(Start.AddDays(1), Zone)); Assert.False(ready.Fresh(Start.AddSeconds(-1), Zone));
        Assert.False((ready with { Status = CollectionStatus.Stopped }).Fresh(Start, Zone));
    }
    [Theory]
    [InlineData("https://x.com/home", true)] [InlineData("https://x.com.evil.test/home", false)] [InlineData("http://x.com/home", false)] [InlineData("https://evil@x.com/home", false)] [InlineData("https://x.com:8443/home", false)]
    public void RecoveryRequiresTrustedPage(string url, bool expected) => Assert.Equal(expected, NavigationPolicy.CanRecover(url, new("off_page", Account: "@fixture", CanRecover: true)));
    [Fact] public void AuthenticationInputBlocksRecovery() => Assert.False(NavigationPolicy.CanRecover("https://x.com/home", new("off_page", Account: "@fixture", CanRecover: false)));
    [Theory]
    [InlineData("https://accounts.google.com/o/oauth2/v2/auth", true)]
    [InlineData("https://accounts.google.com/gsi/select", true)]
    [InlineData("https://accounts.google.com.evil.example/", false)]
    [InlineData("http://accounts.google.com/", false)]
    [InlineData("https://evil@accounts.google.com/", false)]
    [InlineData("https://accounts.google.com:8443/", false)]
    [InlineData("https://appleid.apple.com/auth/authorize", false)]
    public void GoogleLoginGetsGuidanceWithoutAllowingEmbeddedAuthentication(string url, bool expected)
    { Assert.Equal(expected, NavigationPolicy.GoogleAuthentication(url)); if (expected) Assert.False(NavigationPolicy.Allowed(url)); }
    [Fact] public void CardsHideStaleOrDisconnectedAccountData()
    {
        var ready = Ready(); var current = WidgetCard.Render(ready, "Small", Start, Zone); Assert.Contains("1,234", current);
        Assert.DoesNotContain("1,234", WidgetCard.Render(ready, "Small", Start.AddHours(1), Zone));
        Assert.DoesNotContain("1,234", WidgetCard.Render(ready with { Settings = ready.Settings with { Mode = DisplayMode.Desktop } }, "Large", Start, Zone));
        using var json = JsonDocument.Parse(current); Assert.Equal("AdaptiveCard", json.RootElement.GetProperty("type").GetString());
    }
    [Fact] public void WidgetContentCannotInjectCardActions()
    {
        var s = Ready() with { PostsCurrent = true, Posts = [new(Post() with { Title = "\"},\"type\":\"Action.OpenUrl\",\"url\":\"https://evil.example" }, null, false, Start)] };
        using var card = JsonDocument.Parse(WidgetCard.Render(s, "Large", Start, Zone));
        Assert.Equal(2, card.RootElement.GetProperty("actions").GetArrayLength());
    }
    [Fact] public async Task SnapshotAtomicReplaceNeverExposesPartialJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "XBar-test-" + Guid.NewGuid()); var store = new StateStore(root);
        try
        {
            store.Write("snapshot.json", Ready());
            var reader = Task.Run(() => { for (int i = 0; i < 300; i++) { var s = store.LoadSnapshot(); Assert.NotNull(s.Today); Assert.Equal("@fixture", s.Today!.Account); } });
            for (int i = 0; i < 100; i++) store.Write("snapshot.json", Ready() with { Sequence = i });
            await reader;
            store.Write("snapshot.json", new Snapshot()); Assert.Null(store.LoadSnapshot().Today);
            store.Write("snapshot.json", Ready() with { Schema = 999 }); Assert.Null(store.LoadSnapshot().Today);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public void SettingsRoundTripPreservesAllModesAndSanitizesUnknownValues()
    {
        foreach (var mode in Enum.GetValues<DisplayMode>())
        { var settings = new AppSettings { Mode = mode, Period = ContentPeriod.Week, Connected = true, PendingSessionClear = true }; Assert.Equal(settings, JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings, StateStore.Json), StateStore.Json)); }
        Assert.Equal(10, (new AppSettings { RefreshMinutes = 0 }).Normalize().RefreshMinutes);
        Assert.Equal(DisplayMode.Desktop, (new AppSettings { Mode = (DisplayMode)999 }).Normalize().Mode);
    }
    [Theory]
    [InlineData("xbar://post/2111348825839611989", "post")]
    [InlineData("xbar://settings", "settings")]
    [InlineData("xbar://post/../../evil", "activate")]
    [InlineData("xbar://post/2111348825839611989?url=https://evil", "activate")]
    public void DeepLinksOnlyAcceptDefinedActions(string url, string action) => Assert.Equal(action, AppCommand.Parse([url]).Action);

    [Fact] public async Task WidgetCommandsAreAcknowledgedOnceAndUnknownActionsAreRejected()
    {
        using var cancellation = new CancellationTokenSource(); var received = new List<AppCommand>();
        var server = CommandPipe.ListenAsync(c => { received.Add(c); return Task.CompletedTask; }, cancellation.Token);
        try
        {
            Assert.True(await CommandPipe.SendAsync(new("period", "Week")));
            Assert.True(await CommandPipe.SendAsync(new("refresh")));
            Assert.False(await CommandPipe.SendAsync(new("execute", "malicious")));
            Assert.Equal(new[] { new AppCommand("period", "Week"), new AppCommand("refresh") }, received);
        }
        finally { cancellation.Cancel(); await server.WaitAsync(TimeSpan.FromSeconds(3)); }
    }
    private static Snapshot WithPosts() => Ready() with { PostsCurrent = true, PostsAt = Start,
        PostsScope = new("@fixture", ContentPeriod.Today, "2026-10-26", "Etc/UTC"), Posts = [new(Post(), null, false, Start)] };
    [Fact] public void RefreshRetainsVerifiedValuesButNeverCallsThemFresh()
    {
        var s = WithPosts() with { Status = CollectionStatus.Loading, IsCollecting = true, PostsCurrent = false };
        Assert.True(s.CanShowMetric(Start.AddSeconds(5), Zone)); Assert.True(s.CanShowPosts(Start.AddSeconds(5), Zone)); Assert.False(s.Fresh(Start, Zone));
        Assert.Contains("1,234", WidgetCard.Render(s, "Medium", Start, Zone));
        Assert.False(s.CanShowMetric(Start.AddMinutes(11), Zone)); Assert.False(s.CanShowPosts(Start.AddMinutes(11), Zone));
    }
    [Fact] public void RetainedPostsRequireExactAccountPeriodDayAndZone()
    {
        var s = WithPosts(); Assert.True(s.CanShowPosts(Start, Zone));
        foreach (var scope in new[] { s.PostsScope! with { Account = "@other" }, s.PostsScope! with { Period = ContentPeriod.Week }, s.PostsScope! with { DayKey = "2026-10-25" }, s.PostsScope! with { TimeZone = "Asia/Seoul" } })
            Assert.False((s with { PostsScope = scope }).CanShowPosts(Start, Zone));
        Assert.False((s with { PostsScope = null }).CanShowPosts(Start, Zone));
        Assert.False((s with { PostsAt = Start.AddSeconds(1) }).CanShowPosts(Start, Zone));
        Assert.False((s with { Status = CollectionStatus.NeedsLogin }).CanShowPosts(Start, Zone));
        Assert.False((s with { Settings = s.Settings with { Connected = false } }).CanShowMetric(Start, Zone));
    }
    [Theory] [InlineData("Small", 0)] [InlineData("Medium", 3)] [InlineData("Large", 5)]
    public void CardsRespectSizeAndKeepActions(string size, int count)
    {
        var s = WithPosts() with { Posts = Enumerable.Range(0, 5).Select(i => new RankedPost(Post((2111348825839611980L + i).ToString()), null, false, Start)).ToImmutableArray() };
        using var card = JsonDocument.Parse(WidgetCard.Render(s, size, Start, Zone));
        Assert.Equal(count, card.RootElement.GetProperty("body").EnumerateArray().Count(e => e.TryGetProperty("selectAction", out _)));
        Assert.Equal(2, card.RootElement.GetProperty("actions").GetArrayLength());
    }
    [Fact] public void LargeCardCombinesSimultaneousNoticesToLeaveRoomForActions()
    {
        var s = WithPosts() with { Status = CollectionStatus.Loading, IsCollecting = true, Surges = [new(Post(), 10, 10, Start)] };
        using var card = JsonDocument.Parse(WidgetCard.Render(s, "Large", Start, Zone));
        var notes = card.RootElement.GetProperty("body").EnumerateArray().Where(e => e.TryGetProperty("text", out var t) && t.GetString()!.Contains(L10n.T("surge", s.Settings.Language))).ToArray();
        Assert.Single(notes); Assert.Contains(L10n.Status(s, Start, Zone), notes[0].GetProperty("text").GetString());
        Assert.False(notes[0].GetProperty("wrap").GetBoolean());
        Assert.Equal(2, card.RootElement.GetProperty("actions").GetArrayLength());
    }
    [Fact] public void CardTimeUsesTheProvidedZone()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
        var s = Ready() with { Today = new("1,234", "2026-10-26", MetricRecord.ZoneId(zone), "@fixture", Start) };
        Assert.Contains("11:00", WidgetCard.Render(s, "Medium", Start, zone));
    }
    [Fact] public void PlacementKeepsNegativeAndMixedDpiMonitorCoordinates()
    {
        PixelBounds[] areas = [new(-2560, -500, 2560, 1400), new(0, 0, 3840, 2080)];
        foreach (var window in new[] { new PixelBounds(-2400, -450, 700, 880), new PixelBounds(2600, 1000, 700, 880) })
            Assert.Equal(window, WindowPlacement.EnsureVisible(window, areas));
        var moved = WindowPlacement.EnsureVisible(new(-2500, -400, 700, 880), [areas[1]]);
        Assert.Equal(new PixelBounds(24, 24, 700, 880), moved);
    }
    [Fact] public void PlacementRecoversUnreachableTitleAndOversizedWindow()
    {
        var moved = WindowPlacement.EnsureVisible(new(50, -400, 900, 1400), [new(0, 0, 800, 600)]);
        Assert.Equal(new PixelBounds(24, 24, 752, 552), moved);
        Assert.Equal(moved, WindowPlacement.EnsureVisible(moved, []));
    }
    [Fact] public void MalformedSnapshotAndNullSurgeFailClosed()
    {
        var root = Path.Combine(Path.GetTempPath(), "XBar-corrupt-" + Guid.NewGuid()); var store = new StateStore(root);
        try {
            Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "snapshot.json"), "{broken"); Assert.Null(store.LoadSnapshot().Today);
            store.Write("snapshot.json", Ready() with { Surges = [null!] }); Assert.Null(store.LoadSnapshot().Today);
            store.Write("snapshot.json", WithPosts() with { Posts = [new(Post(), -2, false, Start)] }); Assert.Null(store.LoadSnapshot().Today);
            store.Write("snapshot.json", WithPosts()); Assert.NotNull(store.LoadSnapshot().Today);
        } finally { Directory.Delete(root, true); }
    }

}
