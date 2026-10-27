using System.Text.Json;

namespace XBar.Core;

public static class WidgetCard
{
    public static string Render(Snapshot s, string size, DateTimeOffset now, TimeZoneInfo zone)
    {
        string T(string key) => L10n.T(key, s.Settings.Language);
        var body = new List<object>(); var actions = new List<object>();
        object Text(string text, string font = "Default", bool subtle = false, bool bold = false) => new { type = "TextBlock", text, size = font, weight = bold ? "Bolder" : "Default", wrap = false, maxLines = 1, spacing = "None", isSubtle = subtle };
        object Action(string title, string verb, string? argument = null) => new { type = "Action.Execute", title, verb, data = new { argument } };
        object Columns(object[] left, object[] right) => new { type = "ColumnSet", spacing = "None", columns = new object[] {
            new { type = "Column", width = "stretch", items = left }, new { type = "Column", width = "auto", items = right } } };
        if (s.Settings.Mode == DisplayMode.Desktop)
        { body.Add(Text(T("disabled"))); body.Add(Text(T("unpin"), "Small", true)); }
        else
        {
            bool available = s.CanShowMetric(now, zone), postsAvailable = s.CanShowPosts(now, zone);
            var status = postsAvailable && !s.PostsCurrent && !s.IsCollecting ? T("retained") : L10n.Status(s, now, zone);
            var time = available ? s.Today!.CapturedAt.ToLocalTime().ToString("HH:mm") : "";
            body.Add(Columns([Text(T("today"), "Small", true), Text(available ? s.Today!.Display : "—", size == "Small" ? "Large" : "ExtraLarge", bold: true)],
                [Text(status, "Small", true), Text(time, "Small", true)]));
            int take = size == "Large" ? 5 : size == "Medium" ? 3 : 0;
            if (take > 0)
            {
                if (available) body.Add(Text(s.Today!.Account + " · " + T(s.Settings.Growth ? "sort.growth" : "sort.total"), "Small", true));
                body.Add(new { type = "ActionSet", spacing = "Small", actions = Enum.GetValues<ContentPeriod>().Select(p =>
                    Action((p == s.Settings.Period ? "✓ " : "") + T(p == ContentPeriod.Today ? "period.today" : p == ContentPeriod.TwoDays ? "period.short.two" : "period.short.week"), "period", p.ToString())).ToArray() });
                if (postsAvailable)
                {
                    int rank = 0;
                    foreach (var row in s.Posts.Take(take))
                    {
                        var post = row.Post; var title = post.Title == "__XBAR_MEDIA_POST__" ? T("media") : post.Title;
                        var value = s.Settings.Growth ? row.Increase is double increase ? $"{(row.Approximate ? "≈" : "")}+{increase:N0}" : T("measure") : post.Impressions;
                        var items = new List<object> { Columns([Text($"{++rank}. {title}", "Small")], [Text(value, "Small", bold: true)]) };
                        if (size == "Large") items.Add(Text($"{T("likes")} {post.Likes}  ·  {T("reposts")} {post.Reposts}" + (s.Settings.Growth ? $"  ·  {T("impressions")} {post.Impressions}" : ""), "Small", true));
                        body.Add(new { type = "Container", spacing = size == "Large" ? "Medium" : "Small", selectAction = Action(title + " · " + T("analysis"), "post", post.Id), items });
                    }
                    if (s.Posts.Length == 0) body.Add(Text(T(s.Settings.Growth ? "growth.wait" : "empty"), "Small", true));
                }
                else body.Add(Text(T(s.Detail == "content.error" ? "content.error" : "content.wait"), "Small", true));
                if (size == "Large" && s.Settings.Effects && s.Surges.Any(x => now >= x.DetectedAt && now - x.DetectedAt < TimeSpan.FromMinutes(30))) body.Add(Text(T("surge"), "Small"));
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
