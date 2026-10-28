param([string]$Destination = "$env:RUNNER_TEMP\XBar-Inno")
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $Destination | Out-Null
$file=Join-Path $Destination 'innosetup-6.7.3.exe'
Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $file
if ((Get-FileHash $file).Hash -ne '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732') { throw 'Inno compiler download hash mismatch' }
$signature=Get-AuthenticodeSignature $file
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Pyrsys') { throw 'Inno compiler publisher validation failed' }
$folder=Join-Path $Destination 'compiler'
$p=Start-Process $file -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /DIR=`"$folder`"" -Wait -PassThru
if ($p.ExitCode -ne 0 -or !(Test-Path "$folder\ISCC.exe")) { throw 'Inno compiler installation failed' }
Write-Output "$folder\ISCC.exe"
