using System.IO;
using System.Windows.Input;
using System.Windows.Controls;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using XBar.App;
using XBar.Core;

namespace XBar.WindowsTests;
internal static class Program
{
    private static readonly List<object> results = [];
    private static int failures;
    [STAThread] private static void Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            var output = Path.GetFullPath(args.FirstOrDefault() ?? "windows-results.json");
            try { if (args.Contains("--lab-cookie-write") || args.Contains("--lab-cookie-read")) await LabCookieAsync(args.Contains("--lab-cookie-write")); else if (args.Contains("--calendar-only")) await CalendarTests.Run(Check); else if (args.Contains("--desktop-only")) await DesktopTests.Run(Check); else if (args.Contains("--recovery-only")) await CollectorRecoveryTests.Run(Check); else if (args.Contains("--login-only")) await AutoLoginTests.Run(Check); else if (args.Contains("--browser-only")) await BrowserVisibilityAsync(); else if (args.Contains("--updates-only")) await UpdaterTests.Run(Check); else if (args.Contains("--settings-only")) await SettingsTests.Run(Check, Path.GetDirectoryName(output)!); else if (args.Contains("--layout-only")) RunLayout(Path.GetDirectoryName(output)!); else await RunAsync(Path.GetDirectoryName(output)!); }
            catch (Exception e) { Check("Harness completed", false, e.ToString()); }
            Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Windows integration: {results.Count - failures} passed, {failures} failed"); app.Shutdown(failures == 0 ? 0 : 1);
        };
        app.Run();
    }
    private static void Check(string name, bool passed, string? detail = null)
    { results.Add(new { name, passed, detail }); if (!passed) failures++; Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}"); }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr handle, out NativeRect rect);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    private static bool NativeWebHostVisible(BrowserSurface browser)
    { return IsWindowVisible(browser.View.Handle) && GetClientRect(browser.View.Handle, out var bounds) && bounds.Right - bounds.Left > 800 && bounds.Bottom - bounds.Top > 500; }
    private static async Task BrowserVisibilityAsync()
    {
        using var session = new WebSession(Path.Combine(Path.GetTempPath(), "XBar-browser-" + Guid.NewGuid()));
        var browser = await session.CreateAsync(false, "X-Bar browser visibility fixture");
        var core = browser.View.CoreWebView2;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) => e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes("<!doctype html><title>Browser fixture</title><h1>Browser fixture</h1>")), 200, "OK", "Content-Type: text/html");
        browser.SetLanguage("ko", false); browser.Show(); await browser.NavigateAsync("https://x.com/i/account_analytics", CancellationToken.None);
        Check("Analytics native web host is visible on first show without resize", NativeWebHostVisible(browser) && !SettingsTests.FindById<DockPanel>(browser.Window, "account.loginToolbar")!.IsVisible);
        browser.Window.Close(); browser.SetLanguage("ko", true); browser.Show();
        Check("Sign-in native web host and automatic-close hint are visible", NativeWebHostVisible(browser) && SettingsTests.FindById<DockPanel>(browser.Window, "account.loginToolbar")!.IsVisible && !SettingsTests.FindById<DockPanel>(browser.Window, "account.loginToolbar")!.Children.OfType<Button>().Any());
        browser.Window.Close(); browser.SetLanguage("ko", false); browser.Show();
        Check("Reopening analytics keeps web host visible and hides login toolbar", NativeWebHostVisible(browser) && !SettingsTests.FindById<DockPanel>(browser.Window, "account.loginToolbar")!.IsVisible);
        browser.Window.Close();
    }
    private static async Task LabCookieAsync(bool write)
    {
        // This helper accesses only the isolated updater lab profile, never a real account.
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XBar.UpdaterLab");
        using var session = new WebSession(root); var surface = await session.CreateAsync(true, "X-Bar updater cookie fixture");
        var manager = surface.View.CoreWebView2.CookieManager;
        if (write) { var cookie = manager.CreateCookie("xbar_updater_fixture", "synthetic-session", ".x.com", "/"); cookie.Expires = DateTime.UtcNow.AddDays(1); manager.AddOrUpdateCookie(cookie); }
        Check("Isolated synthetic cookie survives " + (write ? "write" : "installer update"), (await manager.GetCookiesAsync("https://x.com")).Any(c => c.Name == "xbar_updater_fixture" && c.Value == "synthetic-session"));
    }
    private static void RunLayout(string output)
    {
        using var collector = new Collector(new StateStore(Path.Combine(Path.GetTempPath(), "XBar-layout-" + Guid.NewGuid())));
        collector.UpdateSettings(collector.Settings with { ShowPosts = false });
        var widget = new DesktopWidget(collector, _ => Task.CompletedTask) { AllowClose = true }; widget.Show();
        foreach (var mode in new[] { "light", "dark" }) {
            Theme.TestMode = mode; Theme.Apply(); collector.UpdateSettings(collector.Settings with { Connected = false }); widget.Refresh(); widget.UpdateLayout();
            Check($"{mode} compact disconnected keeps connect action inside", widget.Height == 260 && widget.ConnectControl.IsVisible && widget.ConnectControl.TranslatePoint(new Point(0, widget.ConnectControl.ActualHeight), widget).Y < widget.ActualHeight);
            collector.UpdateSettings(collector.Settings with { Connected = true }); widget.Refresh(); widget.UpdateLayout();
            Check($"{mode} compact connected returns to summary height", widget.Height == 210 && !widget.ConnectControl.IsVisible);
        }
        // CI packaging preview: render this build's disconnected UI without WebView2 or an X account.
        collector.UpdateSettings(collector.Settings with { Connected = false, Language = "en" }); widget.Refresh();
        Directory.CreateDirectory(output); Capture(widget, Path.Combine(output, "widget-preview.png"));
        widget.Close(); Theme.TestMode = null;
    }
    private static void Capture(Window window, string fileName)
    {
        window.UpdateLayout(); var image = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32); image.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(fileName); png.Save(file);
    }
    private static async Task RunAsync(string output)
    {
        await CalendarTests.Run(Check);
        var root = Path.Combine(Path.GetTempPath(), "XBar-web-test-" + Guid.NewGuid().ToString("N"));
        using var session = new WebSession(root);
        var surface = await session.CreateAsync(true, "X-Bar offline test");
        string body = "";
        var core = surface.View.CoreWebView2;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) => {
            var html = "<!doctype html><meta charset='utf-8'><meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'\"><style>button{display:block}.sr-only{position:absolute;width:1px;height:1px;overflow:hidden}</style>" + body;
            e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(html)), 200, "OK", "Content-Type: text/html; charset=utf-8");
        };
        const string account = "<button aria-label='계정 메뉴'>Test @fixture</button>";
        string Card(string value, string label = "노출수") => $"<button aria-haspopup='dialog'><div>{label}</div><span class='sr-only'>{value}</span><span aria-hidden='true'>0{value}</span><span class='sr-only'>-75%</span></button>";
        const string date = "<button id='date' aria-label='Select date range' aria-haspopup='dialog'></button><script>const d=new Date();document.getElementById('date').textContent=`${d.getFullYear()}. ${d.getMonth()+1}. ${d.getDate()}.`;</script>";
        async Task<PageReading?> Read(string html, string url = "https://x.com/i/account_analytics")
        { body = html; await surface.NavigateAsync(url, CancellationToken.None); return await surface.ReadAsync<PageReading>("globalThis.TodayBarExtractor?.snapshot()", CancellationToken.None); }
        var reading = await Read(account + date + Card("16.3K"));
        Check("Hidden WebView2 reads accessible count", reading is { Status: "ready", Display: "16.3K", Account: "@fixture" }, JsonSerializer.Serialize(reading));
        Check("Extractor is isolated from page scripts", await core.ExecuteScriptAsync("typeof TodayBarExtractor") == "\"undefined\"");
        Check("Local JS timezone matches .NET", reading?.TimeZone == MetricRecord.ZoneId(TimeZoneInfo.Local));
        reading = await Read(account + date + Card("12,345", "Impressions")); Check("English metric", reading is { Status: "ready", Display: "12,345" });
        reading = await Read(account + date + Card("1.2万", "インプレッション")); Check("Japanese metric", reading is { Status: "ready", Display: "1.2万" });
        reading = await Read(account + date + Card("0")); Check("Zero count", reading is { Status: "ready", Display: "0" });
        reading = await Read(account + date + Card("-75%")); Check("Percent is not count", reading?.Status == "unavailable");
        reading = await Read(account + date + Card("1K") + Card("2K")); Check("Ambiguous metric rejected", reading?.Status == "unavailable");
        reading = await Read(account + date + Card("1K") + "<div role='progressbar'></div>"); Check("Loading metric rejected", reading?.Status == "loading");
        reading = await Read(account + date + Card("1K") + "<script>document.getElementById('date').textContent='2020. 1. 1.';</script>"); Check("Old date rejected", reading?.Status == "wrong_day");
        reading = await Read(account + "<input autocomplete='one-time-code'>", "https://x.com/home"); Check("OTP blocks recovery", reading is { Status: "login_required", CanRecover: false });
        reading = await Read(account, "https://x.com/home"); Check("Verified home permits recovery", reading is { Status: "off_page", CanRecover: true });
        reading = await Read(account + "<input id='username'>", "https://x.com/i/flow/login");
        await core.ExecuteScriptAsync("const el=document.getElementById('username');el.focus();el.dispatchEvent(new Event('input',{bubbles:true}));el.blur();");
        reading = await surface.ReadAsync<PageReading>("TodayBarExtractor.snapshot()", CancellationToken.None); Check("Edited login remains protected after blur", reading is { Status: "login_required", CanRecover: false });
        var range = DateRange.For(ContentPeriod.Today, DateTimeOffset.UtcNow, TimeZoneInfo.Local);
        body = account + "<main><div><a href='/i/account_analytics/content/2111348825839611989' aria-label='Fixture'>post</a><div dir='auto'>Fixture title</div><span><svg data-icon='icon-bar-chart'></svg>1,000</span><span><svg data-icon='icon-heart-stroke'></svg>5</span><span><svg data-icon='icon-retweet-stroke'></svg>10</span></div></main>";
        await surface.NavigateAsync(range.Url, CancellationToken.None);
        var content = await surface.ReadAsync<ContentReading>("TodayBarContentExtractor.snapshot()", CancellationToken.None);
        Check("Content DOM extraction and scope", content?.ValidFor("@fixture", range, range.To) == true && content.Posts![0].Impressions == "1,000", JsonSerializer.Serialize(content));
        body = body.Replace("icon-heart-stroke", "icon-heart").Replace("icon-retweet-stroke", "icon-repost");
        await surface.NavigateAsync(range.Url, CancellationToken.None);
        content = await surface.ReadAsync<ContentReading>("TodayBarContentExtractor.snapshot()", CancellationToken.None);
        Check("Windows content icons preserve metric identity", content?.Posts is [{ Impressions: "1,000", Likes: "5", Reposts: "10" }]);
        body = body.Replace("</main>", "<span><svg data-icon='icon-heart'></svg>999</span></main>")
            .Replace("<span><svg data-icon='icon-heart'></svg>5</span>", "<span><svg data-icon='icon-heart'></svg>5</span><span><svg data-icon='icon-heart-stroke'></svg>6</span>");
        await surface.NavigateAsync(range.Url, CancellationToken.None);
        content = await surface.ReadAsync<ContentReading>("TodayBarContentExtractor.snapshot()", CancellationToken.None);
        Check("Conflicting icon variants are rejected", content?.Status == "unrecognized");
        body = account + "<main>No posts during the selected period.</main>"; await surface.NavigateAsync(range.Url, CancellationToken.None);
        content = await surface.ReadAsync<ContentReading>("TodayBarContentExtractor.snapshot()", CancellationToken.None);
        Check("Empty list stays distinct from error", content is { Status: "empty", Posts.Length: 0 });
        surface.OperationTimeout = TimeSpan.FromMilliseconds(300);
        var clock = Stopwatch.StartNew(); bool timedOut = false;
        try { await surface.ReadAsync<JsonElement>("new Promise(() => {})", CancellationToken.None); } catch (TimeoutException) { timedOut = true; }
        Check("Stalled JS promise has bounded read timeout", timedOut && clock.Elapsed < TimeSpan.FromSeconds(3));
        surface.OperationTimeout = TimeSpan.FromSeconds(8);
        using (var cancellation = new CancellationTokenSource(200)) {
            clock.Restart(); bool canceled = false;
            try { await surface.ReadAsync<JsonElement>("new Promise(() => {})", cancellation.Token); } catch (OperationCanceledException) { canceled = true; }
            Check("Pending JS read cancels promptly", canceled && clock.Elapsed < TimeSpan.FromSeconds(3));
        }
        var cookie = core.CookieManager.CreateCookie("xbar_fixture", "test", ".x.com", "/"); core.CookieManager.AddOrUpdateCookie(cookie);
        Check("Test session cookie exists", (await core.CookieManager.GetCookiesAsync("https://x.com")).Any(c => c.Name == "xbar_fixture"));
        await session.ClearAsync();
        var clean = await session.CreateAsync(true, "X-Bar cleanup test");
        Check("Disconnect clears shared profile", !(await clean.View.CoreWebView2.CookieManager.GetCookiesAsync("https://x.com")).Any(c => c.Name == "xbar_fixture"));
        using var collector = new Collector(new StateStore(Path.Combine(root, "ui")));
        var widget = new DesktopWidget(collector, _ => Task.CompletedTask) { AllowClose = true };
        widget.Show(); widget.UpdateLayout();
        Check("Desktop widget renders", widget.ActualWidth >= 300 && widget.ActualHeight >= 210);
        foreach (var mode in Enum.GetValues<DisplayMode>())
        { collector.UpdateSettings(collector.Settings with { Mode = mode }); Check("Mode persists: " + mode, new StateStore(Path.Combine(root, "ui")).LoadSettings().Mode == mode); }
        foreach (var lang in new[] { "ko", "en", "ja" })
        { collector.UpdateSettings(collector.Settings with { Language = lang }); widget.Refresh(); widget.UpdateLayout(); Check("Desktop language: " + lang, widget.ActualWidth >= 300); }
        collector.UpdateSettings(collector.Settings with { Language = "en" }); widget.Refresh(); widget.UpdateLayout();
        Directory.CreateDirectory(output);
        var bitmap = new RenderTargetBitmap((int)widget.ActualWidth, (int)widget.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(widget);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(Path.Combine(output, "widget-preview.png"))) encoder.Save(file);
        widget.Close();
        int overviewRequests = 0;
        string postAccount = "@fixture"; bool online = true; BrowserSurface? lastContent = null, accountBrowser = null;
        var collectionRoot = Path.Combine(root, "collector");
        using var live = new Collector(new StateStore(collectionRoot), s => {
            if (s.Window.ShowInTaskbar) accountBrowser = s;
            var browser = s.View.CoreWebView2;
            browser.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            browser.WebResourceRequested += (_, e) => {
                var isContent = new Uri(e.Request.Uri).AbsolutePath == "/i/account_analytics/content";
                if (e.ResourceContext == CoreWebView2WebResourceContext.Document && new Uri(e.Request.Uri).AbsolutePath == "/i/account_analytics") overviewRequests++;
                if (isContent) lastContent = s;
                var fixturePosts = string.Concat(Enumerable.Range(0, 5).Select(i => $"<div><a href='/i/account_analytics/content/{2111348825839611989L - i}' aria-label='Fixture {i}'>post</a><div dir='auto'>긴 게시글 제목과 줄임 표시 · Long post title · 長い投稿のタイトル {i}</div><span><svg data-icon='icon-bar-chart'></svg>{1000 - i * 10}</span><span><svg data-icon='icon-heart-stroke'></svg>5</span><span><svg data-icon='icon-retweet-stroke'></svg>10</span></div>"));
                var html = isContent ? $"<button aria-label='계정 메뉴'>{postAccount}</button><main>{fixturePosts}</main>" : account + date + Card("5,432");
                html = "<!doctype html><meta charset='utf-8'><style>button{display:block}svg{width:12px;height:12px}</style>" + html;
                e.Response = browser.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(html)), 200, "OK", "Content-Type: text/html; charset=utf-8");
            };
        }, () => online);
        var collectionErrors = new List<string>(); live.Failed += e => collectionErrors.Add(e.ToString());
        async Task WaitFor(Func<bool> predicate, int seconds = 25)
        { var until = DateTimeOffset.UtcNow.AddSeconds(seconds); while (!predicate() && DateTimeOffset.UtcNow < until) await Task.Delay(100); }
        live.UpdateSettings(live.Settings with { Connected = true, Mode = DisplayMode.Both });
        live.RequestRefresh(true); live.RequestRefresh(true); live.RequestRefresh(true);
        await WaitFor(() => live.State.PostsCurrent);
        Check("Collector verifies overview and posts", live.State is { Status: CollectionStatus.Ready, PostsCurrent: true, Today.Display: "5,432", Posts.Length: 5 });
        Check("Two displays coalesce concurrent refresh requests", overviewRequests == 1, overviewRequests.ToString());
        var verified = live.State;
        await live.OpenBrowserAsync(login: true);
        Check("Stale login command opens analytics without invalidating signed-in data", accountBrowser?.Url == "https://x.com/i/account_analytics" && live.State.Generation == verified.Generation && live.State.Today == verified.Today && live.State.PostsCurrent && live.Settings.Connected);
        Check("Signed-in browser hides the whole login toolbar", !SettingsTests.FindById<DockPanel>(accountBrowser!.Window, "account.loginToolbar")!.IsVisible);
        accountBrowser.Window.Close();
        var liveWidget = new DesktopWidget(live, _ => Task.CompletedTask) { AllowClose = true, Height = 330 };
        live.Changed += liveWidget.Refresh; liveWidget.Show(); liveWidget.Activate(); liveWidget.UpdateLayout();
        Check("Signed-in widget menu offers analytics without a login action", liveWidget.BuildMenu().Items.OfType<MenuItem>().Any(m => m.Header.ToString() == L10n.T("analysis", live.Settings.Language)) && !liveWidget.BuildMenu().Items.OfType<MenuItem>().Any(m => m.Header.ToString() == L10n.T("login", live.Settings.Language)));
        var firstRow = liveWidget.PostControls.First(); firstRow.Focus(); liveWidget.PostsScroller.ScrollToVerticalOffset(24); liveWidget.UpdateLayout();
        var oldOffset = liveWidget.PostsScroller.VerticalOffset;
        live.RequestRefresh(true); liveWidget.Refresh(); liveWidget.UpdateLayout();
        Check("Same-scope refresh keeps verified values", live.State.CanShowPosts(DateTimeOffset.UtcNow, TimeZoneInfo.Local) && live.State.IsCollecting);
        Check("Refresh retains row identity keyboard focus and scroll", ReferenceEquals(firstRow, liveWidget.PostControls.First()) && firstRow.IsKeyboardFocused && Math.Abs(liveWidget.PostsScroller.VerticalOffset - oldOffset) < 1);
        await WaitFor(() => !live.IsRunning);
        Check("Completed refresh preserves selection", firstRow.IsKeyboardFocused && ReferenceEquals(firstRow, liveWidget.PostControls.First()));
        liveWidget.PostsScroller.ScrollToHome();
        foreach (var mode in new[] { "light", "dark" }) {
            Theme.TestMode = mode; Theme.Apply();
            foreach (var lang in new[] { "ko", "en", "ja" }) {
                live.UpdateSettings(live.Settings with { Language = lang }); liveWidget.Height = 440; liveWidget.Refresh(); liveWidget.UpdateLayout();
                Check($"Fluent {mode} {lang} layout and named controls", liveWidget.RefreshControl.IsVisible && liveWidget.PostControls.Count == 5 && liveWidget.PeriodControls.All(b => b.ActualWidth >= 70 && !string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(b))));
                Capture(liveWidget, Path.Combine(output, $"desktop-{mode}-{lang}.png"));
                var settings = new SettingsWindow(live, new StateStore(collectionRoot), _ => Task.CompletedTask); settings.Show(); settings.UpdateLayout();
                Capture(settings, Path.Combine(output, $"settings-{mode}-{lang}.png")); settings.Close();
            }
        }
        Theme.TestHighContrast = true; Theme.Apply();
        Check("High contrast selected text uses system contrast pair", ((SolidColorBrush)Application.Current.Resources["XBar.Selected"]).Color == SystemColors.HighlightColor && ((SolidColorBrush)Application.Current.Resources["XBar.OnSelected"]).Color == SystemColors.HighlightTextColor);
        Check("Widget uses stable software rendering target", System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(liveWidget).Handle).CompositionTarget.RenderMode == System.Windows.Interop.RenderMode.SoftwareOnly);
        Theme.TestHighContrast = null;
        Theme.TestMode = "light"; Theme.Apply(); live.UpdateSettings(live.Settings with { Language = "ko" });
        foreach (var scale in new[] { 1.0, 1.5, 2.0 }) {
            var contentElement = (FrameworkElement)liveWidget.Content; contentElement.LayoutTransform = new ScaleTransform(scale, scale); liveWidget.Width = 350 * scale; liveWidget.Height = 440 * scale; liveWidget.UpdateLayout();
            Check($"Scaled layout {scale:P0} keeps controls inside window", liveWidget.RefreshControl.TranslatePoint(new Point(0, 0), liveWidget).X >= 0 && liveWidget.RefreshControl.TranslatePoint(new Point(liveWidget.RefreshControl.ActualWidth, 0), liveWidget).X <= liveWidget.ActualWidth && liveWidget.PeriodControls.All(b => b.ActualWidth >= 70));
            Capture(liveWidget, Path.Combine(output, $"desktop-scale-{scale:0.0}.png"));
        }
        ((FrameworkElement)liveWidget.Content).LayoutTransform = Transform.Identity; liveWidget.Width = 350; liveWidget.Height = 440;
        live.UpdateSettings(live.Settings with { ShowPosts = false }); liveWidget.Refresh(); liveWidget.UpdateLayout(); Check("Compact mode hides list and preserves height", liveWidget.Height == 210);
        live.UpdateSettings(live.Settings with { ShowPosts = true }); liveWidget.Refresh(); liveWidget.UpdateLayout(); Check("Expanded mode restores list", liveWidget.PostsScroller.IsVisible && liveWidget.Height >= 330);
        Capture(liveWidget, Path.Combine(output, "widget-preview.png"));
        live.Changed -= liveWidget.Refresh; liveWidget.Close(); Theme.TestMode = null; Theme.Apply();
        live.RequestRefresh(true); live.UpdateSettings(live.Settings with { Period = ContentPeriod.Week });
        await WaitFor(() => live.State.PostsCurrent && live.State.Settings.Period == ContentPeriod.Week);
        Check("Period switch rejects canceled read and recovers", live.State.PostsCurrent && live.State.Settings.Period == ContentPeriod.Week && live.State.Posts.All(p => p.Increase is null), JsonSerializer.Serialize(new { live.State, errors = collectionErrors }, StateStore.Json));
        live.RequestRefresh(true); live.UpdateSettings(live.Settings with { Period = ContentPeriod.Today }); live.UpdateSettings(live.Settings with { Period = ContentPeriod.Week }); live.UpdateSettings(live.Settings with { Period = ContentPeriod.TwoDays });
        await WaitFor(() => !live.IsRunning && live.State.PostsCurrent);
        Check("Rapid switches settle on latest scope", live.State.PostsCurrent && live.State.PostsScope?.Period == ContentPeriod.TwoDays);
        live.ScanDeadline = TimeSpan.FromMilliseconds(400); live.RequestRefresh(true); await WaitFor(() => !live.IsRunning);
        Check("Collection deadline releases readers", live.State.Status == CollectionStatus.Error && !live.State.IsCollecting);
        live.ScanDeadline = TimeSpan.FromSeconds(100); await WaitFor(() => live.State.PostsCurrent && !live.IsRunning, 40);
        Check("Collection automatically retries after deadline", live.State.PostsCurrent && live.State.Status == CollectionStatus.Ready);
        live.RequestRefresh(true); online = false; live.CheckEnvironment(); await WaitFor(() => !live.IsRunning);
        Check("Offline invalidates in-flight publication", live.State.Status == CollectionStatus.Offline && !live.State.PostsCurrent);
        online = true; live.CheckEnvironment(); await WaitFor(() => live.State.PostsCurrent && !live.IsRunning);
        Check("Network restoration refreshes automatically", live.State.PostsCurrent && live.State.Status == CollectionStatus.Ready);
        live.RequestRefresh(true); live.SleepChanged(true); await WaitFor(() => !live.IsRunning);
        Check("Suspend cancels collection", live.State.Status == CollectionStatus.Sleeping && !live.IsRunning && !live.State.PostsCurrent);
        live.SleepChanged(false); await WaitFor(() => live.State.PostsCurrent && !live.IsRunning);
        Check("Resume refreshes and resets growth baseline", live.State.PostsCurrent && live.State.Posts.All(p => p.Increase is null));
        if (lastContent is not null) {
            try { await lastContent.View.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.crash", "{}").WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
            await WaitFor(() => lastContent.Failed); Check("Renderer failure is detected", lastContent.Failed);
            live.RequestRefresh(true); await WaitFor(() => !live.IsRunning); live.RequestRefresh(true); await WaitFor(() => !live.IsRunning && live.State.PostsCurrent);
            Check("Failed renderer is recreated without losing session", live.State.PostsCurrent && live.State.Today?.Account == "@fixture");
        }
        postAccount = "@different"; live.RequestRefresh(true);
        await WaitFor(() => live.State.Status == CollectionStatus.NeedsLogin);
        Check("Cross-page account mismatch clears metrics", live.State.Status == CollectionStatus.NeedsLogin && live.State.Today is null && live.State.Posts.Length == 0);
        var expiredSettings = new SettingsWindow(live, new StateStore(collectionRoot), _ => Task.CompletedTask); expiredSettings.Show(); expiredSettings.Navigation[1].IsChecked = true; expiredSettings.UpdateLayout();
        Check("Expired account restores sign-in action", SettingsTests.FindById<Button>(expiredSettings, "account.signin")!.IsVisible && !SettingsTests.FindById<Button>(expiredSettings, "account.analysis")!.IsVisible);
        expiredSettings.Close();
        await live.OpenBrowserAsync(login: true);
        Check("Expired session opens sign-in with automatic detection", accountBrowser!.Url!.Contains("/onboarding/web") && SettingsTests.FindById<DockPanel>(accountBrowser.Window, "account.loginToolbar")!.IsVisible && live.IsWatchingLogin);
        accountBrowser.Window.Close();
        live.RequestRefresh(true); await live.DisconnectAsync();
        var persisted = new StateStore(collectionRoot).LoadSnapshot();
        Check("Logout cancels in-flight work and persists empty state", persisted.Status == CollectionStatus.Disconnected && persisted.Today is null && persisted.Posts.Length == 0 && !live.Settings.PendingSessionClear);
        await live.OpenBrowserAsync(login: true);
        Check("First sign-in waits automatically without a completion button", accountBrowser!.Url!.Contains("/onboarding/web") && SettingsTests.FindById<DockPanel>(accountBrowser.Window, "account.loginToolbar")!.IsVisible && !SettingsTests.FindById<DockPanel>(accountBrowser.Window, "account.loginToolbar")!.Children.OfType<Button>().Any() && !live.Settings.Connected);
        Capture(accountBrowser.Window, Path.Combine(output, "login-prompt-fixture.png"));
        postAccount = "@fixture";
        await accountBrowser.NavigateAsync("https://x.com/home", CancellationToken.None);
        await WaitFor(() => live.State.PostsCurrent && !live.IsRunning);
        Check("Login automatically closes the window and refreshes verified data", live.Settings.Connected && live.State.Status == CollectionStatus.Ready && live.State.PostsCurrent && !accountBrowser.Window.IsVisible);
        await live.DisconnectAsync();
        live.UpdateSettings(live.Settings with { Connected = true }); live.RequestRefresh(true); live.Dispose(); await Task.Delay(1500);
        persisted = new StateStore(collectionRoot).LoadSnapshot(); Check("Exit during initialization never republishes late results", persisted.Status == CollectionStatus.Stopped && !persisted.IsCollecting && persisted.Posts.Length == 0);
    }
}
