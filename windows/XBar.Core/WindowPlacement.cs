namespace XBar.Core;
public readonly record struct PixelBounds(int X, int Y, int Width, int Height);
public static class WindowPlacement
{
    public static PixelBounds EnsureVisible(PixelBounds window, IReadOnlyList<PixelBounds> workAreas)
    {
        if (workAreas.Count == 0) return window;
        // Keep the title/drag region reachable, not merely a sliver of the window body.
        foreach (var area in workAreas)
            if (Math.Min(window.X + window.Width, area.X + area.Width) - Math.Max(window.X, area.X) >= Math.Min(100, window.Width) &&
                window.Y >= area.Y && window.Y + Math.Min(40, window.Height) <= area.Y + area.Height) return window;
        var target = workAreas.OrderBy(a => Math.Abs((long)a.X - window.X) + Math.Abs((long)a.Y - window.Y)).First();
        int margin = Math.Min(24, Math.Min(target.Width, target.Height) / 10);
        return new(target.X + margin, target.Y + margin, Math.Min(window.Width, target.Width - margin * 2), Math.Min(window.Height, target.Height - margin * 2));
    }
}
