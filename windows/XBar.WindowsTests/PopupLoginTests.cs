using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using XBar.App;

namespace XBar.WindowsTests;

internal static class PopupLoginTests
{
    private const string Parent = """
        <!doctype html><meta charset="utf-8"><button aria-label="계정 메뉴">Test @fixture</button><input id="draft" value="keep-me">
        <script>
        window.addEventListener('message', e => {
          if(e.origin !== 'https://appleid.apple.com') return;
          if(e.data === 'provider-ready') e.source.postMessage('begin', e.origin);
          if(e.data === 'complete') document.body.dataset.completed = 'yes';
        });
        </script>
        """;
    private const string Provider = """
        <!doctype html><meta charset="utf-8"><div id="status">waiting</div><script>
        window.addEventListener('message', e => {
          if(e.origin === 'https://x.com' && e.data === 'begin') document.getElementById('status').textContent='form-ready';
        });
        window.opener?.postMessage('provider-ready','https://x.com');
        </script>
        """;
    private static void Respond(BrowserSurface surface, string html)
    {
        var core = surface.View.CoreWebView2;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) => e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(html)), 200, "OK", "Content-Type: text/html; charset=utf-8");
    }
    private static async Task WaitFor(Func<bool> predicate)
    { var until = DateTimeOffset.UtcNow.AddSeconds(15); while (!predicate() && DateTimeOffset.UtcNow < until) await Task.Delay(25); }
    public static async Task Run(Action<string, bool, string?> check)
    {
        void Check(string name, bool value) => check(name, value, null);
        using var session = new WebSession(Path.Combine(Path.GetTempPath(), "XBar-popup-login-" + Guid.NewGuid()));
        var parent = await session.CreateAsync(false, "X-Bar popup fixture");
        Respond(parent, Parent);
        parent.PopupCreated += child => Respond(child, Provider);
        parent.SetLanguage("ko", true); parent.Show();
        await parent.NavigateAsync("https://x.com/i/flow/login", CancellationToken.None);
        var core = parent.View.CoreWebView2;
        var hint = SettingsTests.FindById<DockPanel>(parent.Window, "account.loginToolbar")!.Children.OfType<TextBlock>().Single();
        foreach (var language in new[] { "ko", "en", "ja" }) {
            parent.SetLanguage(language, true);
            await core.ExecuteScriptAsync("void window.open('https://accounts.google.com/o/oauth2/v2/auth','google-login')");
            await Task.Delay(150);
            Check("Google guidance keeps the X form and entered text: " + language, hint.Text == XBar.Core.L10n.T("login.google.unsupported", language) && parent.Popups.Count == 0 && parent.Url!.StartsWith("https://x.com/") && await core.ExecuteScriptAsync("document.getElementById('draft').value") == "\"keep-me\"");
        }
        parent.SetLanguage("ko", true);
        core.Navigate("https://accounts.google.com/gsi/select"); await Task.Delay(150);
        Check("Direct Google navigation gives the same guidance without leaving X", hint.Text == XBar.Core.L10n.T("login.google.unsupported", "ko") && parent.Url!.StartsWith("https://x.com/"));
        await core.ExecuteScriptAsync("void window.open('https://untrusted.example/signin','blocked')"); await Task.Delay(150);
        Check("Untrusted popup remains blocked", parent.Popups.Count == 0);
        var cookie = core.CookieManager.CreateCookie("popup_fixture", "shared-profile", ".x.com", "/");
        core.CookieManager.AddOrUpdateCookie(cookie);
        async Task<BrowserSurface> Open()
        {
            await core.ExecuteScriptAsync("window.auth=window.open('https://appleid.apple.com/auth/authorize?response_mode=web_message','apple-login');void 0");
            await WaitFor(() => parent.Popups.Count == 1);
            if (parent.Popups.Count != 1) throw new InvalidOperationException("Authentication popup did not open");
            var child = parent.Popups[0];
            var until = DateTimeOffset.UtcNow.AddSeconds(10);
            while (await child.View.CoreWebView2.ExecuteScriptAsync("document.getElementById('status')?.textContent") != "\"form-ready\"" && DateTimeOffset.UtcNow < until) await Task.Delay(50);
            return child;
        }
        var popup = await Open();
        Check("Apple popup preserves opener and the provider handshake", await popup.View.CoreWebView2.ExecuteScriptAsync("!!window.opener && document.getElementById('status')?.textContent==='form-ready'") == "true" && parent.Url == "https://x.com/i/flow/login");
        Check("Popup shares only the app's profile", (await popup.View.CoreWebView2.CookieManager.GetCookiesAsync("https://x.com")).Any(c => c.Name == "popup_fixture" && c.Value == "shared-profile"));
        Check("Visible provider popup prevents premature X login completion", await parent.ReadLoginIdentityAsync(CancellationToken.None) is null);
        await popup.View.CoreWebView2.ExecuteScriptAsync("window.opener.postMessage('complete','https://x.com');window.close()");
        await WaitFor(() => parent.Popups.Count == 0);
        Check("Provider callback reaches original X window and closes its popup", parent.Popups.Count == 0 && parent.UserVisible && await core.ExecuteScriptAsync("document.body.dataset.completed") == "\"yes\"");
        popup = await Open(); popup.Window.Close(); await WaitFor(() => parent.Popups.Count == 0);
        Check("Canceling provider popup keeps original X login available", parent.Popups.Count == 0 && parent.UserVisible);
        popup = await Open(); parent.SetLanguage("ko", false);
        Check("Switching out of login closes provider windows", parent.Popups.Count == 0 && !popup.UserVisible);
        parent.SetLanguage("ko", true);
        var opening = core.ExecuteScriptAsync("void window.open('https://appleid.apple.com/auth/authorize','apple-login')");
        await WaitFor(() => parent.HasPendingPopup || parent.Popups.Count > 0); parent.Window.Close(); await opening;
        await WaitFor(() => !parent.HasPendingPopup && parent.Popups.Count == 0);
        Check("Closing X login during popup creation leaves no orphan window", !parent.UserVisible && !parent.HasPendingPopup && parent.Popups.Count == 0);
        parent.Show(); popup = await Open();
        await session.ClearAsync();
        Check("Session cleanup closes parent and provider windows", !parent.UserVisible && !popup.UserVisible);
    }
}
