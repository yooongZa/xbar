using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel;
using XBar.App;
using XBar.Core;

namespace XBar.WindowsTests;
internal static class SettingsTests
{
    private sealed class Startup : IStartupPreference
    {
        public StartupTaskState State = StartupTaskState.Disabled;
        public bool Denied;
        public int Writes;
        public TaskCompletionSource? Pending;
        public Task<StartupTaskState> ReadAsync() => Task.FromResult(State);
        public async Task<StartupTaskState> SetAsync(bool enabled)
        { Writes++; if (Pending is { } pending) await pending.Task; return State = Denied ? StartupTaskState.DisabledByUser : enabled ? StartupTaskState.Enabled : StartupTaskState.Disabled; }
    }
    private sealed class Offline : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)); }
    public static async Task Run(Action<string, bool, string?> check, string output)
    {
        void Check(string name, bool value) => check(name, value, null);
        var root = Path.Combine(Path.GetTempPath(), "XBar-settings-tests-" + Guid.NewGuid().ToString("N")); var store = new StateStore(root);
        using var collector = new Collector(store, networkAvailable: () => false);
        var source = new UpdateSource(new("https://yooongza.github.io/xbar/windows-updates.json"), Convert.ToBase64String(new byte[32]));
        using var updater = new AppUpdater(store, () => collector.Settings.Language, () => { }, () => { },
            new("LIMOD.XBar.Dev", "CN=XBar Development", "x64", new(0, 3, 0, 0), 26200), new UpdateClient(source, new Offline()));
        var startup = new Startup();
        var commands = new List<AppCommand>();
        SettingsWindow Open() { var w = new SettingsWindow(collector, store, c => { commands.Add(c); return Task.CompletedTask; }, updater, startup); w.Show(); w.UpdateLayout(); return w; }
        var cancel = Open(); await Task.Delay(50);
        cancel.AutomaticControl!.IsChecked = false; cancel.StartupControl.IsChecked = true;
        Check("Settings choices stay staged before Save", updater.Preferences.AutomaticChecks && startup.Writes == 0);
        cancel.Close(); Check("Closing settings discards staged update and startup choices", updater.Preferences.AutomaticChecks && startup.State == StartupTaskState.Disabled && startup.Writes == 0);
        var save = Open(); await Task.Delay(30); save.AutomaticControl!.IsChecked = false;
        collector.UpdateSettings(collector.Settings with { Period = ContentPeriod.Week });
        save.SaveControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(30);
        Check("Save persists automatic checks with app preferences", !save.IsVisible && !updater.Preferences.AutomaticChecks && store.Read<UpdatePreferences>("updates.json")?.AutomaticChecks == false);
        Check("Save preserves concurrent period changes and does not rewrite startup", collector.Settings.Period == ContentPeriod.Week && startup.Writes == 0);
        var denied = Open(); await Task.Delay(30); denied.AutomaticControl!.IsChecked = true; denied.StartupControl.IsChecked = true; startup.Denied = true;
        denied.SaveControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(30);
        Check("Windows startup denial keeps dialog and other preferences unchanged", denied.IsVisible && !updater.Preferences.AutomaticChecks && startup.Writes == 1);
        denied.Close(); startup.State = StartupTaskState.Disabled; startup.Denied = false; startup.Pending = new();
        var pending = Open(); await Task.Delay(30); pending.StartupControl.IsChecked = true; pending.AutomaticControl!.IsChecked = true;
        pending.SaveControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); pending.SaveControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); pending.Close();
        Check("Pending save blocks close and duplicate startup changes", pending.IsVisible && startup.Writes == 2 && !updater.Preferences.AutomaticChecks);
        startup.Pending.SetResult(); await Task.Delay(60);
        Check("Accepted startup save commits once then closes", !pending.IsVisible && updater.Preferences.AutomaticChecks && startup.State == StartupTaskState.Enabled && startup.Writes == 2);
        startup.Pending = null;
        var signedOut = Open(); signedOut.UpdateLayout();
        Check("First disconnected launch opens account setup and visible sign-in", signedOut.Navigation[1].IsChecked == true && FindById<TextBlock>(signedOut, "account.setup")!.IsVisible && FindById<Button>(signedOut, "account.signin")!.IsVisible);
        var signIn = FindById<Button>(signedOut, "account.signin")!;
        signIn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("Signed-out account offers only sign-in and routes to login", signIn.IsVisible && !FindById<Button>(signedOut, "account.analysis")!.IsVisible && !FindById<Button>(signedOut, "account.signout")!.IsVisible && commands.Last().Action == "login");
        collector.UpdateSettings(collector.Settings with { Connected = true }); signedOut.RefreshRegistration(); signedOut.UpdateLayout();
        var analysis = FindById<Button>(signedOut, "account.analysis")!;
        analysis.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("Signed-in account replaces login with analytics and sign-out", !signIn.IsVisible && analysis.IsVisible && FindById<Button>(signedOut, "account.signout")!.IsVisible && commands.Last().Action == "analysis");
        Check("Account setup guidance disappears after connection", !FindById<TextBlock>(signedOut, "account.setup")!.IsVisible);
        var returning = Open(); returning.UpdateLayout();
        Check("Connected users retain the usual widget settings landing page", returning.Navigation[0].IsChecked == true);
        returning.Close();
        signedOut.Close(); collector.UpdateSettings(collector.Settings with { Connected = false });
        Directory.CreateDirectory(output);
        foreach (var theme in new[] { "light", "dark" }) foreach (var language in new[] { "ko", "en", "ja" }) {
            collector.UpdateSettings(collector.Settings with { Language = language }); Theme.TestMode = theme; Theme.Apply();
            var w = Open(); await Task.Delay(20);
            Check($"Settings native control text contrast {theme} {language}", Contrast(((SolidColorBrush)w.AutomaticControl!.Foreground).Color, ((SolidColorBrush)Application.Current.Resources["XBar.Surface"]).Color) >= 4.5);
            bool fit = true;
            for (int i = 0; i < w.Navigation.Count; i++) {
                w.Navigation[i].IsChecked = true; w.UpdateLayout(); await Task.Delay(20);
                var b = w.Navigation[i]; var pos = b.TranslatePoint(new Point(b.ActualWidth, b.ActualHeight), w);
                fit &= b.ActualWidth >= 90 && pos.X <= w.ActualWidth && pos.Y < w.ActualHeight && w.SaveControl.IsVisible;
                Capture(w, Path.Combine(output, $"settings-{theme}-{language}-{i}.png"));
            }
            Check($"Settings navigation and footer fit {theme} {language}", fit && w.Navigation.Count == 4);
            w.Close();
        }
        collector.UpdateSettings(collector.Settings with { Language = "en" });
        var narrow = Open(); narrow.Width = narrow.MinWidth; narrow.Height = narrow.MinHeight; narrow.UpdateLayout();
        Check("Settings minimum window keeps all pages and Save accessible", narrow.Navigation.All(b => b.IsVisible) && narrow.SaveControl.TranslatePoint(new Point(0, narrow.SaveControl.ActualHeight), narrow).Y <= narrow.ActualHeight);
        narrow.Close(); Theme.TestMode = null; Theme.Apply();
    }
    internal static T? FindById<T>(DependencyObject root, string id) where T : DependencyObject
    {
        if (root is T value && AutomationProperties.GetAutomationId(root) == id) return value;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            if (FindById<T>(child, id) is { } match) return match;
        return null;
    }
    private static double Contrast(Color a, Color b)
    {
        static double Channel(byte value) { double c = value / 255.0; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
        static double Luminance(Color c) => .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
        double x = Luminance(a), y = Luminance(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }
    private static void Capture(Window window, string path)
    {
        var image = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32); image.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(path); encoder.Save(file);
    }
}
