using System.Net;

namespace XBar.Core;
public sealed class UpdateChannelPendingException : IOException { }
public sealed class UpdateClient : IDisposable
{
    private readonly UpdateSource source;
    private readonly HttpClient client;
    public UpdateClient(UpdateSource source, HttpMessageHandler? handler = null)
    {
        this.source = source;
        client = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false, AutomaticDecompression = DecompressionMethods.None }) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("X-Bar-Updater/1.0");
    }
    private async Task<HttpResponseMessage> OpenAsync(Uri uri, bool asset, CancellationToken token)
    {
        for (int redirects = 0; redirects <= 5; redirects++)
        {
            if (!source.Allowed(uri, asset, redirects > 0)) throw new InvalidDataException("Untrusted update address");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(token); requestTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestTimeout.Token);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var location = response.Headers.Location; response.Dispose();
                if (location is null) throw new InvalidDataException("Missing redirect");
                uri = location.IsAbsoluteUri ? location : new(uri, location); continue;
            }
            if (!asset && response.StatusCode == HttpStatusCode.NotFound) { response.Dispose(); throw new UpdateChannelPendingException(); }
            try { response.EnsureSuccessStatusCode(); return response; } catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException("Too many update redirects");
    }
    public async Task<VerifiedUpdate?> CheckAsync(UpdateIdentity installed, DateTimeOffset now, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var response = await OpenAsync(source.Feed, false, timeout.Token);
        if (response.Content.Headers.ContentLength > UpdateValidation.MaximumFeedBytes) throw new InvalidDataException("Feed too large");
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var output = new MemoryStream(); await CopyAsync(input, output, UpdateValidation.MaximumFeedBytes, null, timeout.Token);
        return UpdateValidation.Read(output.ToArray(), source, installed, now);
    }
    public async Task DownloadAsync(VerifiedUpdate update, string destination, IProgress<double>? progress, CancellationToken token)
    {
        var partial = destination + ".partial";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(30));
        try
        {
            using var response = await OpenAsync(new(update.Package.Url), true, timeout.Token);
            if (response.Content.Headers.ContentLength is long length && length != update.Package.Length) throw new InvalidDataException("Package size mismatch");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            { await CopyAsync(input, output, update.Package.Length, progress, timeout.Token); await output.FlushAsync(timeout.Token); }
            await UpdateValidation.VerifyPackageAsync(partial, update, timeout.Token);
            token.ThrowIfCancellationRequested(); File.Move(partial, destination, false);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
    private static async Task CopyAsync(Stream input, Stream output, long maximum, IProgress<double>? progress, CancellationToken token)
    {
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = await input.ReadAsync(buffer, token).AsTask().WaitAsync(TimeSpan.FromSeconds(30), token)) > 0)
        {
            total += read; if (total > maximum) throw new InvalidDataException("Update exceeds declared size");
            await output.WriteAsync(buffer.AsMemory(0, read), token); progress?.Report((double)total / maximum);
        }
    }
    public void Dispose() => client.Dispose();
}
