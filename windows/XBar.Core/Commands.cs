using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XBar.Core;

public sealed record AppCommand(string Action, string? Argument = null)
{
    public bool IsValid => Action is "activate" or "settings" or "updates" or "login" or "refresh" ||
        Action == "period" && Enum.TryParse<ContentPeriod>(Argument, out var p) && Enum.IsDefined(p) ||
        Action == "post" && Argument is not null && Regex.IsMatch(Argument, @"\A[0-9]{10,25}\z");
    public static AppCommand Parse(string[] args)
    {
        var url = args.FirstOrDefault(a => a.StartsWith("xbar:", StringComparison.OrdinalIgnoreCase));
        if (url is not null && Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "xbar" && u.UserInfo == "" && u.Query == "" && u.Fragment == "")
        { var cmd = new AppCommand(u.Host, u.AbsolutePath.Trim('/')); if (cmd.IsValid) return cmd; }
        if (args.Contains("--background")) return new("background");
        return new("activate");
    }
    public string ToUri() => "xbar://" + Action + (string.IsNullOrEmpty(Argument) ? "" : "/" + Uri.EscapeDataString(Argument));
}
public static class CommandPipe
{
    public static string Name => (AppContext.GetData("XBar.UpdaterLab") is true ? "XBar.UpdaterLab." : "XBar.") + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName)))[..16];
    public static async Task<bool> SendAsync(AppCommand command, CancellationToken cancellationToken = default)
    {
        if (!command.IsValid) return false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(2000);
        try
        {
            using var pipe = new NamedPipeClientStream(".", Name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeout.Token);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(JsonSerializer.Serialize(command, StateStore.Json).AsMemory(), timeout.Token);
            return await ReadBoundedAsync(pipe, timeout.Token) == "ok";
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or UnauthorizedAccessException) { return false; }
    }
    public static async Task ListenAsync(Func<AppCommand, Task> handle, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(3000);
                var line = await ReadBoundedAsync(pipe, timeout.Token);
                var command = line is null ? null : JsonSerializer.Deserialize<AppCommand>(line, StateStore.Json);
                if (command?.IsValid == true) await handle(command);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync((command?.IsValid == true ? "ok" : "invalid").AsMemory(), timeout.Token);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or JsonException or UnauthorizedAccessException) { }
        }
    }
    private static async Task<string?> ReadBoundedAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[1024]; int length = 0;
        while (length < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(length, 1), token);
            if (n == 0) return null;
            if (buffer[length++] == 10) return Encoding.UTF8.GetString(buffer, 0, length - 1).TrimEnd('\r');
        }
        return null;
    }
}
