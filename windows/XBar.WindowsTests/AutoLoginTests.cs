using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using XBar.App;
using XBar.Core;

namespace XBar.WindowsTests;
internal static class AutoLoginTests
{
    private const string Account = "<button aria-label='계정 메뉴'>Test @fixture</button>";
    private const string Overview = "<button id='date' aria-label='Select date range' aria-haspopup='dialog'></button><script>const d=new Date();document.getElementById('date').textContent=`${d.getFullYear()}. ${d.getMonth()+1}. ${d.getDate()}.`;</script><button aria-haspopup='dialog'><div>노출수</div><span class='sr-only'>123</span></button>";
    private static void Respond(BrowserSurface surface, Func<string, string> body, Func<Task>? wait = null)
    {
        var core = surface.View.CoreWebView2;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += async (_, e) => {
            using var deferral = e.GetDeferral();
            if (wait is not null) await wait();
            e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes("<!doctype html><meta charset='utf-8'>" + body(e.Request.Uri))), 200, "OK", "Content-Type: text/html; charset=utf-8");
        };
    }
    private static async Task WaitFor(Func<bool> predicate)
    { var until = DateTimeOffset.UtcNow.AddSeconds(25); while (!predicate() && DateTimeOffset.UtcNow < until) await Task.Delay(100); }
    public static async Task Run(Action<string, bool, string?> check)
    {
        void Check(string name, bool value) => check(name, value, null);
        var root = Path.Combine(Path.GetTempPath(), "XBar-auto-login-" + Guid.NewGuid());
        using (var session = new WebSession(Path.Combine(root, "identity"))) {
            var surface = await session.CreateAsync(false, "X-Bar identity fixture");
            string html = Account; Respond(surface, _ => html); surface.SetLanguage("ko", true); surface.Show();
            async Task<BrowserSurface.LoginIdentity?> Read(string body, string path = "/home")
            { html = body; await surface.NavigateAsync("https://x.com" + path, CancellationToken.None); return await surface.ReadLoginIdentityAsync(CancellationToken.None); }
            Check("Signed-in account menu is detected without reading credentials", (await Read(Account))?.Account == "@fixture");
            foreach (var path in new[] { "/i/flow/login", "/i/jf/onboarding/web" })
                Check("Account behind login sheet requires independent session verification: " + path, (await Read(Account, path)) is { RequiresSessionCheck: true });
            foreach (var path in new[] { "/account/access", "/i/flow/two-factor-auth" })
                Check("Authentication route remains open: " + path, await Read(Account, path) is null);
            Check("Visible password field blocks automatic completion", await Read(Account + "<input type='password'>") is null);
            Check("OTP field blocks automatic completion", await Read(Account + "<input autocomplete='one-time-code'>") is null);
            Check("Authentication dialog blocks automatic completion", await Read(Account + "<div role='dialog'><input></div>") is null);
            Check("Conflicting account identities block completion", await Read(Account + "<a aria-label='Profile' href='/different'>profile</a>") is null);
            Check("Loading page without an account never completes login", await Read("<div role='progressbar'></div>") is null);
            await Read(Account + "<input id='name' autocomplete='username webauthn'><script>document.getElementById('name').focus()</script>", "/i/jf/onboarding/web");
            Check("Autofocused untouched username field allows independent session verification", (await surface.ReadLoginIdentityAsync(CancellationToken.None)) is { RequiresSessionCheck: true });
            await surface.View.CoreWebView2.ExecuteScriptAsync("const target=document.getElementById('name');target.dispatchEvent(new PointerEvent('pointerdown',{bubbles:true}));target.blur();");
            Check("User interaction with an untouched login field blocks automatic close after blur", await surface.ReadLoginIdentityAsync(CancellationToken.None) is null);
            await Read(Account + "<iframe id='auth' tabindex='0' srcdoc='<input>'></iframe>");
            await surface.View.CoreWebView2.ExecuteScriptAsync("document.getElementById('auth').focus();");
            Check("Focused authentication frame blocks automatic completion", await surface.ReadLoginIdentityAsync(CancellationToken.None) is null);
            await Read(Account + "<input id='name'>", "/i/flow/login");
            await surface.View.CoreWebView2.ExecuteScriptAsync("const el=document.getElementById('name');el.focus();el.dispatchEvent(new Event('input',{bubbles:true}));el.blur();");
            Check("Started sign-in behind a known account stays protected after blur", await surface.ReadLoginIdentityAsync(CancellationToken.None) is null);
            await surface.View.CoreWebView2.ExecuteScriptAsync("document.getElementById('name').focus();history.pushState({},'', '/home');");
            Check("Focused input remains protected after a same-document redirect", await surface.ReadLoginIdentityAsync(CancellationToken.None) is null);
            await surface.View.CoreWebView2.ExecuteScriptAsync("document.getElementById('name').remove();");
            Check("SPA login completion is detected after credentials leave the page", (await surface.ReadLoginIdentityAsync(CancellationToken.None))?.Account == "@fixture");
            Check("Interactive identity checks stay isolated from page scripts", await surface.View.CoreWebView2.ExecuteScriptAsync("typeof XBarPageContext + ':' + typeof TodayBarExtractor") == "\"undefined:undefined\"");
            await surface.NavigateAsync("https://appleid.apple.com/", CancellationToken.None);
            Check("External authentication hosts cannot complete X login", await surface.ReadLoginIdentityAsync(CancellationToken.None) is null);
        }
        var store = new StateStore(Path.Combine(root, "collector"));
        string page = "<input id='name' autocomplete='username'>", probeAccount = "@fixture"; bool online = true; BrowserSurface? browser = null; int windows = 0, navigations = 0, probes = 0;
        TaskCompletionSource? pendingProbe = null;
        using var collector = new Collector(store, surface => {
            if (surface.Window.ShowInTaskbar) { browser = surface; windows++; surface.View.CoreWebView2.NavigationStarting += (_, _) => navigations++; Respond(surface, _ => page); }
            else if (surface.Window.Title == "X-Bar login session check") { probes++; Respond(surface, _ => $"<button aria-label='계정 메뉴'>{probeAccount}</button>", () => pendingProbe?.Task ?? Task.CompletedTask); }
            else Respond(surface, uri => Account + (new Uri(uri).AbsolutePath.EndsWith("/content") ? "<main>No posts during the selected period.</main>" : Overview));
        }, () => online);
        var first = collector.OpenBrowserAsync(login: true); var duplicate = collector.OpenBrowserAsync(login: true);
        await Task.WhenAll(first, duplicate);
        Check("Repeated login clicks share one browser initialization", ReferenceEquals(first, duplicate) && windows == 1);
        var toolbar = SettingsTests.FindById<DockPanel>(browser!.Window, "account.loginToolbar")!;
        Check("Login window has automatic-close guidance and no completion button", toolbar.IsVisible && !toolbar.Children.OfType<Button>().Any());
        await browser.View.CoreWebView2.ExecuteScriptAsync("document.getElementById('name').focus();");
        int before = navigations; await collector.OpenBrowserAsync(login: true);
        Check("Repeated login action preserves the active authentication page", navigations == before && collector.IsWatchingLogin && !collector.Settings.Connected);
        page = Account; await browser.NavigateAsync("https://x.com/home", CancellationToken.None); online = false;
        await Task.Delay(2300);
        Check("Offline detection does not commit a login", !collector.Settings.Connected && browser.UserVisible);
        online = true; await WaitFor(() => collector.Settings.Connected && !browser.UserVisible);
        Check("Login closes automatically and persists without a button click", collector.Settings.Connected && store.LoadSettings().Connected && !browser.UserVisible && !collector.IsWatchingLogin);
        await WaitFor(() => collector.State.Status == CollectionStatus.Ready && !collector.IsRunning);
        Check("Automatic login starts normal data collection", collector.State.Today?.Account == "@fixture" && collector.State.Today.Display == "123");
        await collector.OpenBrowserAsync(); await Task.Delay(2300);
        Check("Ordinary analytics stays open after an automatic login", browser.UserVisible && !collector.IsWatchingLogin && !toolbar.IsVisible);
        browser.Window.Close(); await collector.DisconnectAsync();
        page = Account + "<input id='name' autocomplete='username webauthn'><script>document.getElementById('name').focus()</script>"; probeAccount = "@different";
        int mismatchProbes = probes;
        await collector.OpenBrowserAsync(login: true); await WaitFor(() => probes > mismatchProbes); await Task.Delay(1500);
        Check("Mismatched background session cannot complete login", !collector.Settings.Connected && browser!.UserVisible);
        await collector.DisconnectAsync();
        probeAccount = "@fixture"; int beforeProbe = probes; pendingProbe = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await collector.OpenBrowserAsync(login: true); await WaitFor(() => probes > beforeProbe);
        await browser!.View.CoreWebView2.ExecuteScriptAsync("const input=document.getElementById('name');input.focus();input.dispatchEvent(new Event('input',{bubbles:true}));input.blur();");
        pendingProbe.SetResult(); await Task.Delay(1800);
        Check("Typing during background session verification prevents automatic close", !collector.Settings.Connected && browser.UserVisible);
        await collector.DisconnectAsync(); pendingProbe = null; beforeProbe = probes;
        await collector.OpenBrowserAsync(login: true); await WaitFor(() => collector.Settings.Connected && !browser!.UserVisible);
        Check("Existing session behind X login sheet is confirmed without touching the form", collector.Settings.Connected && !browser!.UserVisible && probes > beforeProbe);
        await collector.DisconnectAsync();
        page = "<input autocomplete='one-time-code'>"; await collector.OpenBrowserAsync(login: true);
        page = Account; await browser!.NavigateAsync("https://x.com/home", CancellationToken.None);
        browser.Window.Close(); await Task.Delay(2300);
        Check("Closing login cancels detection without late account writes", !collector.IsWatchingLogin && !collector.Settings.Connected && !store.LoadSettings().Connected);
        await collector.OpenBrowserAsync(); await Task.Delay(2300);
        Check("Canceled detection cannot close a subsequently opened analytics window", browser.UserVisible && !collector.Settings.Connected);
        browser.Window.Close(); await collector.OpenBrowserAsync(login: true);
        await browser.NavigateAsync("https://x.com/home", CancellationToken.None); await Task.Delay(1100); await collector.DisconnectAsync(); await Task.Delay(1300);
        Check("Logout cancels pending detection and never restores the cleared session", !collector.IsWatchingLogin && !collector.Settings.Connected && store.LoadSnapshot().Status == CollectionStatus.Disconnected);
        await collector.OpenBrowserAsync(login: true); await browser!.NavigateAsync("https://x.com/home", CancellationToken.None);
        collector.Dispose(); await Task.Delay(1300);
        Check("Exit cancels pending detection and leaves disconnected settings", !store.LoadSettings().Connected && !collector.IsWatchingLogin);
    }
}
