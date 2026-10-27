param([string]$Dotnet = 'dotnet', [string]$Configuration = 'Release', [switch]$SkipUiTests)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    & $Dotnet test XBar.Core.Tests/XBar.Core.Tests.csproj -c $Configuration -p:RestoreLockedMode=true --logger 'trx;LogFileName=core.trx' --results-directory artifacts/tests
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
    & $Dotnet publish XBar.App/XBar.App.csproj -c $Configuration -p:RestoreLockedMode=true -r win-x64 --self-contained true -o artifacts/layout/App
    if ($LASTEXITCODE -ne 0) { throw 'App publish failed' }
    & $Dotnet publish XBar.WidgetProvider/XBar.WidgetProvider.csproj -c $Configuration -p:RestoreLockedMode=true -r win-x64 --self-contained true -o artifacts/layout/Provider
    if ($LASTEXITCODE -ne 0) { throw 'Widget provider publish failed' }
    & $Dotnet build XBar.WindowsTests/XBar.WindowsTests.csproj -c $Configuration -p:RestoreLockedMode=true -r win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Windows integration build failed' }
    if (-not $SkipUiTests) {
    & $Dotnet XBar.WindowsTests/bin/$Configuration/net10.0-windows10.0.19041.0/win-x64/XBar.WindowsTests.dll artifacts/tests/windows-results.json
    if ($LASTEXITCODE -ne 0) { throw 'Windows integration failed' }
    & $Dotnet XBar.WindowsTests/bin/$Configuration/net10.0-windows10.0.19041.0/win-x64/XBar.WindowsTests.dll artifacts/tests/updater-ui-results.json --updates-only
    if ($LASTEXITCODE -ne 0) { throw 'Updater integration failed' }
    & $Dotnet XBar.WindowsTests/bin/$Configuration/net10.0-windows10.0.19041.0/win-x64/XBar.WindowsTests.dll artifacts/tests/settings-results.json --settings-only
    if ($LASTEXITCODE -ne 0) { throw 'Settings integration failed' }
    }
} finally { Pop-Location }
