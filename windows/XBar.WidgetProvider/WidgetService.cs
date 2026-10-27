using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Windows.Widgets.Providers;
using XBar.Core;

namespace XBar.WidgetProvider;

internal sealed class WidgetService : IWidgetProvider
{
    private sealed record Widget(string Id, string Size, bool Active);
    private readonly ConcurrentDictionary<string, Widget> widgets = new();
    private readonly StateStore store = new(StateStore.DefaultRoot);
    private readonly Timer timer;
    private int updating;
    public static readonly ManualResetEventSlim Stopped = new();
    private static readonly Lazy<WidgetService> Shared = new(() => new WidgetService());
    public static WidgetService Instance => Shared.Value;
    private WidgetService()
    {
        foreach (var info in ReadWidgets())
        { var context = info.WidgetContext; widgets[context.Id] = new(context.Id, context.Size.ToString(), true); }
        SaveRegistration();
        timer = new(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }
    public static void Probe(StateStore store)
    {
        var infos = ReadWidgets();
        store.Write("registration.json", new WidgetRegistration(infos.Length, DateTimeOffset.UtcNow));
    }
    private static WidgetInfo[] ReadWidgets()
    {
        var manager = WidgetManager.GetDefault() ?? throw new InvalidOperationException("Widget manager unavailable");
        // WinRT can project an empty native array as null before the first widget is pinned.
        return manager.GetWidgetInfos() ?? [];
    }
    private void SaveRegistration() => store.Write("registration.json", new WidgetRegistration(widgets.Count, DateTimeOffset.UtcNow));
    private void Tick()
    {
        if (Interlocked.Exchange(ref updating, 1) != 0) return;
        try { foreach (var widget in widgets.Values) if (widget.Active) Update(widget); SaveRegistration(); }
        catch (Exception e) { store.Write("registration.json", new WidgetRegistration(widgets.Count, DateTimeOffset.UtcNow, e.GetType().Name)); }
        finally { Interlocked.Exchange(ref updating, 0); }
    }
    private void Update(Widget widget)
    {
        var snapshot = store.LoadSnapshot();
        var template = WidgetCard.Render(snapshot, widget.Size, DateTimeOffset.UtcNow, TimeZoneInfo.Local);
        WidgetManager.GetDefault().UpdateWidget(new(widget.Id) { Template = template, Data = "{}", CustomState = "1" });
    }
    public void CreateWidget(WidgetContext context)
    { var widget = new Widget(context.Id, context.Size.ToString(), true); widgets[widget.Id] = widget; SaveRegistration(); Update(widget); }
    public void DeleteWidget(string id, string customState)
    { widgets.TryRemove(id, out _); SaveRegistration(); if (widgets.IsEmpty) { timer.Dispose(); Stopped.Set(); } }
    public void OnWidgetContextChanged(WidgetContextChangedArgs args)
    { var context = args.WidgetContext; var widget = new Widget(context.Id, context.Size.ToString(), true); widgets[widget.Id] = widget; Update(widget); }
    public void Activate(WidgetContext context)
    { var widget = new Widget(context.Id, context.Size.ToString(), true); widgets[widget.Id] = widget; Update(widget); }
    public void Deactivate(string id)
    { if (widgets.TryGetValue(id, out var widget)) widgets[id] = widget with { Active = false }; }
    public void OnActionInvoked(WidgetActionInvokedArgs args)
    {
        // Callback objects are valid only inside this invocation; copy plain values before awaiting.
        var verb = args.Verb; var data = args.Data; string? argument = null;
        try { if (!string.IsNullOrEmpty(data)) { using var doc = JsonDocument.Parse(data); if (doc.RootElement.TryGetProperty("argument", out var value) && value.ValueKind == JsonValueKind.String) argument = value.GetString(); } }
        catch (JsonException) { return; }
        var command = new AppCommand(verb, argument);
        if (command.IsValid) _ = DispatchAsync(command);
    }
    private static async Task DispatchAsync(AppCommand command)
    {
        try { if (!await CommandPipe.SendAsync(command)) await Windows.System.Launcher.LaunchUriAsync(new(command.ToUri())); }
        catch (Exception) { /* Host will retain the card; the next explicit action can retry. */ }
    }
}
