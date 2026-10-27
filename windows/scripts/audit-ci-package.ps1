[CmdletBinding()]
param([string]$Package='')
# Inspect a CI proof without installing it, signing it, importing certificates, or changing OS settings.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
[xml]$props=[IO.File]::ReadAllText((Join-Path $root 'Directory.Build.props'))
$version=[string]$props.Project.PropertyGroup.Version
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Expected three-part source version'}
if(!$Package){$Package=Join-Path $root "artifacts/XBar-$version.0-x64.msix"}
$packagePath=(Resolve-Path -LiteralPath $Package).Path
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($packagePath)
function Read-Entry($archive,[string]$name){
    $entry=$archive.GetEntry($name)
    if(!$entry){throw "Missing package entry: $name"}
    $stream=$entry.Open();$memory=[IO.MemoryStream]::new()
    try{$stream.CopyTo($memory);return ,$memory.ToArray()}finally{$stream.Dispose();$memory.Dispose()}
}
try {
    [xml]$manifest=[Text.Encoding]::UTF8.GetString((Read-Entry $zip 'AppxManifest.xml'))
    $id=$manifest.Package.Identity
    if($id.Name -cne 'LIMOD.XBar.Dev' -or $id.Publisher -cne 'CN=XBar Development' -or $id.Version -ne "$version.0" -or $id.ProcessorArchitecture -ne 'x64'){throw 'Unexpected development package identity'}
    if($zip.GetEntry('AppxSignature.p7x')){throw 'CI proof must remain unsigned'}
    if(@($zip.Entries|Where-Object {$_.FullName -like '*UpdaterLab.json'}).Count){throw 'Updater lab configuration in normal package'}
    foreach($name in @('Extractor.js','PageContext.js')){
        $actual=Read-Entry $zip "App/Scripts/$name"
        $expected=[IO.File]::ReadAllBytes((Join-Path $root "../app/Resources/$name"))
        if([Convert]::ToBase64String($actual) -cne [Convert]::ToBase64String($expected)){throw "Shared script changed: $name"}
    }
}finally{$zip.Dispose()}
$metadata=@()
foreach($file in @('App/XBar.exe','App/XBar.dll','App/XBar.Core.dll','Provider/XBar.WidgetProvider.exe','Provider/XBar.WidgetProvider.dll','Provider/XBar.Core.dll')){
    $info=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root "artifacts/layout/$file"))
    if($info.ProductName -cne 'X-Bar' -or $info.CompanyName -cne 'LIMOD' -or $info.FileVersion -ne "$version.0"){throw "Unexpected product metadata: $file"}
    $metadata+=@{file=$file;product=$info.ProductName;fileVersion=$info.FileVersion}
}
[xml]$trx=[IO.File]::ReadAllText((Join-Path $root 'artifacts/tests/core.trx'))
$core=$trx.TestRun.ResultSummary.Counters
if([int]$core.failed -ne 0 -or [int]$core.passed -le 0){throw 'Core test results missing or failed'}
$counts=@{core=[int]$core.passed}
foreach($kind in @('layout','updater-ui')){
    $tests=@([IO.File]::ReadAllText((Join-Path $root "artifacts/tests/$kind-results.json"))|ConvertFrom-Json)
    if($tests.Count -eq 0 -or @($tests|Where-Object {$_.passed -ne $true}).Count){throw "Missing or failed $kind tests"}
    $counts[$kind]=$tests.Count
}
$source=(git -C $root rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0){throw 'Source commit unavailable'}
$report=[ordered]@{schema=1;status='passed';sourceCommit=$source;runUrl=if($env:GITHUB_RUN_ID){"https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"}else{$null};sdk=(dotnet --version);package=[IO.Path]::GetFileName($packagePath);sha256=(Get-FileHash $packagePath).Hash.ToLowerInvariant();length=(Get-Item $packagePath).Length;unsigned=$true;publicRelease=$false;identity=[string]$id.Name;tests=$counts;ownBinaries=$metadata;fullWebViewAndCleanPcValidation=$false}
$json=$report|ConvertTo-Json -Depth 6
$json|Set-Content (Join-Path $root 'artifacts/ci-build-proof.json') -Encoding utf8
Write-Output $json
if($env:GITHUB_STEP_SUMMARY){
    @("## Windows source build proof",'',"Source: $source",'',"Core: $($counts.core); layout: $($counts.layout); updater: $($counts.'updater-ui').",'',"Package SHA-256: $($report.sha256)",'','Unsigned development proof. This run does not establish public signing, full WebView2 QA, or clean-PC installation.')|Add-Content $env:GITHUB_STEP_SUMMARY
}
