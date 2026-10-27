using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using NSec.Cryptography;

namespace XBar.Core;

public sealed record UpdatePreferences
{
    public bool AutomaticChecks { get; init; } = true;
    public DateTimeOffset? LastCheck { get; init; }
    public string? SkippedVersion { get; init; }
    public bool Due(DateTimeOffset now) => AutomaticChecks && (LastCheck is null || now - LastCheck >= TimeSpan.FromDays(1) || LastCheck - now > TimeSpan.FromDays(1));
}
public sealed record UpdateIdentity(string Name, string Publisher, string Architecture, Version Version, int OsBuild);
public sealed record UpdatePackage(string Identity, string Publisher, string Architecture, string Url, long Length, string Sha256);
public sealed record UpdateRelease(int Schema, string Platform, string Version, string DisplayVersion, int MinimumOsBuild,
    DateTimeOffset PublishedAt, ImmutableDictionary<string, string> Notes, UpdatePackage[] Packages);
public sealed record UpdateEnvelope(string Payload, string Signature);
public sealed record VerifiedUpdate(UpdateRelease Release, UpdatePackage Package)
{
    public string Notes(string language) => Release.Notes.TryGetValue(L10n.Language(language), out var text) ? text : Release.Notes.GetValueOrDefault("en", "");
}
public sealed record UpdateReceipt(string Version, DateTimeOffset StartedAt);

// Each install choice belongs to this presentation only. Never serialized to disk.
public sealed class UpdateConsent
{
    private Guid? session;
    private bool install;
    public Guid Begin() { install = false; return (session = Guid.NewGuid()).Value; }
    public bool Choose(Guid id) { if (id != session) return false; install = true; return true; }
    public void Invalidate() { install = false; session = null; }
    public bool Consume(Guid id) { if (id != session) return false; var result = install; Invalidate(); return result; }
}
public sealed class UpdateSource(Uri feed, string publicKey, bool allowLoopback = false)
{
    public Uri Feed { get; } = feed;
    public string PublicKey { get; } = publicKey;
    private bool Local(Uri uri) => allowLoopback && uri.Scheme == "http" && uri.Host == "127.0.0.1" && uri.Port == Feed.Port;
    public bool Allowed(Uri uri, bool asset, bool redirect = false)
    {
        if (!uri.IsAbsoluteUri || uri.UserInfo != "" || uri.Fragment != "") return false;
        if (Local(uri)) return true;
        if (uri.Scheme != "https" || uri.Port != 443) return false;
        if (!asset) return uri == Feed && uri.Host == "yooongza.github.io" && uri.AbsolutePath == "/xbar/windows-updates.json" && uri.Query == "";
        if (uri.Host == "github.com" && uri.AbsolutePath.StartsWith("/yooongZa/xbar/releases/download/", StringComparison.Ordinal) && uri.Query == "") return true;
        return redirect && uri.Host is "release-assets.githubusercontent.com" or "objects.githubusercontent.com";
    }
}
public static class UpdateValidation
{
    public const int MaximumFeedBytes = 131072;
    public const long MaximumPackageBytes = 1_073_741_824;
    public static Version ParseVersion(string text)
    {
        if (text is null || !Regex.IsMatch(text, @"\A\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}\z") || !Version.TryParse(text, out var value) || new[] { value.Major, value.Minor, value.Build, value.Revision }.Any(n => n is < 0 or > 65535)) throw new InvalidDataException("Invalid update version");
        return value;
    }
    public static VerifiedUpdate? Read(ReadOnlySpan<byte> data, UpdateSource source, UpdateIdentity installed, DateTimeOffset now)
    {
        if (data.Length > MaximumFeedBytes) throw new InvalidDataException("Update feed too large");
        var envelope = JsonSerializer.Deserialize<UpdateEnvelope>(data, StateStore.Json) ?? throw new InvalidDataException("Missing signature");
        var payload = Convert.FromBase64String(envelope.Payload); var signature = Convert.FromBase64String(envelope.Signature);
        var algorithm = SignatureAlgorithm.Ed25519;
        var key = PublicKey.Import(algorithm, Convert.FromBase64String(source.PublicKey), KeyBlobFormat.RawPublicKey);
        if (!algorithm.Verify(key, payload, signature)) throw new CryptographicException("Update feed signature rejected");
        var release = JsonSerializer.Deserialize<UpdateRelease>(payload, StateStore.Json) ?? throw new InvalidDataException("Missing release");
        var version = ParseVersion(release.Version);
        if (release.Schema != 1 || release.Platform != "windows" || release.DisplayVersion is not { Length: > 0 and <= 32 } || release.MinimumOsBuild < 22000 || release.PublishedAt > now.AddMinutes(5) || release.Notes is null || release.Notes.Count > 3 || release.Notes.Any(x => x.Key is not ("ko" or "en" or "ja") || x.Value is null || x.Value.Length > 6000) || release.Packages is not { Length: > 0 and <= 4 }) throw new InvalidDataException("Invalid release metadata");
        var compatible = release.Packages.Where(p => p is not null && p.Identity == installed.Name && p.Publisher == installed.Publisher && p.Architecture == installed.Architecture).ToArray();
        if (compatible.Length > 1) throw new InvalidDataException("Ambiguous update package");
        if (compatible.Length == 0 || version <= installed.Version || release.MinimumOsBuild > installed.OsBuild) return null;
        var package = compatible[0];
        if (package.Length is <= 0 or > MaximumPackageBytes || package.Sha256 is null || !Regex.IsMatch(package.Sha256, @"\A[0-9a-fA-F]{64}\z") || !Uri.TryCreate(package.Url, UriKind.Absolute, out var uri) || !source.Allowed(uri, true)) throw new InvalidDataException("Invalid package metadata");
        return new(release, package);
    }
    public static async Task VerifyPackageAsync(string path, VerifiedUpdate update, CancellationToken token)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        if (stream.Length != update.Package.Length) throw new InvalidDataException("Package length mismatch");
        var hash = await SHA256.HashDataAsync(stream, token);
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(update.Package.Sha256))) throw new CryptographicException("Package hash mismatch");
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        if (archive.Entries.Count(e => e.FullName == "AppxManifest.xml") != 1 || archive.GetEntry("AppxSignature.p7x") is null) throw new InvalidDataException("Signed MSIX required");
        var entry = archive.GetEntry("AppxManifest.xml")!;
        if (entry.Length > MaximumFeedBytes) throw new InvalidDataException("Manifest too large");
        using var xml = XmlReader.Create(entry.Open(), new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumFeedBytes });
        var manifest = XDocument.Load(xml); XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        var identity = manifest.Root?.Element(ns + "Identity");
        if ((string?)identity?.Attribute("Name") != update.Package.Identity || (string?)identity?.Attribute("Publisher") != update.Package.Publisher || (string?)identity?.Attribute("ProcessorArchitecture") != update.Package.Architecture || (string?)identity?.Attribute("Version") != update.Release.Version) throw new InvalidDataException("Package identity mismatch");
        token.ThrowIfCancellationRequested();
    }
}
