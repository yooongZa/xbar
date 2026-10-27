using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using XBar.Core;

namespace XBar.App;
internal interface IUpdateInstaller
{
    Task PrepareAsync(string file, CancellationToken token);
    Task InstallAsync(string file, IProgress<double> progress);
}
internal sealed class WindowsUpdateInstaller : IUpdateInstaller
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterApplicationRestart(string arguments, uint flags);
    [DllImport("kernel32.dll")] private static extern int UnregisterApplicationRestart();
    [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)] private static extern int WinVerifyTrust(IntPtr window, ref Guid action, IntPtr data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct TrustFile { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr File, Subject; }
    [StructLayout(LayoutKind.Sequential)] private struct TrustData
    {
        public uint Size; public IntPtr Policy, Sip; public uint Ui, Revocation, Choice; public IntPtr File; public uint StateAction; public IntPtr State, Url; public uint Flags, Context; public IntPtr SignatureSettings;
    }
    public static void VerifyTrust(string path)
    {
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
        var fp = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>()); var dp = Marshal.AllocHGlobal(Marshal.SizeOf<TrustData>());
        try
        {
            Marshal.StructureToPtr(file, fp, false);
            // No certificate import, trust override or installation UI. Windows validates the MSIX SIP.
            var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, Choice = 1, File = fp, StateAction = 1, Flags = 0x1000 };
            Marshal.StructureToPtr(data, dp, false); var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            int result = WinVerifyTrust(IntPtr.Zero, ref action, dp);
            data = Marshal.PtrToStructure<TrustData>(dp); data.StateAction = 2; Marshal.StructureToPtr(data, dp, false); WinVerifyTrust(IntPtr.Zero, ref action, dp);
            if (result != 0) throw new System.Security.Cryptography.CryptographicException(result);
        }
        finally { Marshal.DestroyStructure<TrustFile>(fp); Marshal.FreeHGlobal(fp); Marshal.FreeHGlobal(dp); }
    }
    public async Task PrepareAsync(string file, CancellationToken token)
    {
        await Task.Run(() => VerifyTrust(file), token);
        // Restart Manager only restarts processes that have been running for 60 seconds.
        var remaining = TimeSpan.FromSeconds(65) - (DateTime.Now - Process.GetCurrentProcess().StartTime);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, token);
        token.ThrowIfCancellationRequested();
    }
    public async Task InstallAsync(string file, IProgress<double> progress)
    {
        Marshal.ThrowExceptionForHR(RegisterApplicationRestart("--background", 1 | 2 | 8));
        try
        {
            var manager = new PackageManager();
            var operation = manager.AddPackageAsync(new Uri(Path.GetFullPath(file)), null, DeploymentOptions.ForceTargetApplicationShutdown);
            operation.Progress = (_, value) => progress.Report(value.percentage / 100.0);
            var result = await operation;
            if (result.ExtendedErrorCode is { } error && error.HResult < 0) throw error;
            // On successful self-update Restart Manager shuts down this process and relaunches it.
        }
        catch { UnregisterApplicationRestart(); throw; }
    }
    public static UpdateIdentity Identity()
    {
        var id = Package.Current.Id; var v = id.Version;
        return new(id.Name, id.Publisher, id.Architecture.ToString().ToLowerInvariant(), new(v.Major, v.Minor, v.Build, v.Revision), Environment.OSVersion.Version.Build);
    }
}
