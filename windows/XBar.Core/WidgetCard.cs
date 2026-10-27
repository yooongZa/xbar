using System.Text.Json;

namespace XBar.Core;

public static class WidgetCard
{
    public static string Render(Snapshot s, string size, DateTimeOffset now, TimeZoneInfo zone)
    {
        string T(string key) => L10n.T(key, s.Settings.Language);
        var body = new List<object>(); var actions = new List<object>();
        bool small = size == "Small", large = size == "Large";
        object Text(string text, string font = "Default", bool subtle = false, bool bold = false, string align = "Left", string color = "Default") =>
            new { type = "TextBlock", text, size = font, weight = bold ? "Bolder" : "Default", wrap = false, maxLines = 1, spacing = "None", isSubtle = subtle, horizontalAlignment = align, color };
        object Action(string title, string verb, string? argument = null) => new { type = "Action.Execute", title, verb, data = new { argument } };
        object Column(object[] items, string width = "stretch") => new { type = "Column", width, spacing = "Small", verticalContentAlignment = "Center", items };
        object Columns(object[] columns, string spacing = "None") => new { type = "ColumnSet", spacing, columns };
        if (s.Settings.Mode == DisplayMode.Desktop)
        { body.Add(Text(T("disabled"))); body.Add(Text(T("unpin"), "Small", true)); }
        else
        {
            bool available = s.CanShowMetric(now, zone), postsAvailable = s.CanShowPosts(now, zone);
            var status = postsAvailable && !s.PostsCurrent && !s.IsCollecting ? T("retained") : L10n.Status(s, now, zone);
            var time = available ? TimeZoneInfo.ConvertTime(s.Today!.CapturedAt, zone).ToString("HH:mm") : "";
            if (small) {
                body.Add(Columns([
                    Column([Text(T("today"), "Small", true), Text(available ? s.Today!.Display : "—", "Large", bold: true)]),
                    Column([Text(status, "Small", true, align: "Right"), Text(time, "Small", true, align: "Right")], "auto")
                ]));
            } else {
                body.Add(Text(T("today"), "Small", true));
                body.Add(Text(available ? s.Today!.Display : "—", "ExtraLarge", bold: true));
                body.Add(Columns([
                    Column([Text(available ? s.Today!.Account : status, "Small", true)]),
                    Column([Text(time, "Small", true, align: "Right")], "auto")
                ]));
                // Equal-width period choices use host theme colors and stay inside all widget widths.
                body.Add(new { type = "ColumnSet", spacing = "Medium", columns = Enum.GetValues<ContentPeriod>().Select(p => {
                    bool selected = p == s.Settings.Period;
                    var label = T(p == ContentPeriod.Today ? "period.today" : p == ContentPeriod.TwoDays ? "period.short.two" : "period.short.week");
                    return new { type = "Column", width = "stretch", spacing = "Small", style = selected ? "emphasis" : "default", minHeight = "28px", verticalContentAlignment = "Center",
                        selectAction = Action((selected ? "✓ " : "") + label, "period", p.ToString()),
                        items = new[] { Text((selected ? "✓ " : "") + label, "Small", bold: selected, align: "Center", color: selected ? "Accent" : "Default") } };
                }).ToArray() });
                body.Add(Columns([Column([Text(T("posts"), "Small", bold: true)]), Column([Text(T(s.Settings.Growth ? "sort.growth" : "sort.total"), "Small", true, align: "Right")], "auto")], "Medium"));
                if (postsAvailable) {
                    int rank = 0;
                    foreach (var row in s.Posts.Take(large ? 5 : 3)) {
                        var post = row.Post; var title = post.Title == "__XBAR_MEDIA_POST__" ? T("media") : post.Title;
                        var value = s.Settings.Growth ? row.Increase is double increase ? $"{(row.Approximate ? "≈" : "")}+{increase:N0}" : T("measure") : post.Impressions;
                        var text = new List<object> { Text(title, "Small") };
                        if (large) text.Add(Text($"{T("likes")} {post.Likes}  ·  {T("reposts")} {post.Reposts}" + (s.Settings.Growth ? $"  ·  {T("impressions")} {post.Impressions}" : ""), "Small", true));
                        body.Add(new { type = "Container", spacing = large ? "Medium" : "Small", selectAction = Action(title + " · " + T("analysis"), "post", post.Id), items = new[] {
                            Columns([Column([Text((++rank).ToString(), "Small", true)], "16px"), Column(text.ToArray()), Column([Text(value, "Small", bold: true, align: "Right", color: s.Settings.Growth ? "Accent" : "Default")], "auto")])
                        } });
                    }
                    if (s.Posts.Length == 0) body.Add(Text(T(s.Settings.Growth ? "growth.wait" : "empty"), "Small", true));
                } else body.Add(Text(T(s.Detail == "content.error" ? "content.error" : "content.wait"), "Small", true));
                var notes = new List<string>();
                if (status != T("checked") && available) notes.Add(status);
                if (large && s.Settings.Effects && s.Surges.Any(x => now >= x.DetectedAt && now - x.DetectedAt < TimeSpan.FromMinutes(30))) notes.Add(T("surge"));
                // Reserve one status line so five posts plus simultaneous notices keep the actions visible.
                if (notes.Count > 0) body.Add(Text(string.Join(" · ", notes), "Small", true));
            }
            bool login = !s.Settings.Connected || s.Status == CollectionStatus.NeedsLogin;
            actions.Add(Action(T(login ? "login" : "refresh"), login ? "login" : "refresh"));
        }
        actions.Add(Action(T("settings"), "settings"));
        return JsonSerializer.Serialize(new Dictionary<string, object> {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json", ["type"] = "AdaptiveCard", ["version"] = "1.5", ["body"] = body, ["actions"] = actions
        });
    }
}
