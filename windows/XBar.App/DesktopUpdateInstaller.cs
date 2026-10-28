using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using XBar.Core;

namespace XBar.App;

internal sealed class DesktopUpdateInstaller : IUpdateInstaller
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterApplicationRestart(string commandLine, int flags);
    [DllImport("kernel32.dll")] private static extern int UnregisterApplicationRestart();
    public async Task PrepareAsync(string file, CancellationToken token)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Installer not found");
        // Inno Setup uses Restart Manager. Windows only restarts an app after 60 seconds.
        var remaining = TimeSpan.FromSeconds(65) - (DateTime.Now - Process.GetCurrentProcess().StartTime);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, token);
        token.ThrowIfCancellationRequested();
    }
    public async Task InstallAsync(string file, IProgress<double> progress)
    {
        Marshal.ThrowExceptionForHR(RegisterApplicationRestart("--background", 1 | 2 | 8));
        try {
            var start = new ProcessStartInfo(Path.GetFullPath(file)) { UseShellExecute = false };
            foreach (var argument in new[] { "/SP-", "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS", "/NOFORCECLOSEAPPLICATIONS", "/RESTARTAPPLICATIONS" }) start.ArgumentList.Add(argument);
            start.ArgumentList.Add("/DIR=" + AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            using var process = Process.Start(start) ?? throw new IOException("Installer could not start");
            // Success closes this process and restarts the installed version through Restart Manager.
            await process.WaitForExitAsync();
            throw new IOException("Installer exited without replacing the running app: " + process.ExitCode);
        } finally { UnregisterApplicationRestart(); }
    }
    public static UpdateIdentity Identity() => new(
#if XBAR_UPDATER_LAB
        "LIMOD.XBar.Desktop.UpdaterLab",
#else
        "LIMOD.XBar.Desktop",
#endif
        "LIMOD", RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
        typeof(DesktopUpdateInstaller).Assembly.GetName().Version ?? throw new InvalidOperationException("Missing app version"), Environment.OSVersion.Version.Build, "exe");
}
