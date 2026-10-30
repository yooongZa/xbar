param([string]$Version = '0.4.4.0', [string]$ISCC = '', [string]$LayoutPath = '', [switch]$UpdaterLab)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if (!$LayoutPath) { $LayoutPath=Join-Path $root 'artifacts/layout/App' }
if (!(Test-Path "$LayoutPath/XBar.exe")) { throw 'Publish the desktop app first' }
if (!$UpdaterLab -and (Test-Path "$LayoutPath/UpdaterLab.json")) { throw 'Lab configuration cannot be published' }
if ($UpdaterLab -and !(Test-Path "$LayoutPath/UpdaterLab.json")) { throw 'Lab configuration is missing' }
$appInfo=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $LayoutPath 'XBar.dll'))
$appVersion='{0}.{1}.{2}.{3}' -f $appInfo.FileMajorPart,$appInfo.FileMinorPart,$appInfo.FileBuildPart,$appInfo.FilePrivatePart
if ($appVersion -ne $Version) { throw 'Installer version must match the included app version' }
if (!$ISCC) { $ISCC=(Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
if (!$ISCC) { $ISCC="${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
if (!(Test-Path $ISCC)) { throw 'Inno Setup 6.7.3 or compatible compiler is required' }
$output=Join-Path $root 'artifacts'
$compilerArgs=@("/DAppVersion=$Version", "/DSourceDir=$LayoutPath", "/DOutputDir=$output")
if ($UpdaterLab) { $compilerArgs += '/DUpdaterLab=1' }
& $ISCC @compilerArgs "$root/package/desktop.iss"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
$package="$output/XBar-$Version-windows-x64-setup.exe"
$info=[Diagnostics.FileVersionInfo]::GetVersionInfo($package)
$actual='{0}.{1}.{2}.{3}' -f $info.FileMajorPart,$info.FileMinorPart,$info.FileBuildPart,$info.FilePrivatePart
if ($actual -ne $Version -or $info.ProductName.Trim() -ne 'X-Bar' -or $info.CompanyName.Trim() -ne 'LIMOD') { throw 'Installer metadata mismatch' }
if ((Get-AuthenticodeSignature $package).Status -ne 'NotSigned') { throw 'Expected an unsigned desktop installer' }
$identity=if($UpdaterLab){'LIMOD.XBar.Desktop.UpdaterLab'}else{'LIMOD.XBar.Desktop'}
$proof=@{schema=1;status='passed';format='exe';identity=$identity;publisher='LIMOD';version=$Version;architecture='x64';sha256=(Get-FileHash $package).Hash.ToLowerInvariant();length=(Get-Item $package).Length;codeSigned=$false}
$proof|ConvertTo-Json|Set-Content "$package.verification.json" -Encoding UTF8
$proof|ConvertTo-Json
