using System.Text.Json;
using System.Text.Json.Serialization;

namespace XBar.Core;

public sealed class StateStore(string root)
{
    public string Root { get; } = root;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false, Converters = { new JsonStringEnumConverter() } };
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppContext.GetData("XBar.UpdaterLab") is true ? "XBar.UpdaterLab" : "XBar");
    public T? Read<T>(string name)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                using var stream = new FileStream(Path.Combine(Root, name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length > 1_048_576) return default;
                return JsonSerializer.Deserialize<T>(stream, Json);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { if (attempt < 5) Thread.Sleep(2 << attempt); }
            catch (JsonException) { return default; }
        }
        return default;
    }
    public void Write<T>(string name, T value)
    {
        Directory.CreateDirectory(Root);
        var target = Path.Combine(Root, name); var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { JsonSerializer.Serialize(stream, value, Json); stream.Flush(true); }
            // ReplaceFile preserves open readers on Windows. MoveFileEx(overwrite) can
            // fail with ACCESS_DENIED while another process still holds the old file.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(target)) File.Replace(temp, target, null);
                    else File.Move(temp, target);
                    break;
                }
                catch (Exception e) when (attempt < 5 && e is IOException or UnauthorizedAccessException)
                { Thread.Sleep(2 << attempt); }
            }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public AppSettings LoadSettings() => (Read<AppSettings>("settings.json") ?? new()).Normalize();
    public Snapshot LoadSnapshot()
    {
        var value = Read<Snapshot>("snapshot.json");
        if (value is not { Schema: 1 } || value.Settings is null || value.Posts.IsDefault || value.Surges.IsDefault || value.Posts.Length > 20 || value.Posts.Any(p => p?.Post is null || !p.Post.IsValid || p.Increase is double increase && (!double.IsFinite(increase) || increase < 0)) || value.Surges.Length > 100 || value.Surges.Any(s => s?.Post is null || !s.Post.IsValid || s.Delta < 0 || s.Minutes <= 0)) return new();
        if (value.Today is { } today && (!MetricRecord.ValidAccount(today.Account) || !MetricRecord.ValidDisplay(today.Display))) return new();
        return value with { Settings = value.Settings.Normalize() };
    }
}
