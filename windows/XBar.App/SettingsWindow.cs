using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Windows.ApplicationModel;
using XBar.Core;

namespace XBar.App;

internal interface IStartupPreference
{
    Task<StartupTaskState> ReadAsync();
    Task<StartupTaskState> SetAsync(bool enabled);
}
internal sealed class WindowsStartupPreference : IStartupPreference
{
    public async Task<StartupTaskState> ReadAsync() => (await StartupTask.GetAsync("XBarStartup")).State;
    public async Task<StartupTaskState> SetAsync(bool enabled)
    {
        var task = await StartupTask.GetAsync("XBarStartup");
        if (enabled) return await task.RequestEnableAsync();
        task.Disable(); return (await StartupTask.GetAsync("XBarStartup")).State;
    }
}
internal sealed class SettingsWindow : Window
{
    private readonly StateStore store;
    private readonly Collector collector;
    private readonly IStartupPreference startupPreference;
    private readonly TextBlock registration = UI.Text("", 12, "Muted");
    private readonly TextBlock feedback = UI.Text("", 12, "Muted");
    private readonly CheckBox startup = new();
    private StartupTaskState? startupState;
    private bool saving, closed;
    private Action? refreshUpdate, refreshAccount;
    internal Button SaveControl { get; private set; } = null!;
    internal CheckBox? AutomaticControl { get; private set; }
    internal CheckBox StartupControl => startup;
    internal IReadOnlyList<RadioButton> Navigation => navigation;
    private readonly List<RadioButton> navigation = [];
    public void RefreshUpdate() => refreshUpdate?.Invoke();
    public SettingsWindow(Collector collector, StateStore store, Func<AppCommand, Task> execute, AppUpdater? updater = null, IStartupPreference? startupPreference = null)
    {
        this.store = store; this.collector = collector; this.startupPreference = startupPreference ?? new WindowsStartupPreference();
        Theme.Install(); Style = (Style)Application.Current.FindResource(typeof(Window));
        string T(string key) => L10n.T(key, collector.Settings.Language);
        Title = "X-Bar · " + T("settings"); Width = 600; Height = 650; MinWidth = 500; MinHeight = 470;
        SourceInitialized += (_, _) => Theme.ApplyFrame(this);
        var root = new DockPanel { Margin = new(24) }; Content = root;
        var heading = new StackPanel { Margin = new(0, 0, 0, 18) };
        var name = UI.Text(T("settings"), 25); name.FontWeight = FontWeights.SemiBold; heading.Children.Add(name);
        heading.Children.Add(UI.Text(T("settings.hint"), 12, "Muted")); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var footer = new StackPanel { Margin = new(0, 16, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        feedback.Text = T("settings.apply"); feedback.Margin = new(0, 0, 0, 10); AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite); footer.Children.Add(feedback);
        var buttons = new DockPanel(); footer.Children.Add(buttons);
        SaveControl = UI.Button(T("save"), () => { }, "XBar.Primary"); SaveControl.MinWidth = 100; SaveControl.IsDefault = true; DockPanel.SetDock(SaveControl, Dock.Right); buttons.Children.Add(SaveControl);
        var close = UI.Button(T("close"), Close); close.Margin = new(0, 0, 8, 0); close.IsCancel = true; DockPanel.SetDock(close, Dock.Right); buttons.Children.Add(close);
        var version = UI.Text("X-Bar " + Version(), 11, "Muted"); version.VerticalAlignment = VerticalAlignment.Center; buttons.Children.Add(version);
        var tabs = new UniformGrid { Rows = 1, Columns = 4, Margin = new(2) };
        var tabsSurface = UI.Surface(tabs, new(0), "Hover"); tabsSurface.Margin = new(0, 0, 0, 18); DockPanel.SetDock(tabsSurface, Dock.Top); root.Children.Add(tabsSurface);
        var pages = new Grid(); root.Children.Add(pages);
        StackPanel Page(string key)
        {
            var panel = new StackPanel(); var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Visibility = Visibility.Collapsed };
            pages.Children.Add(scroll);
            var tab = UI.Segment(T(key), "settingsPage"); tab.FontSize = 12; AutomationProperties.SetAutomationId(tab, key);
            tab.Checked += (_, _) => { foreach (UIElement child in pages.Children) child.Visibility = child == scroll ? Visibility.Visible : Visibility.Collapsed; scroll.ScrollToTop(); };
            tabs.Children.Add(tab); navigation.Add(tab); return panel;
        }
        var widgets = Page("settings.widgets"); var account = Page("settings.account"); var updatesPage = Page("update.title"); var systemPage = Page("settings.system");
        StackPanel Section(StackPanel page, string label)
        {
            var content = new StackPanel(); var title = UI.Text(label, 14); title.FontWeight = FontWeights.SemiBold; title.Margin = new(0, 0, 0, 14); content.Children.Add(title);
            var card = UI.Surface(content, new(16)); card.Margin = new(0, 0, 0, 12); page.Children.Add(card); return content;
        }
        void Field(StackPanel parent, string label, UIElement control)
        {
            var grid = new Grid { Margin = new(0, 0, 0, 12) }; grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var text = UI.Text(label, 13); text.VerticalAlignment = VerticalAlignment.Center; text.Margin = new(0, 0, 12, 0); grid.Children.Add(text); Grid.SetColumn(control, 1); grid.Children.Add(control); parent.Children.Add(grid);
        }
        CheckBox Check(StackPanel group, string key, bool value)
        { var c = new CheckBox { Content = T(key), IsChecked = value, Margin = new(0, 5, 0, 7), MinHeight = 28 }; AutomationProperties.SetAutomationId(c, key); group.Children.Add(c); return c; }
        var display = Section(widgets, T("mode")); var modes = new UniformGrid { Rows = 1, Columns = 3 }; var selectedMode = collector.Settings.Mode;
        foreach (var mode in Enum.GetValues<DisplayMode>()) {
            var radio = UI.Segment(T(mode == DisplayMode.Desktop ? "desktop" : mode == DisplayMode.Windows ? "windows" : "both"), "displayMode");
            radio.Content = new TextBlock { Text = radio.Content.ToString()!, FontSize = 12, TextWrapping = TextWrapping.Wrap }; radio.Margin = new(2); radio.IsChecked = mode == selectedMode; radio.Checked += (_, _) => selectedMode = mode; modes.Children.Add(radio);
        }
        display.Children.Add(modes); registration.Margin = new(2, 12, 0, 8); display.Children.Add(registration);
        var guide = new Expander { Header = T("pin.guide"), Content = UI.Text(T("pin"), 12, "Muted"), Padding = new(0, 4, 0, 0) }; display.Children.Add(guide);
        var appearance = Section(widgets, T("appearance"));
        var topmost = Check(appearance, "topmost", collector.Settings.AlwaysOnTop); var effects = Check(appearance, "effects", collector.Settings.Effects); appearance.Children.Add(UI.Text(T("theme.note"), 11, "Muted"));
        var connection = Section(account, T("account")); var connectionStatus = UI.Text("", 13); connection.Children.Add(connectionStatus);
        var accountActions = new WrapPanel { Margin = new(0, 12, 0, 0) }; connection.Children.Add(accountActions);
        var manage = UI.Button("", () => _ = execute(new("login")), "XBar.Primary"); manage.Margin = new(0, 0, 6, 0); accountActions.Children.Add(manage);
        var analysis = UI.Button(T("analysis"), () => _ = execute(new("analysis"))); analysis.Margin = new(0, 0, 6, 0); accountActions.Children.Add(analysis);
        var disconnect = UI.Button(T("logout"), () => _ = execute(new("logout"))); accountActions.Children.Add(disconnect);
        refreshAccount = () => {
            bool connected = collector.Settings.Connected && collector.State.Status != CollectionStatus.NeedsLogin;
            connectionStatus.Text = T(connected ? "connected" : "disconnected") + (connected && collector.State.Today is { } today ? " · " + today.Account : "");
            manage.Content = T(connected ? "account.change" : "login"); UI.Label(manage, manage.Content.ToString()!); analysis.IsEnabled = disconnect.IsEnabled = collector.Settings.Connected;
        };
        var collection = Section(account, T("collection"));
        var interval = UI.Choice(new[] { "5 " + T("minutes"), "10 " + T("minutes"), "30 " + T("minutes") }, Array.IndexOf(new[] { 5, 10, 30 }, collector.Settings.RefreshMinutes), T("interval")); Field(collection, T("interval"), interval);
        var growth = Check(collection, "growth", collector.Settings.Growth); collection.Children.Add(UI.Text(T("scope.hint"), 11, "Muted"));
        var updates = Section(updatesPage, T("update.title"));
        updates.Children.Add(UI.Text(T("update.installed") + "  " + Version(), 13));
        if (updater is not null) {
            var available = UI.Text("", 13, "Muted"); available.Margin = new(0, 12, 0, 12); updates.Children.Add(available);
            var check = UI.Button(T("update.check"), updater.Show, "XBar.Primary"); check.HorizontalAlignment = HorizontalAlignment.Left; updates.Children.Add(check);
            var options = Section(updatesPage, T("update.preferences")); AutomaticControl = Check(options, "update.auto", updater.Preferences.AutomaticChecks); options.Children.Add(UI.Text(T("update.hint"), 12, "Muted"));
            refreshUpdate = () => {
                available.Text = updater.Phase == UpdatePhase.Idle ? T("update.notchecked") : updater.Status;
                if (updater.Available is { } next) available.Text += " · " + next.Release.DisplayVersion;
                check.IsEnabled = !updater.Busy;
            }; RefreshUpdate();
        } else updates.Children.Add(UI.Text(T("update.unavailable"), 12, "Muted"));
        var languageSection = Section(systemPage, T("language"));
        var language = UI.Choice(new[] { T("auto"), "한국어", "English", "日本語" }, Array.IndexOf(new[] { "auto", "ko", "en", "ja" }, collector.Settings.Language), T("language")); Field(languageSection, T("language"), language);
        var system = Section(systemPage, T("startup")); startup.Content = T("startup"); startup.Margin = new(0, 0, 0, 8); startup.IsEnabled = false; AutomationProperties.SetAutomationId(startup, "startup"); system.Children.Add(startup);
        system.Children.Add(UI.Text(T("startup.note"), 12, "Muted"));
        var open = UI.Button(T("startup.open"), () => Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true })); open.HorizontalAlignment = HorizontalAlignment.Left; open.Margin = new(0, 10, 0, 0); system.Children.Add(open);
        var quit = UI.Button(T("quit"), () => _ = execute(new("quit"))); quit.HorizontalAlignment = HorizontalAlignment.Left; systemPage.Children.Add(quit);
        SaveControl.Click += async (_, _) => {
            if (saving) return;
            var wantedStartup = startup.IsChecked == true; var changeStartup = startup.IsEnabled && startupState is { } state && StartupEnabled(state) != wantedStartup;
            var mode = selectedMode; var lang = new[] { "auto", "ko", "en", "ja" }[Math.Max(0, language.SelectedIndex)]; var minutes = new[] { 5, 10, 30 }[Math.Max(0, interval.SelectedIndex)];
            var always = topmost.IsChecked == true; var effect = effects.IsChecked == true; var sort = growth.IsChecked == true; var automatic = AutomaticControl?.IsChecked;
            saving = true; root.IsEnabled = false; feedback.Text = T("settings.saving");
            try {
                // Ask Windows first; denial must not commit the other staged preferences.
                if (changeStartup) {
                    startupState = await this.startupPreference.SetAsync(wantedStartup);
                    if (StartupEnabled(startupState.Value) != wantedStartup) { feedback.Text = T("startup.denied"); return; }
                }
                // Merge only this dialog’s fields into the latest state, preserving concurrent account/period changes.
                collector.UpdateSettings(collector.Settings with { Mode = mode, Language = lang, RefreshMinutes = minutes, AlwaysOnTop = always, Effects = effect, Growth = sort });
                if (updater is not null && automatic is { } auto && updater.Preferences.AutomaticChecks != auto) updater.SetAutomaticChecks(auto);
                saving = false; Close();
            }
            catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException or System.IO.IOException or UnauthorizedAccessException) { feedback.Text = T("settings.save.error"); }
            finally { saving = false; root.IsEnabled = true; }
        };
        Closing += (_, e) => { if (saving) e.Cancel = true; }; Closed += (_, _) => closed = true;
        Loaded += async (_, _) => {
            try {
                var state = await this.startupPreference.ReadAsync(); if (closed) return;
                startupState = state; startup.IsChecked = StartupEnabled(state); startup.IsEnabled = state is StartupTaskState.Enabled or StartupTaskState.Disabled;
            } catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException) { if (!closed) startup.IsEnabled = false; }
        };
        navigation[0].IsChecked = true; RefreshRegistration();
    }
    private static bool StartupEnabled(StartupTaskState state) => state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    private static string Version()
    { try { var v = Package.Current.Id.Version; return $"{v.Major}.{v.Minor}.{v.Build}"; } catch { return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.3.0"; } }
    public void RefreshRegistration()
    {
        var state = store.Read<WidgetRegistration>("registration.json");
        registration.Text = L10n.T(state is null || state.Error is not null || DateTimeOffset.UtcNow - state.CheckedAt > TimeSpan.FromMinutes(2) ? "pin.unknown" : state.Count > 0 ? "pinned" : "pin.needed", collector.Settings.Language);
        refreshAccount?.Invoke();
    }
}
