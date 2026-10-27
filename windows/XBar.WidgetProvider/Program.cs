using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using WinRT;
using XBar.Core;

namespace XBar.WidgetProvider;

internal static class Program
{
    internal static readonly Guid ClassId = new("714FE93A-D4D8-4AF2-AFC1-4248B86D9260");
    [DllImport("ole32.dll")] private static extern int CoRegisterClassObject(in Guid id, [MarshalAs(UnmanagedType.IUnknown)] object factory, uint context, uint flags, out uint cookie);
    [DllImport("ole32.dll")] private static extern int CoRevokeClassObject(uint cookie);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint coInit);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [MTAThread] private static int Main(string[] args)
    {
        var store = new StateStore(StateStore.DefaultRoot);
        try
        {
            Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero, 0));
            if (args.Contains("--probe"))
            { WidgetService.Probe(store); return 0; }
            var factory = new ProviderFactory();
            Marshal.ThrowExceptionForHR(CoRegisterClassObject(ClassId, factory, 4, 1, out var cookie));
            try { WidgetService.Stopped.Wait(); }
            finally { CoRevokeClassObject(cookie); GC.KeepAlive(factory); }
            return 0;
        }
        catch (Exception e)
        { RecordFailure(e); return 1; }
        finally { CoUninitialize(); }
    }
    internal static void RecordFailure(Exception error)
    {
        var store = new StateStore(StateStore.DefaultRoot);
        store.Write("registration.json", new WidgetRegistration(0, DateTimeOffset.UtcNow, error.GetType().Name));
        // Method names locate packaging/activation failures without recording user data.
        store.Write("provider-diagnostics.json", new {
            Type = error.GetType().Name, HResult = $"0x{error.HResult:X8}",
            Methods = new System.Diagnostics.StackTrace(error, false).GetFrames()
                .Select(frame => frame.GetMethod()).Select(method => method?.DeclaringType?.FullName + "." + method?.Name).ToArray()
        });
    }
}

[ComImport, Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IClassFactory
{
    [PreserveSig] int CreateInstance(IntPtr outer, ref Guid iid, out IntPtr result);
    [PreserveSig] int LockServer([MarshalAs(UnmanagedType.Bool)] bool locked);
}
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class ProviderFactory : IClassFactory
{
    public int CreateInstance(IntPtr outer, ref Guid iid, out IntPtr result)
    {
        result = IntPtr.Zero;
        if (outer != IntPtr.Zero) return unchecked((int)0x80040110);
        IntPtr unknown = IntPtr.Zero;
        try
        {
            unknown = MarshalInspectable<IWidgetProvider>.FromManaged(WidgetService.Instance);
            return Marshal.QueryInterface(unknown, in iid, out result);
        }
        catch (Exception e) { Program.RecordFailure(e); return Marshal.GetHRForException(e); }
        finally { if (unknown != IntPtr.Zero) Marshal.Release(unknown); }
    }
    public int LockServer(bool locked) => 0;
}
