using System.Net;
using System.Net.Http.Json;

namespace XBar.Core;

// Separate from X account/profile state. Never receives cookies, account names or post data.
public sealed class UpdateDownloadMetrics : IDisposable
{
    public sealed record Event(int Version, string EventId, string InstallationId, string Platform, string FromVersion, string ToVersion);
    public sealed record State(string InstallationId, Event[] Pending, string[] Recorded);
    private readonly StateStore store;
    private readonly Func<Event, Task<bool>> send;
    private readonly HttpClient? client;
    private readonly object gate = new();
    private State state;
    private bool sending, disposed;
    public UpdateDownloadMetrics(StateStore store, Func<Event, Task<bool>>? send = null)
    {
        this.store = store;
        var saved = store.Read<State>("update-metrics.json");
        state = saved is { Pending: not null, Recorded: not null } && Guid.TryParse(saved.InstallationId, out _) ? saved : new(Guid.NewGuid().ToString(), [], []);
        if (send is not null) this.send = send;
        else {
            client = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, UseDefaultCredentials = false }) { Timeout = TimeSpan.FromSeconds(5) };
            this.send = async value => {
                using var response = await client.PostAsJsonAsync("https://stats.lazinessisthemotherof.dev/app/xbar/update-download", value, StateStore.Json);
                return response.StatusCode == HttpStatusCode.NoContent;
            };
        }
    }
    public void Record(string fromVersion, string toVersion)
    {
        lock (gate) {
            if (disposed || !System.Version.TryParse(fromVersion, out var from) || !System.Version.TryParse(toVersion, out var to) || to <= from || state.Recorded.Contains(toVersion) || state.Pending.Any(e => e.ToVersion == toVersion)) return;
            var next = state with { Pending = [..state.Pending, new(1, Guid.NewGuid().ToString(), state.InstallationId, "windows", fromVersion, toVersion)] };
            if (!Save(next)) return;
        }
        _ = FlushAsync();
    }
    public async Task FlushAsync()
    {
        lock (gate) { if (sending || disposed) return; sending = true; }
        try {
            while (true) {
                Event? item;
                lock (gate) { if (disposed) return; item = state.Pending.FirstOrDefault(); }
                if (item is null || !await send(item)) return;
                lock (gate) {
                    // A newer event may have been queued while delivery was awaiting a response.
                    var next = state with { Pending = state.Pending.Where(e => e.EventId != item.EventId).ToArray(), Recorded = [..state.Recorded, item.ToVersion] };
                    if (!Save(next)) return;
                }
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or ObjectDisposedException) { }
        finally { lock (gate) { sending = false; } }
    }
    private bool Save(State next)
    {
        try { store.Write("update-metrics.json", next); state = next; return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
    public void Dispose() { lock (gate) { disposed = true; } client?.Dispose(); }
}
