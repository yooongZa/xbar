using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using XBar.Core;

namespace XBar.App;

internal static class UI
{
    public static Button Button(string text, Action action, string style = "XBar.Button")
    {
        var button = new Button { Content = text, Style = (Style)Application.Current.FindResource(style), HorizontalContentAlignment = HorizontalAlignment.Center };
        AutomationProperties.SetName(button, text); button.Click += (_, _) => action(); return button;
    }
    public static TextBlock Text(string text, double size = 14, string color = "Text")
    { var value = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap }; value.SetResourceReference(TextBlock.ForegroundProperty, "XBar." + color); return value; }
    public static void Label(Button button, string text) { AutomationProperties.SetName(button, text); button.ToolTip = text; }
    public static void Brush(FrameworkElement element, DependencyProperty property, string name) => element.SetResourceReference(property, "XBar." + name);
    public static Border Surface(UIElement child, Thickness padding, string color = "Surface")
    { var border = new Border { Child = child, CornerRadius = new(12), Padding = padding }; Brush(border, Border.BackgroundProperty, color); return border; }
    public static ComboBox Choice(IEnumerable<string> labels, int selected, string name)
    { var combo = new ComboBox { MinWidth = 140, MinHeight = 34, Padding = new(8, 4, 8, 4) }; AutomationProperties.SetName(combo, name); foreach (var label in labels) combo.Items.Add(label); combo.SelectedIndex = selected; return combo; }
    public static RadioButton Segment(string label, string group)
    { var b = new RadioButton { Content = label, GroupName = group, Style = (Style)Application.Current.FindResource("XBar.Segment") }; AutomationProperties.SetName(b, label); return b; }
    public static FrameworkElement Icon(string kind)
    {
        var geometry = kind switch {
            "refresh" => "M 13 6 A 5 5 0 1 0 13 11 M 13 2 L 13 6 L 9 6",
            "more" => "M 3 7 L 3 9 M 8 7 L 8 9 M 13 7 L 13 9",
            "down" => "M 4 6 L 8 10 L 12 6", "up" => "M 4 10 L 8 6 L 12 10",
            "heart" => "M 8 13 C -3 6 3 -1 8 4 C 13 -1 19 6 8 13",
            "repost" => "M 2 6 L 2 3 L 12 3 M 9 0 L 12 3 L 9 6 M 14 10 L 14 13 L 4 13 M 7 10 L 4 13 L 7 16",
            "chart" => "M 2 13 L 2 8 M 7 13 L 7 3 M 12 13 L 12 5",
            _ => "M 2 2 L 14 14 M 3 2 L 14 13 M 14 2 L 2 14"
        };
        var path = new Path { Data = Geometry.Parse(geometry), StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = 16, Height = 16, Stretch = Stretch.Uniform, IsHitTestVisible = false };
        path.SetBinding(Shape.StrokeProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Control), 1) }); return path;
    }
    public static Button IconButton(string icon, Action action)
    { var b = Button("", action); b.Content = Icon(icon); b.Width = 34; b.Height = 34; b.Padding = new(8); return b; }
}

internal sealed class DesktopWidget : Window
{
    private readonly Collector collector;
    private readonly Func<AppCommand, Task> execute;
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly TextBlock title = UI.Text("X-Bar", 15), metric = UI.Text("—", 42), metricLabel = UI.Text("", 12, "Muted"), account = UI.Text("", 12, "Muted"), status = UI.Text("", 12, "Muted"), postsLabel = UI.Text("", 13), sortLabel = UI.Text("", 11, "Muted"), empty = UI.Text("", 13, "Muted"), surge = UI.Text("", 11, "Accent");
    private readonly Button refresh, menu, collapse, connect;
    private readonly Grid root = new(), postsArea = new();
    private readonly StackPanel rowsPanel = new();
    private readonly ScrollViewer scroller;
    private readonly RadioButton[] periods;
    private readonly Dictionary<string, PostRow> rows = new();
    private bool rendering, expanded = true;
    public bool AllowClose;
    public event Action? HideRequested;
    internal ScrollViewer PostsScroller => scroller;
    internal Button RefreshControl => refresh;
    internal Button ConnectControl => connect;
    internal IEnumerable<RadioButton> PeriodControls => periods;
    internal IReadOnlyCollection<PostRow> PostControls => rows.Values;
    public DesktopWidget(Collector collector, Func<AppCommand, Task> execute)
    {
        Theme.Install(); Style = (Style)Application.Current.FindResource(typeof(Window)); this.collector = collector; this.execute = execute;
        Title = "X-Bar"; MinWidth = 320; MinHeight = 210; Width = Math.Max(320, collector.Settings.Bounds.Width); Height = collector.Settings.Bounds.Height;
        Left = collector.Settings.Bounds.Left; Top = collector.Settings.Bounds.Top;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize; ShowActivated = false; ShowInTaskbar = false;
        WindowChrome.SetWindowChrome(this, new() { CaptionHeight = 0, ResizeBorderThickness = new(6), GlassFrameThickness = new(0), CornerRadius = new(12) });
        SourceInitialized += (_, _) => { Theme.ApplyFrame(this); KeepOnScreen(); };
        root.Margin = new(20, 12, 20, 12);
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) root.RowDefinitions.Add(new() { Height = height });
        Content = root;
        var header = new DockPanel { Margin = new(0, 0, 0, 12), LastChildFill = true, Background = Brushes.Transparent };
        refresh = UI.IconButton("refresh", () => _ = execute(new("refresh"))); menu = UI.IconButton("more", ShowMenu);
        DockPanel.SetDock(menu, Dock.Right); header.Children.Add(menu); DockPanel.SetDock(refresh, Dock.Right); header.Children.Add(refresh);
        title.FontWeight = FontWeights.SemiBold; title.VerticalAlignment = VerticalAlignment.Center; header.Children.Add(title);
        header.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is TextBlock || ReferenceEquals(e.OriginalSource, header)) { if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed) DragMove(); } };
        root.Children.Add(header);
        var hero = new StackPanel { Margin = new(0, 0, 0, 14) }; Grid.SetRow(hero, 1); root.Children.Add(hero);
        hero.Children.Add(metricLabel); metric.FontWeight = FontWeights.SemiBold; metric.Margin = new(0, 2, 0, 1); hero.Children.Add(metric);
        account.TextTrimming = TextTrimming.CharacterEllipsis; account.TextWrapping = TextWrapping.NoWrap; hero.Children.Add(account);
        connect = UI.Button("", () => _ = execute(new("login")), "XBar.Primary"); connect.HorizontalAlignment = HorizontalAlignment.Left; connect.Margin = new(0, 10, 0, 0); hero.Children.Add(connect);
        postsArea.RowDefinitions.Add(new() { Height = GridLength.Auto }); postsArea.RowDefinitions.Add(new() { Height = GridLength.Auto }); postsArea.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        Grid.SetRow(postsArea, 2); root.Children.Add(postsArea);
        var tabs = new UniformGrid { Rows = 1, Columns = 3, Margin = new(2) }; periods = Enum.GetValues<ContentPeriod>().Select(p => { var b = UI.Segment("", "period"); b.Checked += (_, _) => { if (!rendering) collector.UpdateSettings(collector.Settings with { Period = p }); }; tabs.Children.Add(b); return b; }).ToArray();
        var tabsBorder = UI.Surface(tabs, new(0), "Hover"); tabsBorder.CornerRadius = new(9); postsArea.Children.Add(tabsBorder);
        var listHeader = new DockPanel { Margin = new(0, 14, 0, 7) }; Grid.SetRow(listHeader, 1); postsArea.Children.Add(listHeader);
        collapse = UI.IconButton("up", () => collector.UpdateSettings(collector.Settings with { ShowPosts = !collector.Settings.ShowPosts })); collapse.Width = collapse.Height = 26; collapse.MinHeight = 26; DockPanel.SetDock(collapse, Dock.Right); listHeader.Children.Add(collapse);
        sortLabel.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(sortLabel, Dock.Right); listHeader.Children.Add(sortLabel); postsLabel.FontWeight = FontWeights.SemiBold; listHeader.Children.Add(postsLabel);
        var contents = new StackPanel(); contents.Children.Add(rowsPanel); empty.Margin = new(0, 14, 0, 14); contents.Children.Add(empty);
        scroller = new ScrollViewer { Content = contents, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, PanningMode = PanningMode.VerticalOnly, CanContentScroll = false }; Grid.SetRow(scroller, 2); postsArea.Children.Add(scroller);
        var footer = new DockPanel { Margin = new(0, 12, 0, 0) }; Grid.SetRow(footer, 3); root.Children.Add(footer);
        surge.Margin = new(8, 0, 0, 0); DockPanel.SetDock(surge, Dock.Right); footer.Children.Add(surge); status.TextTrimming = TextTrimming.CharacterEllipsis; status.TextWrapping = TextWrapping.NoWrap; footer.Children.Add(status);
        InputBindings.Add(new KeyBinding(new UICommand(() => _ = execute(new("refresh"))), Key.F5, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new UICommand(() => _ = execute(new("settings"))), Key.OemComma, ModifierKeys.Control));
        Closing += (_, e) => { if (!AllowClose) { e.Cancel = true; HideRequested?.Invoke(); } };
        Closed += (_, _) => { saveTimer.Stop(); };
        LocationChanged += (_, _) => ScheduleSave(); SizeChanged += (_, _) => ScheduleSave();
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); if (!AllowClose) collector.UpdateSettings(collector.Settings with { Bounds = new(Left, Top, Width, collector.Settings.ShowPosts ? Height : collector.Settings.Bounds.Height) }); };
        Refresh();
    }
    private void ScheduleSave() { if (!rendering && IsVisible) { saveTimer.Stop(); saveTimer.Start(); } }
    public void KeepOnScreen() => MonitorAreas.KeepVisible(this);
    private void ShowMenu()
    {
        var cm = new ContextMenu();
        foreach (var item in new[] { ("settings", "settings"), ("update.title", "updates"), ("analysis", "analysis"), (collector.Settings.ShowPosts ? "collapse" : "expand", "toggle"), ("account.change", "login"), ("hide", "hide"), ("quit", "quit") })
        { var mi = new MenuItem { Header = L10n.T(item.Item1, collector.Settings.Language) }; mi.Click += async (_, _) => { if (item.Item2 == "hide") HideRequested?.Invoke(); else if (item.Item2 == "toggle") collector.UpdateSettings(collector.Settings with { ShowPosts = !collector.Settings.ShowPosts }); else await execute(new(item.Item2)); }; cm.Items.Add(mi); }
        cm.PlacementTarget = menu; cm.Placement = PlacementMode.Bottom; cm.IsOpen = true;
    }
    public void Refresh()
    {
        var s = collector.State; var now = DateTimeOffset.UtcNow; var zone = TimeZoneInfo.Local; string T(string key) => L10n.T(key, s.Settings.Language);
        rendering = true;
        try {
            Topmost = s.Settings.AlwaysOnTop; metricLabel.Text = T("today"); metric.Text = s.CanShowMetric(now, zone) ? s.Today!.Display : "—";
            AutomationProperties.SetName(metric, T("today") + " " + metric.Text);
            account.Text = s.Today is not null && s.CanShowMetric(now, zone) ? s.Today.Account + " · " + s.Today.CapturedAt.ToLocalTime().ToString("HH:mm") : T(s.Settings.Connected ? "loading" : "disconnected");
            account.ToolTip = s.Today?.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            UI.Label(refresh, T("refresh") + " (F5)"); UI.Label(menu, T("settings")); UI.Label(collapse, T("collapse"));
            connect.Content = T("login"); UI.Label(connect, T("login")); connect.Visibility = !s.Settings.Connected || s.Status == CollectionStatus.NeedsLogin ? Visibility.Visible : Visibility.Collapsed;
            postsLabel.Text = T("posts"); postsLabel.ToolTip = T("scope.hint"); sortLabel.Text = T(s.Settings.Growth ? "sort.growth" : "sort.total");
            for (int i = 0; i < periods.Length; i++) { var text = T(i == 0 ? "period.today" : i == 1 ? "period.short.two" : "period.short.week"); periods[i].Content = text; AutomationProperties.SetName(periods[i], T(L10n.PeriodKey((ContentPeriod)i))); periods[i].IsChecked = i == (int)s.Settings.Period; }
            postsArea.Visibility = s.Settings.ShowPosts ? Visibility.Visible : Visibility.Collapsed;
            var compactHeight = connect.Visibility == Visibility.Visible ? 260 : 210;
            if (expanded != s.Settings.ShowPosts) { expanded = s.Settings.ShowPosts; MinHeight = expanded ? 330 : compactHeight; Height = expanded ? Math.Max(330, s.Settings.Bounds.Height) : compactHeight; }
            else { MinHeight = expanded ? 330 : compactHeight; if (!expanded) Height = compactHeight; }
            var visible = s.CanShowPosts(now, zone); var desired = visible ? s.Posts : [];
            var focused = Keyboard.FocusedElement as DependencyObject;
            foreach (var id in rows.Keys.Where(id => !desired.Any(row => row.Post.Id == id)).ToArray()) { rowsPanel.Children.Remove(rows[id]); rows.Remove(id); }
            for (int i = 0; i < desired.Length; i++) {
                var row = desired[i]; if (!rows.TryGetValue(row.Post.Id, out var control)) { control = new(row.Post.Id, execute); rows[row.Post.Id] = control; }
                control.Update(row, i + 1, s.Settings);
                if (rowsPanel.Children.IndexOf(control) != i) { rowsPanel.Children.Remove(control); rowsPanel.Children.Insert(i, control); }
            }
            if (focused is UIElement input && input.IsVisible && rows.Values.Any(row => ReferenceEquals(row, input) || row.IsAncestorOf(input))) input.Focus();
            empty.Visibility = desired.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            empty.Text = T(!s.Settings.Connected ? "disconnected" : s.Status == CollectionStatus.NeedsLogin ? "needslogin" : s.Settings.Growth && s.PostsAt is not null && visible ? "growth.wait" : visible && s.PostsCurrent ? "empty" : s.Detail == "content.error" ? "content.error" : "content.wait");
            status.Text = L10n.Status(s, now, zone); status.ToolTip = status.Text + (s.PostsAt is { } at ? " · " + at.ToLocalTime().ToString("HH:mm") : "");
            if (visible && !s.PostsCurrent && !s.IsCollecting) status.Text = T("retained") + " · " + status.Text;
            surge.Text = T("surge"); surge.Visibility = s.Settings.Effects && s.Surges.Any(x => now >= x.DetectedAt && now - x.DetectedAt < TimeSpan.FromMinutes(30)) ? Visibility.Visible : Visibility.Collapsed;
        } finally { rendering = false; }
    }
}
internal sealed class UICommand(Action run) : ICommand
{ public bool CanExecute(object? parameter) => true; public void Execute(object? parameter) => run(); public event EventHandler? CanExecuteChanged { add { } remove { } } }

internal sealed class PostRow : Button
{
    private readonly TextBlock rank = UI.Text("", 11, "Muted"), title = UI.Text("", 13), value = UI.Text("", 15), metrics = UI.Text("", 11, "Muted");
    public string PostId { get; }
    public PostRow(string id, Func<AppCommand, Task> execute)
    {
        PostId = id; Style = (Style)Application.Current.FindResource("XBar.Button"); Padding = new(8, 9, 8, 9); Margin = new(-8, 0, -1, 2); HorizontalContentAlignment = HorizontalAlignment.Stretch;
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new(22) }); grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        rank.Margin = new(0, 2, 4, 0); grid.Children.Add(rank); title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis; title.Margin = new(0, 0, 8, 5); Grid.SetColumn(title, 1); grid.Children.Add(title);
        value.FontWeight = FontWeights.SemiBold; Grid.SetColumn(value, 2); grid.Children.Add(value);
        Grid.SetRow(metrics, 1); Grid.SetColumn(metrics, 1); Grid.SetColumnSpan(metrics, 2); grid.Children.Add(metrics); Content = grid;
        Click += (_, _) => _ = execute(new("post", PostId));
    }
    public void Update(RankedPost row, int position, AppSettings settings)
    {
        string T(string key) => L10n.T(key, settings.Language); var post = row.Post;
        rank.Text = position.ToString(CultureInfo.InvariantCulture); title.Text = post.Title == "__XBAR_MEDIA_POST__" ? T("media") : post.Title; ToolTip = title.Text;
        value.Text = settings.Growth ? row.Increase is double increase ? $"{(row.Approximate ? "≈" : "")}+{increase:N0}" : T("measure") : post.Impressions;
        value.SetResourceReference(TextBlock.ForegroundProperty, settings.Growth ? "XBar.Accent" : "XBar.Text");
        metrics.Text = (settings.Growth ? T("impressions") + " " + post.Impressions + "   ·   " : "") + T("likes") + " " + post.Likes + "   ·   " + T("reposts") + " " + post.Reposts;
        AutomationProperties.SetName(this, $"{position}. {title.Text}. {T("impressions")} {post.Impressions}, {T("likes")} {post.Likes}, {T("reposts")} {post.Reposts}" + (settings.Growth ? ", " + value.Text : ""));
    }
}
