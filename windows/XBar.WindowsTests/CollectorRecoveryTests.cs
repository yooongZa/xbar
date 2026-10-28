using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using XBar.App;
using XBar.Core;

namespace XBar.WindowsTests;
internal static class CollectorRecoveryTests
{
    private const string Account = "<button aria-label='계정 메뉴'>Test @fixture</button>";
    private const string Overview = "<button id='date' aria-label='Select date range' aria-haspopup='dialog'></button><script>const d=new Date();document.getElementById('date').textContent=`${d.getFullYear()}. ${d.getMonth()+1}. ${d.getDate()}.`;</script><button aria-haspopup='dialog'><div>노출수</div><span class='sr-only'>123</span></button>";
    private const string Post = "<main><div><a href='/i/account_analytics/content/2111348825839611989' aria-label='Fixture'>post</a><div dir='auto'>Fixture post</div><span><svg data-icon='icon-bar-chart'></svg>1000</span><span><svg data-icon='icon-heart-stroke'></svg>5</span><span><svg data-icon='icon-retweet-stroke'></svg>10</span></div></main>";
    private static FieldInfo Field(string name) => typeof(Collector).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void StopTimer(Collector collector) => ((DispatcherTimer)Field("timer").GetValue(collector)!).Stop();
    private static void Respond(BrowserSurface surface, Func<string, string> body)
    {
        var core = surface.View.CoreWebView2;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) => e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes("<!doctype html><meta charset='utf-8'><style>button{display:block}svg{width:12px;height:12px}</style>" + body(e.Request.Uri))), 200, "OK", "Content-Type: text/html; charset=utf-8");
    }
    private static async Task WaitFor(Func<bool> predicate)
    {
        var until = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!predicate() && DateTimeOffset.UtcNow < until) await Task.Delay(50);
        if (!predicate()) throw new TimeoutException("Collector recovery fixture did not settle");
    }
    public static async Task Run(Action<string, bool, string?> check)
    {
        await LogoutAsync(check);
        await IntervalsAsync(check);
    }
    private static async Task LogoutAsync(Action<string, bool, string?> check)
    {
        foreach (var file in new[] { "settings.json", "snapshot.json" }) foreach (bool retryLogout in new[] { true, false })
        {
            var store = new StateStore(Path.Combine(Path.GetTempPath(), "XBar-logout-recovery-" + Guid.NewGuid().ToString("N")));
            store.Write("settings.json", new AppSettings { Connected = true });
            bool online = false; BrowserSurface? browser = null;
            using var collector = new Collector(store, surface => {
                if (surface.Window.ShowInTaskbar) browser = surface;
                Respond(surface, _ => "<input type='password'>");
            }, () => online);
            StopTimer(collector);
            await collector.OpenBrowserAsync();
            var cookies = browser!.View.CoreWebView2.CookieManager;
            cookies.AddOrUpdateCookie(cookies.CreateCookie("xbar_recovery_fixture", "synthetic-session", ".x.com", "/"));
            online = true; collector.RequestRefresh(true);
            bool failed = false;
            using (var locked = new FileStream(Path.Combine(store.Root, file), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { await collector.DisconnectAsync(); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed = true; }
                await WaitFor(() => !collector.IsRunning);
            }
            online = false;
            string scenario = file + (retryLogout ? " / retry logout" : " / sign in directly");
            check("Failed logout retains cleanup intent: " + scenario, failed && !collector.Settings.Connected && collector.Settings.PendingSessionClear && collector.State.Status == CollectionStatus.Disconnected, null);
            if (file == "snapshot.json") check("Locked snapshot also exercises a faulted in-flight scan: " + scenario, ((Task?)Field("running").GetValue(collector))?.IsFaulted == true, null);
            if (retryLogout) await collector.DisconnectAsync();
            await collector.OpenBrowserAsync(login: true);
            check("Logout recovery opens sign-in without restarting: " + scenario, browser.UserVisible && collector.IsWatchingLogin && !collector.Settings.PendingSessionClear && !store.LoadSettings().Connected, null);
            check("Logout recovery clears the old synthetic session: " + scenario, !(await browser.View.CoreWebView2.CookieManager.GetCookiesAsync("https://x.com")).Any(c => c.Name == "xbar_recovery_fixture"), null);
        }
    }
    private static async Task IntervalsAsync(Action<string, bool, string?> check)
    {
        var store = new StateStore(Path.Combine(Path.GetTempPath(), "XBar-interval-recovery-" + Guid.NewGuid().ToString("N")));
        store.Write("settings.json", new AppSettings { Connected = true, RefreshMinutes = 30 });
        int overviewRequests = 0, contentRequests = 0; string contentAccount = "@fixture";
        using var collector = new Collector(store, surface => Respond(surface, uri => {
            bool content = new Uri(uri).AbsolutePath == "/i/account_analytics/content";
            if (content) { contentRequests++; return $"<button aria-label='계정 메뉴'>{contentAccount}</button>" + Post; }
            if (new Uri(uri).AbsolutePath == "/i/account_analytics") overviewRequests++;
            return Account + Overview;
        }), () => true);
        StopTimer(collector);
        collector.RequestRefresh(true); await WaitFor(() => !collector.IsRunning && collector.State.PostsCurrent);
        check("Refresh scheduling fixture starts with verified overview and posts", collector.State.Today?.Display == "123" && collector.State.Posts.Length == 1, null);
        void Age(int overviewSeconds, int postSeconds)
        {
            var now = DateTimeOffset.UtcNow;
            // Advance only persisted reading ages; the actual navigation and extraction run unchanged.
            typeof(Collector).GetProperty("State")!.SetValue(collector, collector.State with { Today = collector.State.Today! with { CapturedAt = now.AddSeconds(-overviewSeconds) }, PostsAt = now.AddSeconds(-postSeconds), Heartbeat = now });
        }
        foreach (bool showPosts in new[] { true, false })
        {
            collector.UpdateSettings(collector.Settings with { ShowPosts = showPosts }); Age(605, 605);
            var metric = collector.State.Today; int over = overviewRequests, posts = contentRequests;
            collector.CheckEnvironment(); await WaitFor(() => !collector.IsRunning);
            check("30-minute overview is retained during the 10-minute posts refresh; visible=" + showPosts, overviewRequests == over && contentRequests == posts + 1 && collector.State.Today == metric && collector.State.PostsCurrent, null);
        }
        foreach (int minutes in new[] { 5, 10, 30 })
        {
            collector.UpdateSettings(collector.Settings with { RefreshMinutes = minutes }); Age(minutes * 60 + 1, 60);
            var postTime = collector.State.PostsAt; int over = overviewRequests, posts = contentRequests;
            collector.CheckEnvironment(); await WaitFor(() => !collector.IsRunning);
            check($"{minutes}-minute overview refresh leaves fresh posts untouched", overviewRequests == over + 1 && contentRequests == posts && collector.State.PostsAt == postTime && collector.State.PostsCurrent, null);
        }
        collector.UpdateSettings(collector.Settings with { RefreshMinutes = 10 }); Age(601, 601);
        int oldOverview = overviewRequests, oldPosts = contentRequests;
        collector.CheckEnvironment(); await WaitFor(() => !collector.IsRunning);
        check("Both due readings are collected in one refresh", overviewRequests == oldOverview + 1 && contentRequests == oldPosts + 1 && collector.State.PostsCurrent, null);
        oldOverview = overviewRequests; oldPosts = contentRequests;
        collector.CheckEnvironment(); await WaitFor(() => !collector.IsRunning);
        check("Fresh readings cause no background navigation", overviewRequests == oldOverview && contentRequests == oldPosts, null);
        collector.RequestRefresh(true); await WaitFor(() => !collector.IsRunning);
        check("Manual refresh still reloads both fresh readings", overviewRequests == oldOverview + 1 && contentRequests == oldPosts + 1 && collector.State.PostsCurrent, null);
        collector.UpdateSettings(collector.Settings with { RefreshMinutes = 30 }); Age(605, 605);
        collector.CheckEnvironment(); collector.UpdateSettings(collector.Settings with { Period = ContentPeriod.Week });
        await WaitFor(() => !collector.IsRunning && collector.State.PostsCurrent && collector.State.PostsScope?.Period == ContentPeriod.Week);
        check("Period change cancels posts-only work and publishes the new scope", collector.State.PostsScope?.Period == ContentPeriod.Week, null);
        Age(605, 605); contentAccount = "@different"; oldOverview = overviewRequests;
        collector.CheckEnvironment(); await WaitFor(() => !collector.IsRunning);
        check("Posts-only refresh rejects an account switch and clears old data", overviewRequests == oldOverview && collector.State.Status == CollectionStatus.NeedsLogin && collector.State.Today is null && collector.State.Posts.Length == 0, null);
    }
}
