using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using XBar.Core;
namespace XBar.App;
internal sealed class UpdateWindow : Window
{
    private readonly AppUpdater updater;
    private readonly string language;
    private Guid session;
    private readonly TextBlock heading = UI.Text("", 24), status = UI.Text("", 13, "Muted"), notes = UI.Text("", 13), detail = UI.Text("", 12, "Muted");
    private readonly ProgressBar progress = new() { Height = 4, Margin = new(0, 16, 0, 12), Minimum = 0, Maximum = 1 };
    private readonly Button install, skip, close, check;
    internal Button InstallControl => install;
    public bool AllowClose;
    public UpdateWindow(AppUpdater updater, string language, Guid session)
    {
        this.updater = updater; this.language = language; this.session = session; Theme.Install(); Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "X-Bar · " + T("update.title"); Width = 500; Height = 460; MinWidth = 420; MinHeight = 360; SourceInitialized += (_, _) => Theme.ApplyFrame(this);
        var root = new DockPanel { Margin = new(24) }; Content = root;
        var top = new StackPanel(); heading.FontWeight = FontWeights.SemiBold; top.Children.Add(heading); top.Children.Add(UI.Text("X-Bar " + updater.Identity.Version.ToString(3), 12, "Muted")); status.Margin = new(0, 16, 0, 0); top.Children.Add(status); top.Children.Add(progress); top.Children.Add(detail); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 20, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        skip = UI.Button(T("update.skip"), updater.Skip); close = UI.Button(T("close"), Close); close.IsCancel = true; close.Margin = new(6, 0, 6, 0);
        install = UI.Button(T("update.install"), () => _ = updater.InstallAsync(this.session), "XBar.Primary"); install.IsDefault = true; check = UI.Button(T("update.check"), () => _ = updater.CheckAsync(true)); footer.Children.Add(check); footer.Children.Add(skip); footer.Children.Add(close); footer.Children.Add(install);
        notes.Margin = new(0, 18, 0, 0); root.Children.Add(new ScrollViewer { Content = notes, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Closing += (_, e) => { if (!AllowClose && updater.Phase == UpdatePhase.Installing) e.Cancel = true; else if (updater.CanCancel) updater.Cancel(); };
        Refresh();
    }
    public void ResetSession() => session = updater.BeginPresentation();
    private string T(string key) => L10n.T(key, language);
    public void Refresh()
    {
        heading.Text = updater.Available is { } update ? "X-Bar " + update.Release.DisplayVersion : T("update.title");
        status.Text = updater.Status; AutomationProperties.SetName(status, updater.Status);
        notes.Text = updater.Available?.Notes(language) ?? "";
        detail.Text = updater.Phase == UpdatePhase.Available ? T("update.restart.hint") : updater.Phase == UpdatePhase.Preparing ? T("update.prepare.hint") : "";
        progress.Visibility = updater.Busy ? Visibility.Visible : Visibility.Collapsed; progress.IsIndeterminate = updater.Phase is UpdatePhase.Checking or UpdatePhase.Preparing; progress.Value = updater.Progress;
        install.Visibility = skip.Visibility = updater.Available is not null && updater.Phase == UpdatePhase.Available ? Visibility.Visible : Visibility.Collapsed;
        check.Visibility = !updater.Busy && updater.Phase != UpdatePhase.Available ? Visibility.Visible : Visibility.Collapsed;
        close.IsEnabled = updater.Phase != UpdatePhase.Installing; close.Content = T(updater.CanCancel ? "update.cancel" : updater.Phase == UpdatePhase.Available ? "update.later" : "close");
        UI.Label(close, close.Content.ToString()!);
    }
}
