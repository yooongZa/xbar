using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Windows.ApplicationModel;
using XBar.App;
using XBar.Core;

namespace XBar.WindowsTests;
internal static class DesktopTests
{
    public static async Task Run(Action<string, bool, string?> check)
    {
        void Check(string name, bool ok) => check(name, ok, null);
        var registry = @"Software\XBar.DesktopTests\" + Guid.NewGuid().ToString("N");
        var startup = new DesktopStartupPreference(@"C:\Users\시험 사용자\Apps\X-Bar\XBar.exe", registry + @"\Run", registry + @"\Approved");
        try {
            Check("Unpackaged startup initially disabled", await startup.ReadAsync() == StartupTaskState.Disabled);
            Check("Unpackaged startup enables without MSIX identity", await startup.SetAsync(true) == StartupTaskState.Enabled);
            using (var key = Registry.CurrentUser.OpenSubKey(registry + @"\Run"))
                Check("Startup command quotes Unicode and spaced paths", key?.GetValue("X-Bar") as string == "\"C:\\Users\\시험 사용자\\Apps\\X-Bar\\XBar.exe\" --background");
            using (var key = Registry.CurrentUser.CreateSubKey(registry + @"\Approved")) key.SetValue("X-Bar", new byte[] { 3, 0, 0, 0 }, RegistryValueKind.Binary);
            Check("Windows-disabled startup is respected", await startup.ReadAsync() == StartupTaskState.DisabledByUser && await startup.SetAsync(true) == StartupTaskState.DisabledByUser);
            using (var key = Registry.CurrentUser.OpenSubKey(registry + @"\Approved")) Check("Startup never rewrites Windows approval policy", key?.GetValue("X-Bar") is byte[] bytes && bytes[0] == 3);
            Check("Disabling startup removes only the app entry", await startup.SetAsync(false) == StartupTaskState.Disabled);
        } finally { Registry.CurrentUser.DeleteSubKeyTree(registry, false); }
        Check("Desktop identity has EXE distribution and assembly version", DesktopUpdateInstaller.Identity() is { Name: "LIMOD.XBar.Desktop", Format: "exe" } identity && identity.Version == typeof(Collector).Assembly.GetName().Version);
        var root = Path.Combine(Path.GetTempPath(), "XBar-desktop-tests-" + Guid.NewGuid());
        var store = new StateStore(root); store.Write("settings.json", new AppSettings { Mode = DisplayMode.Both });
        using var collector = new Collector(store, networkAvailable: () => false);
        var window = new SettingsWindow(collector, store, _ => Task.CompletedTask, startupPreference: startup); window.Show(); window.UpdateLayout();
        static IEnumerable<DependencyObject> Tree(DependencyObject node) {
            yield return node;
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) foreach (var item in Tree(child)) yield return item;
        }
        Check("Desktop settings contain no board mode or pin guide", !Tree(window).OfType<RadioButton>().Any(x => x.GroupName == "displayMode") && !Tree(window).OfType<Expander>().Any());
        window.SaveControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(100);
        Check("Saving legacy display settings selects the desktop and keeps account preferences", store.LoadSettings().Mode == DisplayMode.Desktop && !store.LoadSettings().Connected);
        window.Close();
        // Real PE metadata fixture; actual installer execution is a separate device gate.
        var file = typeof(Collector).Assembly.Location; var version = typeof(Collector).Assembly.GetName().Version!.ToString();
        var package = new UpdatePackage("LIMOD.XBar.Desktop", "LIMOD", "x64", "https://github.com/yooongZa/xbar/releases/download/test/setup.exe", new FileInfo(file).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))), "exe");
        var release = new UpdateRelease(1, "windows", version, version, 22000, DateTimeOffset.UtcNow, ImmutableDictionary<string,string>.Empty, [package]);
        await UpdateValidation.VerifyPackageAsync(file, new(release, package), CancellationToken.None);
        Check("Desktop update validates real PE metadata without Authenticode", true);
        async Task<bool> Reject(VerifiedUpdate update) { try { await UpdateValidation.VerifyPackageAsync(file, update, CancellationToken.None); return false; } catch (Exception e) when (e is InvalidDataException or CryptographicException) { return true; } }
        Check("Desktop update rejects modified content", await Reject(new(release, package with { Sha256 = new string('0',64) })));
        Check("Desktop update rejects another product identity", await Reject(new(release, package with { Identity = "Other.App" })));
        Check("Desktop update rejects mismatched executable version", await Reject(new(release with { Version = "9.9.9.9" }, package)));
        Check("Uninstall shutdown is a local command and not a web deep link", AppCommand.Parse(["--quit"]).Action == "quit" && AppCommand.Parse(["xbar://quit"]).Action == "activate");
    }
}
