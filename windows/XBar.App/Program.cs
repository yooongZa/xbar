using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using XBar.Core;

namespace XBar.App;

internal sealed class Program : Application
{
    private readonly CancellationTokenSource lifetime = new();
    private Collector collector = null!;
    private DesktopWidget desktop = null!;
    private SettingsWindow? settings;
    private AppUpdater? updater;
    private readonly StateStore store = new(StateStore.DefaultRoot);
    private readonly DispatcherTimer registrationTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private bool desktopHidden;
    private bool probing;
    private bool closing;
    [STAThread] public static void Main(string[] args)
    {
#if XBAR_UPDATER_LAB
        AppContext.SetData("XBar.UpdaterLab", true);
#endif
        var command = AppCommand.Parse(args);
        try
        {
            var activation = global::Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
            if (activation is global::Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs protocol) command = AppCommand.Parse([protocol.Uri.ToString()]);
            else if (activation?.Kind == global::Windows.ApplicationModel.Activation.ActivationKind.StartupTask) command = new("background");
        }
        catch (System.Runtime.InteropServices.COMException) { /* Unpackaged development launch. */ }
        using var mutex = new Mutex(true, "Global\\" + CommandPipe.Name, out bool first);
        if (!first) { if (command.Action != "background") CommandPipe.SendAsync(command).GetAwaiter().GetResult(); return; }
        var app = new Program { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += (_, _) => app.Start(command);
        app.Run();
    }
    private void Start(AppCommand command)
    {
        DispatcherUnhandledException += (_, e) => { Log(e.Exception); e.Handled = true; MessageBox.Show(L10n.T("failure", collector?.Settings.Language ?? "auto"), "X-Bar"); };
        collector = new(store); collector.Failed += Log;
        desktop = new(collector, ExecuteAsync);
        desktop.HideRequested += () => { desktopHidden = true; desktop.Hide(); };
        collector.Changed += () => { desktop.Refresh(); settings?.RefreshRegistration(); ApplyMode(); };
        try {
            updater = new(store, () => collector.Settings.Language,
                () => { registrationTimer.Stop(); collector.SleepChanged(true); desktop.AllowClose = true; },
                () => { desktop.AllowClose = false; registrationTimer.Start(); collector.SleepChanged(false); });
            updater.Changed += () => settings?.RefreshUpdate(); updater.Start();
        } catch (System.Runtime.InteropServices.COMException e) { Log(e); }
        Exit += (_, _) => { updater?.Dispose(); if (!closing) collector.Dispose(); };
        registrationTimer.Tick += async (_, _) => await ProbeAsync(); registrationTimer.Start();
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        SystemEvents.TimeChanged += TimeChanged;
        // Acknowledge dispatch, not the eventual network operation. Otherwise a slow
        // login load makes the provider retry an already accepted command via activation.
        _ = CommandPipe.ListenAsync(c => Dispatcher.InvokeAsync(() => { _ = ExecuteAsync(c); }).Task, lifetime.Token);
        ApplyMode(); _ = ProbeAsync();
        if (command.Action != "background") _ = ExecuteAsync(command);
        collector.RequestRefresh(false);
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode is PowerModes.Suspend or PowerModes.Resume) Dispatcher.BeginInvoke(() => collector.SleepChanged(e.Mode == PowerModes.Suspend)); }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(desktop.KeepOnScreen);
    private void TimeChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { TimeZoneInfo.ClearCachedData(); collector.SleepChanged(false); });
    private void ApplyMode()
    {
        if (closing) return;
        var registration = store.Read<WidgetRegistration>("registration.json");
        // Keep a usable surface while board registration is unknown or incomplete.
        bool needsDesktop = collector.Settings.Mode != DisplayMode.Windows || registration is not { Count: > 0, Error: null } || DateTimeOffset.UtcNow - registration.CheckedAt > TimeSpan.FromMinutes(2);
        if (needsDesktop && !desktopHidden) { if (!desktop.IsVisible) desktop.Show(); } else desktop.Hide();
    }
    private async Task ProbeAsync()
    {
#if XBAR_UPDATER_LAB
        await Task.CompletedTask; return;
#else
        if (probing || closing || collector.Settings.Mode == DisplayMode.Desktop && settings is null) return;
        probing = true;
        try
        {
            var exe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Provider", "XBar.WidgetProvider.exe"));
            if (File.Exists(exe))
            {
                using var p = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "--probe" } });
                if (p is not null)
                {
                    try { await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8)); }
                    catch (TimeoutException) { if (!p.HasExited) p.Kill(); throw; }
                }
            }
            settings?.RefreshRegistration(); ApplyMode();
        }
        catch (Exception e) { Log(e); }
        finally { probing = false; }
#endif
    }
    private async Task ExecuteAsync(AppCommand command)
    {
        if (closing) return;
        try
        {
            switch (command.Action)
            {
                case "activate": desktopHidden = false; ApplyMode(); OpenSettings(); break;
                case "settings": OpenSettings(); break;
                case "updates": updater?.Show(); break;
                case "login": await collector.OpenBrowserAsync(login: true); break;
                case "refresh": if (!collector.Settings.Connected) await collector.OpenBrowserAsync(login: true); else collector.RequestRefresh(true); break;
                case "post": if (command.IsValid) await collector.OpenBrowserAsync(command.Argument); break;
                case "analysis": await collector.OpenBrowserAsync(); break;
                case "period": if (Enum.TryParse<ContentPeriod>(command.Argument, out var period) && Enum.IsDefined(period)) collector.UpdateSettings(collector.Settings with { Period = period }); break;
                case "logout": if (MessageBox.Show(L10n.T("logout.confirm", collector.Settings.Language), "X-Bar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) await collector.DisconnectAsync(); break;
                case "quit": Quit(); break;
            }
        }
        catch (Exception e) { Log(e); MessageBox.Show(L10n.T(e.GetType().Name.Contains("WebView2Runtime") ? "runtime" : "failure", collector.Settings.Language), "X-Bar"); }
    }
    private void OpenSettings()
    {
        if (settings is null) { settings = new(collector, store, ExecuteAsync, updater); settings.Closed += (_, _) => settings = null; }
        settings.Show(); settings.Activate();
        _ = ProbeAsync();
    }
    private void Quit()
    {
        if (closing) return; closing = true;
        registrationTimer.Stop(); lifetime.Cancel();
        SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.DisplaySettingsChanged -= DisplayChanged; SystemEvents.TimeChanged -= TimeChanged;
        updater?.Dispose(); collector.Dispose(); desktop.AllowClose = true; desktop.Close(); settings?.Close(); Shutdown();
    }
    private static void Log(Exception e)
    {
        // No URLs, page contents, cookies or identifiers in diagnostics.
        try { Directory.CreateDirectory(StateStore.DefaultRoot); var path = Path.Combine(StateStore.DefaultRoot, "diagnostics.log"); if (File.Exists(path) && new FileInfo(path).Length > 128_000) File.WriteAllText(path, ""); File.AppendAllText(path, DateTimeOffset.UtcNow.ToString("O") + " " + e.GetType().Name + " " + e.HResult + Environment.NewLine); } catch (IOException) { }
    }
}
