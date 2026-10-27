using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Windows.ApplicationModel;
using XBar.Core;

namespace XBar.App;

internal sealed class SettingsWindow : Window
{
    private readonly StateStore store;
    private readonly Collector collector;
    private readonly TextBlock registration = UI.Text("", 12, "Muted");
    private readonly CheckBox startup = new();
    private bool updatingStartup;
    private Action? refreshUpdate;
    public void RefreshUpdate() => refreshUpdate?.Invoke();
    public SettingsWindow(Collector collector, StateStore store, Func<AppCommand, Task> execute, AppUpdater? updater = null)
    {
        Theme.Install(); Style = (Style)Application.Current.FindResource(typeof(Window)); this.store = store; this.collector = collector;
        string T(string key) => L10n.T(key, collector.Settings.Language);
        Title = "X-Bar · " + T("settings"); Width = 540; Height = 730; MinWidth = 460; MinHeight = 420;
        SourceInitialized += (_, _) => Theme.ApplyFrame(this);
        var root = new DockPanel { Margin = new(24) }; Content = root;
        var heading = new StackPanel { Margin = new(0, 0, 0, 18) }; var name = UI.Text(T("settings"), 25); name.FontWeight = FontWeights.SemiBold; heading.Children.Add(name); heading.Children.Add(UI.Text(T("settings.hint"), 12, "Muted")); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var footer = new DockPanel { Margin = new(0, 18, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var save = UI.Button(T("save"), () => { }, "XBar.Primary"); save.MinWidth = 100; save.IsDefault = true; DockPanel.SetDock(save, Dock.Right); footer.Children.Add(save);
        var close = UI.Button(T("close"), Close); close.Margin = new(0, 0, 8, 0); close.IsCancel = true; DockPanel.SetDock(close, Dock.Right); footer.Children.Add(close);
        var version = UI.Button("X-Bar " + Version(), () => updater?.Show()); version.FontSize = 11; version.Padding = new(0); version.HorizontalContentAlignment = HorizontalAlignment.Left; version.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(version);
        var panel = new StackPanel(); root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        StackPanel Section(string label)
        { var content = new StackPanel(); var title = UI.Text(label, 14); title.FontWeight = FontWeights.SemiBold; title.Margin = new(0, 0, 0, 14); content.Children.Add(title); var card = UI.Surface(content, new(16)); card.Margin = new(0, 0, 0, 12); panel.Children.Add(card); return content; }
        void Field(StackPanel parent, string label, UIElement control)
        { var grid = new Grid { Margin = new(0, 0, 0, 12) }; grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); var text = UI.Text(label, 13); text.VerticalAlignment = VerticalAlignment.Center; text.Margin = new(0, 0, 12, 0); grid.Children.Add(text); Grid.SetColumn(control, 1); grid.Children.Add(control); parent.Children.Add(grid); }
        var display = Section(T("mode")); var modes = new UniformGrid { Rows = 1, Columns = 3 };
        var selectedMode = collector.Settings.Mode;
        foreach (var mode in Enum.GetValues<DisplayMode>()) { var radio = UI.Segment(T(mode == DisplayMode.Desktop ? "desktop" : mode == DisplayMode.Windows ? "windows" : "both"), "displayMode"); radio.Content = new TextBlock { Text = radio.Content.ToString()!, FontSize = 12, TextWrapping = TextWrapping.Wrap }; radio.Margin = new(2); radio.IsChecked = mode == selectedMode; radio.Checked += (_, _) => selectedMode = mode; modes.Children.Add(radio); } display.Children.Add(modes);
        registration.Margin = new(2, 12, 0, 4); display.Children.Add(registration); display.Children.Add(UI.Text(T("pin"), 11, "Muted"));
        var appearance = Section(T("appearance"));
        var language = UI.Choice(new[] { T("auto"), "한국어", "English", "日本語" }, Array.IndexOf(new[] { "auto", "ko", "en", "ja" }, collector.Settings.Language), T("language")); Field(appearance, T("language"), language);
        CheckBox Check(StackPanel group, string key, bool value)
        { var c = new CheckBox { Content = T(key), IsChecked = value, Margin = new(0, 5, 0, 7), MinHeight = 28 }; group.Children.Add(c); return c; }
        var topmost = Check(appearance, "topmost", collector.Settings.AlwaysOnTop); var effects = Check(appearance, "effects", collector.Settings.Effects); appearance.Children.Add(UI.Text(T("theme.note"), 11, "Muted"));
        var collection = Section(T("collection")); var interval = UI.Choice(new[] { "5 " + T("minutes"), "10 " + T("minutes"), "30 " + T("minutes") }, Array.IndexOf(new[] { 5, 10, 30 }, collector.Settings.RefreshMinutes), T("interval")); Field(collection, T("interval"), interval);
        var growth = Check(collection, "growth", collector.Settings.Growth); collection.Children.Add(UI.Text(T("scope.hint"), 11, "Muted"));
        if (updater is not null) {
            var updates = Section(T("update.title"));
            var check = UI.Button(T("update.check"), updater.Show); check.HorizontalAlignment = HorizontalAlignment.Left; updates.Children.Add(check);
            var available = UI.Text("", 12, "Muted"); available.Margin = new(0, 10, 0, 8); updates.Children.Add(available);
            var automatic = Check(updates, "update.auto", updater.Preferences.AutomaticChecks); updates.Children.Add(UI.Text(T("update.hint"), 11, "Muted"));
            automatic.Checked += (_, _) => updater.SetAutomaticChecks(true); automatic.Unchecked += (_, _) => updater.SetAutomaticChecks(false);
            refreshUpdate = () => { available.Text = updater.Status + (updater.Available is { } next ? " · " + next.Release.DisplayVersion : ""); check.IsEnabled = !updater.Busy; version.Content = "X-Bar " + Version() + (updater.Available is not null ? " ↑" : ""); version.ToolTip = available.Text; }; RefreshUpdate();
        }
        var connection = Section(T("account")); connection.Children.Add(UI.Text(collector.State.Today?.Account ?? T(collector.Settings.Connected ? "connected" : "disconnected"), 13));
        var accountActions = new WrapPanel { Margin = new(0, 10, 0, 0) }; connection.Children.Add(accountActions);
        foreach (var action in new[] { "login", "analysis", "logout" }) { var b = UI.Button(T(action), () => _ = execute(new(action))); b.Margin = new(0, 0, 6, 0); accountActions.Children.Add(b); }
        var system = Section(T("startup")); startup.Content = T("startup"); startup.Margin = new(0, 0, 0, 8); startup.IsEnabled = false; system.Children.Add(startup);
        startup.Checked += async (_, _) => await SetStartupAsync(true); startup.Unchecked += async (_, _) => await SetStartupAsync(false);
        var open = UI.Button(T("startup.open"), () => Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true })); open.HorizontalAlignment = HorizontalAlignment.Left; system.Children.Add(open);
        var quit = UI.Button(T("quit"), () => _ = execute(new("quit"))); quit.HorizontalAlignment = HorizontalAlignment.Left; panel.Children.Add(quit);
        save.Click += (_, _) => { collector.UpdateSettings(collector.Settings with { Mode = selectedMode, Language = new[] { "auto", "ko", "en", "ja" }[Math.Max(0, language.SelectedIndex)], RefreshMinutes = new[] { 5, 10, 30 }[Math.Max(0, interval.SelectedIndex)], AlwaysOnTop = topmost.IsChecked == true, Effects = effects.IsChecked == true, Growth = growth.IsChecked == true }); Close(); };
        Loaded += async (_, _) => await LoadStartupAsync(); RefreshRegistration();
    }
    private static string Version()
    { try { var v = Package.Current.Id.Version; return $"{v.Major}.{v.Minor}.{v.Build}"; } catch { return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.0"; } }
    private async Task LoadStartupAsync()
    {
        updatingStartup = true;
        try { var task = await StartupTask.GetAsync("XBarStartup"); startup.IsChecked = task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy; startup.IsEnabled = task.State is StartupTaskState.Enabled or StartupTaskState.Disabled; }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException) { startup.IsEnabled = false; }
        finally { updatingStartup = false; }
    }
    private async Task SetStartupAsync(bool enabled)
    {
        if (updatingStartup) return;
        updatingStartup = true; startup.IsEnabled = false;
        try { var task = await StartupTask.GetAsync("XBarStartup"); if (enabled) await task.RequestEnableAsync(); else task.Disable(); }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
        finally { updatingStartup = false; await LoadStartupAsync(); }
    }
    public void RefreshRegistration()
    {
        var state = store.Read<WidgetRegistration>("registration.json");
        registration.Text = L10n.T(state is null || state.Error is not null || DateTimeOffset.UtcNow - state.CheckedAt > TimeSpan.FromMinutes(2) ? "pin.unknown" : state.Count > 0 ? "pinned" : "pin.needed", collector.Settings.Language);
    }
}
