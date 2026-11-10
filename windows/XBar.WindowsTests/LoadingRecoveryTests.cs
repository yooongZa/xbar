using System.IO;
using System.Text;
using Microsoft.Web.WebView2.Core;
using XBar.App;
using XBar.Core;

namespace XBar.WindowsTests;

internal static class LoadingRecoveryTests
{
    private static async Task WaitFor(Func<bool> condition, int seconds = 60)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(seconds);
        while (!condition() && DateTimeOffset.UtcNow < deadline) await Task.Delay(100);
        if (!condition()) throw new TimeoutException("Loading recovery fixture did not settle");
    }

    public static async Task Run(Action<string, bool, string?> check)
    {
        foreach (var offline in new[] { false, true })
        {
            var label = offline ? "network interruption" : "45-second navigation timeout";
            var store = new StateStore(Path.Combine(Path.GetTempPath(), "XBar-loading-recovery-" + Guid.NewGuid().ToString("N")));
            store.Write("settings.json", new AppSettings { Connected = true });
            bool online = true, hold = true, requested = false;
            int readers = 0;
            BrowserSurface? firstReader = null, latestReader = null;
            CoreWebView2Deferral? deferred = null;
            using var collector = new Collector(store, surface => {
                readers++;
                firstReader ??= surface;
                latestReader = surface;
                var core = surface.View.CoreWebView2;
                if (readers == 1)
                {
                    var manager = core.CookieManager;
                    var cookie = manager.CreateCookie("xbar_loading_fixture", "preserved", ".x.com", "/");
                    cookie.Expires = DateTime.UtcNow.AddDays(1);
                    manager.AddOrUpdateCookie(cookie);
                }
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += (_, e) => {
                    if (hold && new Uri(e.Request.Uri).AbsolutePath == "/i/account_analytics")
                    {
                        requested = true;
                        deferred = e.GetDeferral();
                        return;
                    }
                    var body = "<button aria-label='Account menu'>@fixture</button>";
                    if (new Uri(e.Request.Uri).AbsolutePath == "/i/account_analytics/content")
                        body += "<main>No posts during the selected period.</main>";
                    else
                        body += """
                        <button id='date' aria-label='Select date range'></button>
                        <script>const d=new Date();document.getElementById('date').textContent=`${d.getFullYear()}. ${d.getMonth()+1}. ${d.getDate()}.`;</script>
                        <button aria-haspopup='dialog'>Impressions <span class='sr-only'>123</span></button>
                        """;
                    e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(
                        "<!doctype html><meta charset='utf-8'><meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; script-src 'unsafe-inline'\">" + body)),
                        200, "OK", "Content-Type: text/html; charset=utf-8");
                };
            }, () => online);
            try
            {
                collector.RequestRefresh(true);
                await WaitFor(() => requested, 20);
                check(label + ": real navigation is held before completion", collector.IsRunning && collector.State.Today is null, null);
                var before = await firstReader!.View.CoreWebView2.CookieManager.GetCookiesAsync("https://x.com");
                check(label + ": persistent login fixture is stored", before.Any(c => c.Name == "xbar_loading_fixture" && c.Value == "preserved" && !c.IsSession), null);
                if (offline) { online = false; collector.CheckEnvironment(); }
                await WaitFor(() => !collector.IsRunning);
                check(label + ": failed collection exits loading state", !collector.State.IsCollecting && collector.State.Status == (offline ? CollectionStatus.Offline : CollectionStatus.Error), null);
                hold = false;
                // A late completion from the retired controller must not publish.
                try { deferred?.Complete(); } catch (Exception) { /* The retired WebView may already be disposed. */ }
                deferred = null;
                if (offline) { online = true; collector.CheckEnvironment(); }
                await WaitFor(() => !collector.IsRunning && collector.State.PostsCurrent && collector.State.Status == CollectionStatus.Ready);
                check(label + ": automatic retry accepts current metrics", collector.State.Today is { Display: "123", Account: "@fixture" }, null);
                check(label + ": stalled reader is replaced", readers >= 3 && !ReferenceEquals(firstReader, latestReader), null);
                var cookies = await latestReader!.View.CoreWebView2.CookieManager.GetCookiesAsync("https://x.com");
                check(label + ": replacement preserves persistent login", cookies.Any(c => c.Name == "xbar_loading_fixture" && c.Value == "preserved" && !c.IsSession), null);
            }
            finally
            {
                try { deferred?.Complete(); } catch (Exception) { }
            }
        }
    }
}
