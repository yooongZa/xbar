param([string]$Version = '0.3.2.0', [string]$Identity = 'LIMOD.XBar.Dev', [string]$Publisher = 'CN=XBar Development', [string]$MakeAppx = '', [string]$LayoutPath = '', [switch]$UpdaterLab)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$layout = Join-Path $root 'artifacts/layout'
if ($LayoutPath) { $layout = $LayoutPath }
if ($UpdaterLab -and $Identity -ne 'LIMOD.XBar.UpdaterLab') { throw 'Isolated updater lab identity required' }
if (-not $UpdaterLab -and (Test-Path "$layout/App/UpdaterLab.json")) { throw 'Lab files cannot be packaged for production' }
foreach ($exe in @('App/XBar.exe','Provider/XBar.WidgetProvider.exe')) { if (-not (Test-Path (Join-Path $layout $exe))) { throw "Build first: $exe" } }
if (-not $MakeAppx) { $MakeAppx = (Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" | Sort-Object FullName -Descending | Select-Object -First 1).FullName }
if (-not $MakeAppx) { throw 'Install the Windows SDK packaging tools' }
New-Item -ItemType Directory -Force "$layout/Assets", "$layout/Public" | Out-Null
[xml]$manifest = Get-Content "$root/package/AppxManifest.xml"
$manifest.Package.Identity.Version = $Version
$manifest.Package.Identity.Name = $Identity
$manifest.Package.Identity.Publisher = $Publisher
if ($UpdaterLab) {
    $application = $manifest.Package.Applications.Application
    $application.RemoveChild($application.Extensions) | Out-Null
    $manifest.Package.Properties.DisplayName = 'X-Bar Updater Lab'
    $application.SelectSingleNode('*[local-name()="VisualElements"]').SetAttribute('DisplayName', 'X-Bar Updater Lab')
}
$manifest.Save("$layout/AppxManifest.xml")
Add-Type -AssemblyName System.Drawing
$icon = [Drawing.Image]::FromFile((Join-Path $root '../assets/app-icon.png'))
try {
    foreach ($item in @(@('StoreLogo.png',50), @('Square44.png',44), @('Square150.png',150))) {
        $bitmap = New-Object Drawing.Bitmap($item[1],$item[1])
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic; $graphics.DrawImage($icon,0,0,$item[1],$item[1]); $bitmap.Save("$layout/Assets/$($item[0])",[Drawing.Imaging.ImageFormat]::Png) }
        finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $icon.Dispose() }
Copy-Item "$root/artifacts/tests/widget-preview.png" "$layout/Assets/Preview.png" -Force
Copy-Item "$root/../LICENSE" "$layout/LICENSE.txt" -Force
Copy-Item "$root/THIRD-PARTY-UPDATER-NOTICES.txt" "$layout/THIRD-PARTY-UPDATER-NOTICES.txt" -Force
# Packaged WinRT marshaling resolves namespace metadata from the package root.
# The provider's embedded SxS manifest alone does not make subfolder metadata visible.
$metadata = "$layout/Provider/Microsoft.Windows.Widgets.winmd"
if (-not (Test-Path $metadata)) { throw 'Missing Windows Widgets runtime metadata; publish the provider first' }
Copy-Item $metadata "$layout/Microsoft.Windows.Widgets.winmd" -Force
Set-Content "$layout/Public/README.txt" 'X-Bar Windows widget provider' -Encoding UTF8
$output = "$root/artifacts/XBar-$Version-x64.msix"
& $MakeAppx pack /d $layout /p $output /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX validation failed' }
Get-FileHash $output -Algorithm SHA256
Write-Host 'Unsigned package created. Sign with the matching publisher certificate, or register the layout for development.'
