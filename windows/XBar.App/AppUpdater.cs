using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Threading;
using XBar.Core;

namespace XBar.App;
internal enum UpdatePhase { Idle, Checking, Available, Current, ChannelPending, Downloading, Preparing, Installing, Updated, Canceled, Error }
internal sealed class AppUpdater : IDisposable
{
    private readonly StateStore store;
    private readonly UpdateClient client;
    private readonly IUpdateInstaller installer;
    private readonly Action prepareInstall, resume;
    private readonly Func<string> language;
    private readonly UpdateConsent consent = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private CancellationTokenSource? operation;
    private UpdateWindow? window;
    private bool disposed, busy;
    public UpdateIdentity Identity { get; }
    public UpdatePreferences Preferences { get; private set; }
    public UpdatePhase Phase { get; private set; }
    public VerifiedUpdate? Available { get; private set; }
    public string ErrorKey { get; private set; } = "update.error";
    public double Progress { get; private set; }
    public bool Busy => busy;
    public bool CanCancel => Phase is UpdatePhase.Checking or UpdatePhase.Downloading or UpdatePhase.Preparing;
    public event Action? Changed;
    internal const string ProductionKey = "1B1TWAu18QVrbf5Qn7+5PHVOU3IBdq2InHZd2FH6Efo=";
    internal static UpdateSource Source()
    {
#if XBAR_UPDATER_LAB
        if (Windows.ApplicationModel.Package.Current.Id.Name != "LIMOD.XBar.UpdaterLab") throw new InvalidOperationException("Lab identity required");
        var config = JsonSerializer.Deserialize<LabSource>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpdaterLab.json")), StateStore.Json)!;
        return new(new Uri(config.Url), config.PublicKey, true);
#else
        return new(new Uri("https://yooongza.github.io/xbar/windows-updates.json"), ProductionKey);
#endif
    }
    private sealed record LabSource(string Url, string PublicKey);
    public AppUpdater(StateStore store, Func<string> language, Action prepareInstall, Action resume,
        UpdateIdentity? identity = null, UpdateClient? client = null, IUpdateInstaller? installer = null)
    {
        this.store = store; this.language = language; this.prepareInstall = prepareInstall; this.resume = resume;
        Identity = identity ?? WindowsUpdateInstaller.Identity(); this.client = client ?? new UpdateClient(Source()); this.installer = installer ?? new WindowsUpdateInstaller();
        Preferences = store.Read<UpdatePreferences>("updates.json") ?? new();
        if (store.Read<UpdateReceipt>("update-install.json") is { } receipt)
        {
            try { Phase = Identity.Version >= UpdateValidation.ParseVersion(receipt.Version) ? UpdatePhase.Updated : UpdatePhase.Error; } catch { Phase = UpdatePhase.Error; }
            DeleteReceipt();
        }
        Cleanup(); timer.Tick += async (_, _) => { if (!busy && Preferences.Due(DateTimeOffset.UtcNow)) await CheckAsync(false); };
    }
    public void Start() { timer.Start(); _ = CheckIfDueAsync(); }
    private async Task CheckIfDueAsync() { await Task.Delay(3000); if (!disposed && Preferences.Due(DateTimeOffset.UtcNow)) await CheckAsync(false); }
    public void SetAutomaticChecks(bool value)
    { Preferences = Preferences with { AutomaticChecks = value }; Save(); Notify(); }
    private void Save() => store.Write("updates.json", Preferences);
    private void Notify() { if (!disposed) { Changed?.Invoke(); window?.Refresh(); } }
    public string Status => L10n.T(Phase switch {
        UpdatePhase.Checking => "update.checking", UpdatePhase.Available => "update.available", UpdatePhase.Current => "update.current",
        UpdatePhase.ChannelPending => "update.pending", UpdatePhase.Downloading => "update.downloading", UpdatePhase.Preparing => "update.preparing",
        UpdatePhase.Installing => "update.installing", UpdatePhase.Updated => "update.updated", UpdatePhase.Canceled => "update.canceled", UpdatePhase.Error => ErrorKey, _ => "update.hint"
    }, language());
    public void Show()
    {
        if (disposed) return;
        if (window is null)
        {
            var session = consent.Begin(); window = new(this, language(), session);
            window.Closed += (_, _) => { window = null; if (CanCancel) Cancel(); consent.Invalidate(); };
        }
        window.Show(); window.Activate(); window.Refresh();
        if (!busy && (Available is null || Phase != UpdatePhase.Available)) _ = CheckAsync(true);
    }
    public async Task CheckAsync(bool manual)
    {
        if (disposed || busy) return;
        busy = true; operation?.Dispose(); operation = new(); var token = operation.Token;
        Phase = UpdatePhase.Checking; Notify();
        try
        {
            Preferences = Preferences with { LastCheck = DateTimeOffset.UtcNow }; Save();
            var value = await client.CheckAsync(Identity, DateTimeOffset.UtcNow, token);
            token.ThrowIfCancellationRequested(); if (disposed) return;
            Available = !manual && value?.Release.Version == Preferences.SkippedVersion ? null : value;
            Phase = Available is null ? UpdatePhase.Current : UpdatePhase.Available;
            if (Available is not null) window?.ResetSession();
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { Fail(new TimeoutException()); }
        catch (OperationCanceledException) { Phase = UpdatePhase.Canceled; }
        catch (UpdateChannelPendingException) { Available = null; Phase = UpdatePhase.ChannelPending; }
        catch (Exception e) { Fail(e); }
        finally { busy = false; Notify(); }
    }
    internal Guid BeginPresentation() => consent.Begin();
    public void Skip()
    {
        if (busy || Available is null) return;
        Preferences = Preferences with { SkippedVersion = Available.Release.Version }; Save(); Available = null; consent.Invalidate(); window?.Close(); Phase = UpdatePhase.Idle; Notify();
    }
    public void Cancel() { if (CanCancel) { consent.Invalidate(); operation?.Cancel(); } }
    public async Task InstallAsync(Guid session)
    {
        if (disposed || busy || Available is not { } candidate) return;
        if (!consent.Choose(session)) return;
        busy = true; operation?.Dispose(); operation = new(); var token = operation.Token;
        var directory = Path.Combine(store.Root, "Updates", Guid.NewGuid().ToString("N")); var file = Path.Combine(directory, "update.msix");
        bool prepared = false, submitted = false;
        Phase = UpdatePhase.Downloading; Progress = 0; Notify();
        try
        {
            await client.DownloadAsync(candidate, file, new Progress<double>(p => { if (!disposed && Phase == UpdatePhase.Downloading) { Progress = p; Notify(); } }), token);
            token.ThrowIfCancellationRequested(); Phase = UpdatePhase.Preparing; Notify();
            await installer.PrepareAsync(file, token);
            // Re-read the verified file immediately before handing it to the OS.
            await UpdateValidation.VerifyPackageAsync(file, candidate, token);
            if (disposed || !consent.Consume(session)) throw new OperationCanceledException();
            store.Write("update-install.json", new UpdateReceipt(candidate.Release.Version, DateTimeOffset.UtcNow));
            prepared = true; prepareInstall(); if (window is not null) window.AllowClose = true; Phase = UpdatePhase.Installing; Progress = 0; Notify();
            await installer.InstallAsync(file, new Progress<double>(p => { Progress = p; Notify(); })); submitted = true;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { consent.Invalidate(); Fail(new TimeoutException()); }
        catch (OperationCanceledException) { consent.Invalidate(); Phase = UpdatePhase.Canceled; }
        catch (Exception e) { consent.Invalidate(); Fail(e); }
        finally
        {
            if (!submitted)
            {
                DeleteReceipt();
                if (window is not null) window.AllowClose = false;
                if (prepared && !disposed) resume();
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                busy = false; Notify();
            }
        }
    }
    private void Fail(Exception e)
    {
        Available = null; Phase = UpdatePhase.Error;
        ErrorKey = e is CryptographicException or InvalidDataException or FormatException or JsonException ? "update.verify.error" : e is HttpRequestException or TimeoutException ? "update.network.error" : "update.error";
    }
    private void Cleanup()
    {
        try {
        var path = Path.Combine(store.Root, "Updates"); if (!Directory.Exists(path)) return;
        foreach (var dir in Directory.GetDirectories(path))
            if (Guid.TryParseExact(Path.GetFileName(dir), "N", out _) && (File.GetAttributes(dir) & FileAttributes.ReparsePoint) == 0)
                try { Directory.Delete(dir, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
    private void DeleteReceipt() { try { File.Delete(Path.Combine(store.Root, "update-install.json")); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    public void Dispose()
    { if (disposed) return; disposed = true; timer.Stop(); consent.Invalidate(); operation?.Cancel(); operation?.Dispose(); client.Dispose(); if (window is not null) { window.AllowClose = true; window.Close(); } }
}
