using Xunit;
namespace XBar.Core.Tests;
public sealed class UpdateDownloadMetricsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    [Fact] public async Task RetryPreservesEventAndNewVersionQueuedInFlightSurvives()
    {
        var store = new StateStore(root);
        var sent = new List<UpdateDownloadMetrics.Event>();
        using (var offline = new UpdateDownloadMetrics(store, e => { sent.Add(e); return Task.FromResult(false); })) {
            offline.Record("0.4.5.0", "0.4.6.0"); await offline.FlushAsync();
        }
        var original = sent[0]; var started = new TaskCompletionSource(); var release = new TaskCompletionSource();
        using var active = new UpdateDownloadMetrics(store, async e => {
            sent.Add(e);
            if (e.ToVersion == "0.4.6.0") { started.TrySetResult(); await release.Task; }
            return true;
        });
        var flushing = active.FlushAsync(); await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        active.Record("0.4.6.0", "0.4.7.0"); release.SetResult(); await flushing;
        Assert.Equal(original, sent[^2]); Assert.Equal("0.4.7.0", sent[^1].ToVersion);
        Assert.Empty(store.Read<UpdateDownloadMetrics.State>("update-metrics.json")!.Pending);
        int count = sent.Count;
        active.Record("0.4.5.0", "0.4.6.0"); active.Record("0.4.6.0", "0.4.6.0"); active.Record("0.4.7.0", "0.4.6.0");
        await active.FlushAsync(); Assert.Equal(count, sent.Count);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
