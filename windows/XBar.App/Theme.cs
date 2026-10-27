using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace XBar.App;

internal static class Theme
{
    private static bool installed;
    internal static string? TestMode { get; set; }
    internal static bool? TestHighContrast { get; set; }
    public static bool Dark { get; private set; }
    public static void Install()
    {
        if (installed) return;
        installed = true;
        var resources = Application.Current.Resources;
        resources.MergedDictionaries.Add(new() { Source = new("pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml") });
        resources.MergedDictionaries.Add(new() { Source = new("pack://application:,,,/XBar;component/Themes/Controls.xaml") });
        Apply();
        SystemEvents.UserPreferenceChanged += PreferencesChanged;
        SystemParameters.StaticPropertyChanged += SystemChanged;
        Application.Current.Exit += (_, _) => { SystemEvents.UserPreferenceChanged -= PreferencesChanged; SystemParameters.StaticPropertyChanged -= SystemChanged; };
    }
    private static void PreferencesChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatch();
    private static void SystemChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(SystemParameters.HighContrast)) Dispatch(); }
    private static void Dispatch()
    { if (Application.Current is { Dispatcher.HasShutdownStarted: false } app) app.Dispatcher.BeginInvoke(Apply); }
    internal static void Apply()
    {
        bool light = true;
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); light = key?.GetValue("AppsUseLightTheme") is not int value || value != 0; } catch (System.Security.SecurityException) { }
        Dark = TestMode == "dark" || TestMode != "light" && !light;
        var values = Dark
            ? new[] { "#171B21", "#20262E", "#F3F6FA", "#A9B5C4", "#343D49", "#2B3440", "#183D5B", "#8CCBFF", "#8CCBFF" }
            : new[] { "#F7F9FC", "#FFFFFF", "#18232F", "#536579", "#DDE4EC", "#EBF0F6", "#E0EFFC", "#005FA8", "#005FA8" };
        var keys = new[] { "Canvas", "Surface", "Text", "Muted", "Border", "Hover", "Selected", "Accent", "OnSelected" };
        for (int i = 0; i < keys.Length; i++)
        {
            Brush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));
            if (TestHighContrast ?? SystemParameters.HighContrast) brush = i switch { 0 or 1 or 5 => SystemColors.WindowBrush, 6 => SystemColors.HighlightBrush, 8 => SystemColors.HighlightTextBrush, _ => SystemColors.WindowTextBrush };
            if (brush.CanFreeze) brush.Freeze(); Application.Current.Resources["XBar." + keys[i]] = brush;
        }
        foreach (Window window in Application.Current.Windows) ApplyFrame(window);
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    internal static void ApplyFrame(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        // These small static WPF surfaces need reliable first frames more than GPU effects.
        // SIYONG lost its first hardware-rendered frame until resize; WebView2 keeps its own renderer.
        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target) target.RenderMode = RenderMode.SoftwareOnly;
        int dark = Dark ? 1 : 0, rounded = 2;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
    }
}
