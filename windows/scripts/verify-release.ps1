[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Package,
    [Parameter(Mandatory=$true)][string]$ExpectedPublisher,
    [string]$ExpectedIdentity='LIMOD.XBar',
    [string]$Report='',
    [string]$SignTool=''
)
# Read-only release gate. Never imports certificates or changes machine trust.
$ErrorActionPreference='Stop'
$packagePath=(Resolve-Path -LiteralPath $Package).Path
if (!$Report) { $Report=$packagePath+'.verification.json' }
$result=[ordered]@{schema=1;status='blocked';checkedAt=[DateTimeOffset]::UtcNow.ToString('o');sha256=(Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant();length=(Get-Item -LiteralPath $packagePath).Length;identity=$null;publisher=$null;version=$null;architecture=$null;signer=$null;timestamped=$false;error=$null}
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entries=@($zip.Entries | Where-Object {$_.FullName -eq 'AppxManifest.xml'})
        if($entries.Count -ne 1 -or $entries[0].Length -gt 131072){throw 'Invalid MSIX manifest'}
        $reader=[IO.StreamReader]::new($entries[0].Open())
        try {$document=[xml]$reader.ReadToEnd()} finally {$reader.Dispose()}
        $identity=$document.Package.Identity
        $result.identity=[string]$identity.Name;$result.publisher=[string]$identity.Publisher;$result.version=[string]$identity.Version;$result.architecture=[string]$identity.ProcessorArchitecture
        if($identity.Name -match '\.(Dev|UpdaterLab)$' -or $identity.Publisher -match 'Development|UpdaterLab'){throw 'Development packages cannot be publicly released'}
        if($identity.Name -cne $ExpectedIdentity -or $identity.Publisher -cne $ExpectedPublisher){throw 'Release identity or publisher mismatch'}
        if($identity.ProcessorArchitecture -ne 'x64'){throw 'Only the verified x64 release is supported'}
        if(@($zip.Entries|Where-Object {$_.FullName -like '*UpdaterLab.json'}).Count){throw 'Lab configuration found in release'}
        if(!$zip.GetEntry('AppxSignature.p7x')){throw 'MSIX has not been signed'}
    } finally {$zip.Dispose()}
    if(!$SignTool){$SignTool=(Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe"|Sort-Object FullName -Descending|Select-Object -First 1).FullName}
    if(!$SignTool){throw 'Windows SDK SignTool is required'}
    & $SignTool verify /pa /all $packagePath | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Windows signature trust verification failed'}
    $signature=Get-AuthenticodeSignature -LiteralPath $packagePath
    $certificate=$signature.SignerCertificate
    if($signature.Status -ne 'Valid' -or !$certificate){throw 'Valid Authenticode signature required'}
    if($certificate.Subject -eq $certificate.Issuer){throw 'Self-signed test certificates cannot be publicly released'}
    if($certificate.Subject -cne $ExpectedPublisher){throw 'Signer subject differs from expected MSIX publisher'}
    if(!$signature.TimeStamperCertificate){throw 'A trusted timestamp is required for a public release'}
    $result.signer=$certificate.Subject;$result.timestamped=$true;$result.status='passed'
} catch {$result.error=$_.Exception.Message}
$directory=Split-Path ([IO.Path]::GetFullPath($Report)) -Parent
[IO.Directory]::CreateDirectory($directory)|Out-Null
[IO.File]::WriteAllText([IO.Path]::GetFullPath($Report),($result|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
$result|ConvertTo-Json -Depth 4
if($result.status -ne 'passed'){throw "Release blocked: $($result.error)"}
