using Microsoft.Win32;
using Windows.ApplicationModel;

namespace XBar.App;

internal sealed class DesktopStartupPreference(string? executable = null,
    string runPath = @"Software\Microsoft\Windows\CurrentVersion\Run",
    string approvedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run") : IStartupPreference
{
    private const string Name = "X-Bar";
    private readonly string command = "\"" + (executable ?? Environment.ProcessPath ?? throw new InvalidOperationException("Missing executable path")) + "\" --background";
    public Task<StartupTaskState> ReadAsync()
    {
        using var run = Registry.CurrentUser.OpenSubKey(runPath);
        if (run?.GetValue(Name) as string != command) return Task.FromResult(StartupTaskState.Disabled);
        using var approved = Registry.CurrentUser.OpenSubKey(approvedPath);
        // Respect a user's Task Manager choice; only Windows should reset StartupApproved.
        bool blocked = approved?.GetValue(Name) is byte[] { Length: > 0 } bytes && bytes[0] is 3 or 7;
        return Task.FromResult(blocked ? StartupTaskState.DisabledByUser : StartupTaskState.Enabled);
    }
    public async Task<StartupTaskState> SetAsync(bool enabled)
    {
        if (enabled && await ReadAsync() == StartupTaskState.DisabledByUser) return StartupTaskState.DisabledByUser;
        using var run = Registry.CurrentUser.CreateSubKey(runPath);
        if (enabled) run.SetValue(Name, command, RegistryValueKind.String);
        else if (run.GetValue(Name) as string == command) run.DeleteValue(Name, false);
        return await ReadAsync();
    }
}
