using System.Collections.Immutable;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NSec.Cryptography;
using Xunit;

namespace XBar.Core.Tests;
public sealed class UpdateTests : IDisposable
{
    private readonly Key key = Key.Create(SignatureAlgorithm.Ed25519);
    private readonly DateTimeOffset now = DateTimeOffset.Parse("2026-10-27T12:00:00Z");
    private static readonly UpdateIdentity Installed = new("LIMOD.XBar.Dev", "CN=XBar Development", "x64", new(0, 3, 0, 0), 26200);
    private UpdateSource Source => new(new Uri("https://yooongza.github.io/xbar/windows-updates.json"), Convert.ToBase64String(key.PublicKey.Export(KeyBlobFormat.RawPublicKey)));
    private static byte[] Package(string version = "0.3.0.1", string identity = "LIMOD.XBar.Dev")
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("AppxManifest.xml").Open())) writer.Write($"<Package xmlns='http://schemas.microsoft.com/appx/manifest/foundation/windows10'><Identity Name='{identity}' Publisher='CN=XBar Development' ProcessorArchitecture='x64' Version='{version}'/></Package>");
            // Structural fixture only. WinVerifyTrust + the OS deployment engine validate real signatures.
            using var signature = new StreamWriter(zip.CreateEntry("AppxSignature.p7x").Open()); signature.Write("structural-test-signature");
        }
        return stream.ToArray();
    }
    private UpdateRelease Release(byte[] bytes) => new(1, "windows", "0.3.0.1", "0.3.0", 22000, now, ImmutableDictionary<string, string>.Empty.Add("ko", "개선 사항").Add("en", "Improvements"), [new(Installed.Name, Installed.Publisher, "x64", "https://github.com/yooongZa/xbar/releases/download/windows-v0.3.0/XBar.msix", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)))]);
    private byte[] Sign(UpdateRelease release)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(release, StateStore.Json);
        return JsonSerializer.SerializeToUtf8Bytes(new UpdateEnvelope(Convert.ToBase64String(payload), Convert.ToBase64String(SignatureAlgorithm.Ed25519.Sign(key, payload))), StateStore.Json);
    }
    [Fact] public void SignedFeedSelectsNewCompatibleVersion()
    {
        var update = UpdateValidation.Read(Sign(Release(Package())), Source, Installed, now);
        Assert.NotNull(update); Assert.Equal("개선 사항", update!.Notes("ko")); Assert.Equal("Improvements", update.Notes("ja"));
    }
    [Fact] public void TamperedFeedAndWrongKeyAreRejected()
    {
        var signed = Sign(Release(Package())); var envelope = JsonSerializer.Deserialize<UpdateEnvelope>(signed, StateStore.Json)!;
        var payload = Convert.FromBase64String(envelope.Payload); payload[0] ^= 1;
        Assert.Throws<CryptographicException>(() => UpdateValidation.Read(JsonSerializer.SerializeToUtf8Bytes(envelope with { Payload = Convert.ToBase64String(payload) }, StateStore.Json), Source, Installed, now));
        using var other = Key.Create(SignatureAlgorithm.Ed25519);
        Assert.Throws<CryptographicException>(() => UpdateValidation.Read(signed, new(Source.Feed, Convert.ToBase64String(other.PublicKey.Export(KeyBlobFormat.RawPublicKey))), Installed, now));
    }
    [Theory] [InlineData("0.3.0.0")] [InlineData("0.2.9.9")]
    public void SameVersionAndDowngradesAreIgnored(string version) => Assert.Null(UpdateValidation.Read(Sign(Release(Package()) with { Version = version }), Source, Installed, now));
    [Fact] public void WrongPlatformArchitectureIdentityAndUnsupportedOsNeverInstall()
    {
        var r = Release(Package());
        Assert.Throws<InvalidDataException>(() => UpdateValidation.Read(Sign(r with { Platform = "macos" }), Source, Installed, now));
        foreach (var package in new[] { r.Packages[0] with { Architecture = "arm64" }, r.Packages[0] with { Identity = "Another.App" }, r.Packages[0] with { Publisher = "CN=Other" } })
            Assert.Null(UpdateValidation.Read(Sign(r with { Packages = [package] }), Source, Installed, now));
        Assert.Null(UpdateValidation.Read(Sign(r with { MinimumOsBuild = 99999 }), Source, Installed, now));
        Assert.Throws<InvalidDataException>(() => UpdateValidation.Read(Sign(r with { Packages = [r.Packages[0], r.Packages[0]] }), Source, Installed, now));
    }
    [Theory] [InlineData("file:///C:/evil.msix")] [InlineData("http://github.com/yooongZa/xbar/releases/download/v1/a.msix")]
    [InlineData("https://github.com/attacker/xbar/releases/download/v1/a.msix")] [InlineData("https://evil@github.com/yooongZa/xbar/releases/download/v1/a.msix")]
    [InlineData("https://github.com:444/yooongZa/xbar/releases/download/v1/a.msix")]
    public void PackageAddressesAreRestricted(string url)
    { var r = Release(Package()); Assert.Throws<InvalidDataException>(() => UpdateValidation.Read(Sign(r with { Packages = [r.Packages[0] with { Url = url }] }), Source, Installed, now)); }
    [Fact] public void LoopbackRequiresExplicitTestSourceAndExactPort()
    {
        Assert.False(Source.Allowed(new("http://127.0.0.1:9000/update.msix"), true));
        var lab = new UpdateSource(new("http://127.0.0.1:9000/feed"), Source.PublicKey, true);
        Assert.True(lab.Allowed(new("http://127.0.0.1:9000/update.msix"), true)); Assert.False(lab.Allowed(new("http://127.0.0.1:9001/update.msix"), true));
        Assert.False(lab.Allowed(new("http://localhost:9000/update.msix"), true));
    }
    [Theory] [InlineData("0.3.1")] [InlineData("65536.0.0.0")] [InlineData("1.0.0.-1")] [InlineData("1.0.0.1/evil")]
    public void InvalidVersionsAreRejected(string version) => Assert.Throws<InvalidDataException>(() => UpdateValidation.ParseVersion(version));
    [Fact] public void ConsentIsScopedOneShotAndNeverSurvivesCancel()
    {
        var consent = new UpdateConsent(); var old = consent.Begin(); consent.Choose(old); var current = consent.Begin();
        Assert.False(consent.Consume(old)); Assert.False(consent.Consume(current));
        current = consent.Begin(); consent.Choose(current); Assert.True(consent.Consume(current)); Assert.False(consent.Consume(current));
        current = consent.Begin(); consent.Choose(current); consent.Invalidate(); Assert.False(consent.Consume(current));
    }
    [Fact] public void ChecksAreDailyAndUserPreferencePersists()
    {
        var prefs = new UpdatePreferences(); Assert.True(prefs.Due(now)); prefs = prefs with { LastCheck = now };
        Assert.False(prefs.Due(now.AddHours(23))); Assert.True(prefs.Due(now.AddDays(1)));
        prefs = prefs with { AutomaticChecks = false }; Assert.False(prefs.Due(now.AddDays(2)));
        Assert.Equal(prefs, JsonSerializer.Deserialize<UpdatePreferences>(JsonSerializer.Serialize(prefs, StateStore.Json), StateStore.Json));
    }
    [Fact] public async Task DownloadVerifiesHashLengthAndIdentityBeforePublishingFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "xbar-update-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try {
            var bytes = Package(); var release = Release(bytes); var update = new VerifiedUpdate(release, release.Packages[0]);
            using var client = new UpdateClient(Source, new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) })));
            var path = Path.Combine(root, "update.msix"); await client.DownloadAsync(update, path, null, CancellationToken.None); Assert.True(File.Exists(path)); Assert.False(File.Exists(path + ".partial"));
            await Assert.ThrowsAsync<CryptographicException>(() => client.DownloadAsync(update with { Package = update.Package with { Sha256 = new string('0', 64) } }, Path.Combine(root, "bad.msix"), null, CancellationToken.None));
            var other = Package(identity: "Other.App"); var otherRelease = Release(other);
            File.WriteAllBytes(path, other);
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdateValidation.VerifyPackageAsync(path, new(otherRelease, otherRelease.Packages[0]), CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(root, "bad.msix.partial"))); Assert.False(File.Exists(Path.Combine(root, "bad.msix")));
        } finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task RedirectToUntrustedHostStopsBeforeSecondRequest()
    {
        int count = 0; using var client = new UpdateClient(Source, new Handler((_, _) => { count++; var response = new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location = new("https://evil.example/update.msix"); return Task.FromResult(response); }));
        var r = Release(Package()); var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(new(r, r.Packages[0]), Path.Combine(root, "update.msix"), null, CancellationToken.None)); Assert.Equal(1, count);
    }
    [Fact] public async Task CanceledDownloadNeverCreatesInstallableFile()
    {
        using var cancel = new CancellationTokenSource(50); var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); var path = Path.Combine(root, "update.msix");
        using var client = new UpdateClient(Source, new Handler(async (_, token) => { await Task.Delay(5000, token); return new(HttpStatusCode.OK); }));
        var r = Release(Package()); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DownloadAsync(new(r, r.Packages[0]), path, null, cancel.Token));
        Assert.False(File.Exists(path)); Assert.False(File.Exists(path + ".partial"));
    }
    [Fact] public async Task UnpublishedFeedIsDistinctFromNoUpdates()
    { using var client = new UpdateClient(Source, new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))); await Assert.ThrowsAsync<UpdateChannelPendingException>(() => client.CheckAsync(Installed, now, CancellationToken.None)); }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token); }
    public void Dispose() => key.Dispose();
}
