using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using XBar.Core;

namespace XBar.App;

internal sealed class WebSession(string root) : IDisposable
{
    private Task<CoreWebView2Environment>? environment;
    private bool disposed;
    private readonly List<BrowserSurface> surfaces = [];
    public async Task<BrowserSurface> CreateAsync(bool reader, string title)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        CoreWebView2Environment env;
        try { env = await (environment ??= CoreWebView2Environment.CreateAsync(null, Path.Combine(root, "WebView2"))).WaitAsync(TimeSpan.FromSeconds(45)); }
        catch { environment = null; throw; }
        ObjectDisposedException.ThrowIf(disposed, this);
        var surface = new BrowserSurface(reader, title);
        surfaces.Add(surface);
        try { await surface.InitializeAsync(env).WaitAsync(TimeSpan.FromSeconds(45)); return surface; }
        catch { surfaces.Remove(surface); surface.Dispose(); environment = null; throw; }
    }
    public async Task ClearAsync()
    {
        var old = surfaces.ToArray();
        foreach (var surface in old) surface.View.CoreWebView2?.Stop();
        // A blank controller keeps the profile alive after every page that could write cookies closes.
        var janitor = await CreateAsync(true, "X-Bar session cleanup");
        foreach (var surface in old) { surface.Dispose(); surfaces.Remove(surface); }
        await janitor.View.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);
        janitor.Dispose(); surfaces.Remove(janitor);
    }
    public void Release(BrowserSurface surface) { surfaces.Remove(surface); surface.Dispose(); }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        // Disposing a WebView can synchronously resume a failed initialization.
        // Detach ownership first so its catch/cleanup cannot mutate our enumeration.
        var old = surfaces.ToArray(); surfaces.Clear();
        foreach (var surface in old) surface.Dispose();
    }
}

internal sealed class BrowserSurface : IDisposable
{
    public readonly WebView2 View = new();
    public Window Window { get; }
    private readonly bool reader;
    private bool disposed;
    private string isolatedBootstrap = "";
    private long navigationRevision;
    private ulong navigationId;
    private bool navigating;
    public string? Url => View.CoreWebView2?.Source;
    public bool UserVisible => !reader && Window.IsVisible;
    public bool Failed { get; private set; }
    internal TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(8);
    private readonly System.Windows.Controls.TextBlock hint = new() { TextWrapping = TextWrapping.Wrap, Margin = new(12) };
    private readonly System.Windows.Controls.DockPanel toolbar = new() { Margin = new(8), LastChildFill = true, Visibility = Visibility.Collapsed };
    public BrowserSurface(bool reader, string title)
    {
        Theme.Install(); this.reader = reader;
        Window = new() { Title = title, Width = reader ? 1200 : 1100, Height = reader ? 1600 : 800, ShowActivated = !reader, ShowInTaskbar = !reader };
        if (!reader) Window.SourceInitialized += (_, _) => Theme.ApplyFrame(Window);
        if (reader) { Window.Left = -30000; Window.Top = -30000; Window.Opacity = 0; Window.WindowStyle = WindowStyle.None; Window.ResizeMode = ResizeMode.NoResize; }
        var panel = new System.Windows.Controls.DockPanel();
        if (!reader)
        {
            System.Windows.Automation.AutomationProperties.SetAutomationId(toolbar, "account.loginToolbar");
            toolbar.Children.Add(hint);
            System.Windows.Controls.DockPanel.SetDock(toolbar, System.Windows.Controls.Dock.Top); panel.Children.Add(toolbar);
            Window.Closing += (_, e) => { if (!disposed) { e.Cancel = true; Window.Hide(); } };
        }
        panel.Children.Add(View); Window.Content = panel;
        Window.Show(); if (!reader) Window.Hide();
    }
    public void SetLanguage(string language, bool login)
    {
        hint.Text = L10n.T("login.hint", language);
        toolbar.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
    }
    public void Show()
    {
        Window.Show(); Window.Activate(); Window.UpdateLayout();
        // Initialization happens while hidden; synchronize the native web host on every reopen.
        View.UpdateWindowPos();
    }
    public async Task InitializeAsync(CoreWebView2Environment env)
    {
        await View.EnsureCoreWebView2Async(env);
        var core = View.CoreWebView2;
        core.ProcessFailed += (_, _) => Failed = true;
        core.Settings.IsWebMessageEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = !reader;
        core.NavigationStarting += (_, e) => {
            if (!NavigationPolicy.Allowed(e.Uri) && e.Uri != "about:blank") { e.Cancel = true; return; }
            navigationRevision++; navigationId = e.NavigationId; navigating = true;
        };
        core.SourceChanged += (_, _) => navigationRevision++;
        core.NavigationCompleted += (_, e) => { if (e.NavigationId == navigationId) { navigating = false; navigationRevision++; } };
        core.NewWindowRequested += (_, e) => { e.Handled = true; if (!reader && NavigationPolicy.Allowed(e.Uri)) core.Navigate(e.Uri); };
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        {
            // The interactive window receives only read-only identity/authentication checks.
            var names = reader ? new[] { ("PageContext", "XBarPageContext"), ("Extractor", "TodayBarExtractor"), ("ContentExtractor", "TodayBarContentExtractor") } : new[] { ("PageContext", "XBarPageContext") };
            isolatedBootstrap = string.Join("\n", names.Select(n => "if (!globalThis." + n.Item2 + ") {\n" + File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Scripts", n.Item1 + ".js")) + "\n}"));
            if (!reader) isolatedBootstrap += """

                if (!globalThis.XBarLoginInteraction) {
                  let started = false;
                  const observe = event => {
                    const el = event.target;
                    if (el instanceof HTMLElement && el.closest('input, textarea, [contenteditable="true"], [role="dialog"]')) started = true;
                  };
                  for (const type of ['input', 'pointerdown', 'keydown', 'compositionstart']) document.addEventListener(type, observe, true);
                  Object.defineProperty(globalThis, 'XBarLoginInteraction', {value:Object.freeze({started:()=>started}), configurable:false});
                }
                """;
            var script = isolatedBootstrap;
            await core.CallDevToolsProtocolMethodAsync("Page.addScriptToEvaluateOnNewDocument", JsonSerializer.Serialize(new { source = script, worldName = "XBar.Isolated" }));
        }
    }
    internal sealed record LoginIdentity(string Account, long Revision, bool RequiresSessionCheck);
    private sealed record LoginContext(string? Account, bool Ready, bool RequiresSessionCheck);
    internal async Task<LoginIdentity?> ReadLoginIdentityAsync(CancellationToken token, bool requireVisible = true)
    {
        if (requireVisible && !UserVisible || navigating || Failed) return null;
        var revision = navigationRevision;
        var result = await ReadAsync<LoginContext>("""
            (() => {
              const context = globalThis.XBarPageContext?.snapshot();
              const focused = document.activeElement;
              const frameEditing = focused instanceof HTMLIFrameElement;
              const editing = focused instanceof HTMLInputElement || focused instanceof HTMLTextAreaElement || focused?.isContentEditable || frameEditing;
              const rendered = el => {
                const rect = el.getBoundingClientRect();
                if (rect.width <= 0 || rect.height <= 0) return false;
                for (let node = el; node instanceof HTMLElement; node = node.parentElement) {
                  const style = getComputedStyle(node);
                  if (style.display === 'none' || ['hidden','collapse'].includes(style.visibility) || Number(style.opacity) === 0) return false;
                }
                return true;
              };
              const inputs = [...document.querySelectorAll('input')].filter(rendered);
              const dialogInput = inputs.some(el => el.closest('[role="dialog"]'));
              const isChallenge = el => el.type === 'password' || el.autocomplete.split(/\s+/).some(token => ['current-password','one-time-code'].includes(token));
              // X also renders a transparent password placeholder behind the username field.
              // A focused challenge remains protected regardless of its styling.
              const challenge = inputs.some(isChallenge) || focused instanceof HTMLInputElement && isChallenge(focused);
              const loginRoute = /^\/i\/flow\/(login|signup)/.test(location.pathname) || /^\/i\/jf\/onboarding\//.test(location.pathname);
              // X autofocuses an untouched username field over an already signed-in page.
              // Actual input, clicks and keys remain protected, even after the field loses focus.
              const existingSession = loginRoute && !!context?.account && !challenge && !frameEditing && !globalThis.XBarLoginInteraction?.started();
              return {account: context?.account, requiresSessionCheck: existingSession,
                ready: !!context && !context.conflictingAccount && (existingSession || !context.authentication && !editing && !dialogInput)};
            })()
            """, token);
        return revision == navigationRevision && !navigating && (!requireVisible || UserVisible) && result is { Ready: true } && MetricRecord.ValidAccount(result.Account)
            ? new(result.Account!, revision, result.RequiresSessionCheck) : null;
    }
    public async Task NavigateAsync(string url, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Failed) throw new IOException("WebView2 process unavailable");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ulong? navigationId = null;
        void Starting(object? sender, CoreWebView2NavigationStartingEventArgs e) { if (e.Uri == url) navigationId = e.NavigationId; }
        void Done(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        { if (e.NavigationId != navigationId) return; if (e.IsSuccess) completion.TrySetResult(); else completion.TrySetException(new IOException("Navigation: " + e.WebErrorStatus)); }
        View.CoreWebView2.NavigationStarting += Starting;
        View.CoreWebView2.NavigationCompleted += Done;
        try
        {
            View.CoreWebView2.Navigate(url);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(45), token);
            token.ThrowIfCancellationRequested();
        }
        finally { View.CoreWebView2.NavigationStarting -= Starting; View.CoreWebView2.NavigationCompleted -= Done; if (token.IsCancellationRequested) View.CoreWebView2.Stop(); }
    }
    public async Task<T?> ReadAsync<T>(string expression, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Failed) throw new IOException("WebView2 process unavailable");
        if (!NavigationPolicy.Trusted(Url)) return default;
        var core = View.CoreWebView2;
        using var tree = JsonDocument.Parse(await core.CallDevToolsProtocolMethodAsync("Page.getFrameTree", "{}").WaitAsync(OperationTimeout, token));
        var frame = tree.RootElement.GetProperty("frameTree").GetProperty("frame").GetProperty("id").GetString();
        using var world = JsonDocument.Parse(await core.CallDevToolsProtocolMethodAsync("Page.createIsolatedWorld", JsonSerializer.Serialize(new { frameId = frame, worldName = "XBar.Isolated" })).WaitAsync(OperationTimeout, token));
        var contextId = world.RootElement.GetProperty("executionContextId").GetInt32();
        using var result = JsonDocument.Parse(await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", JsonSerializer.Serialize(new { expression = isolatedBootstrap + "\n" + expression, contextId, returnByValue = true, awaitPromise = true })).WaitAsync(OperationTimeout, token));
        token.ThrowIfCancellationRequested();
        if (!NavigationPolicy.Trusted(Url) || result.RootElement.TryGetProperty("exceptionDetails", out _) || !result.RootElement.GetProperty("result").TryGetProperty("value", out var value)) return default;
        return value.Deserialize<T>(StateStore.Json);
    }
    public void Stop() => View.CoreWebView2?.Stop();
    public void Dispose() { if (disposed) return; disposed = true; View.Dispose(); Window.Close(); }
}
