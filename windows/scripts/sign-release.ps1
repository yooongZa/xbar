[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Package,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$CertificateThumbprint,
    [Parameter(Mandatory=$true)][string]$ExpectedPublisher,
    [string]$TimestampUrl='http://timestamp.digicert.com',
    [string]$SignTool='',
    [switch]$MachineCertificate
)
# Uses an existing key in its certificate provider/HSM; never exports a private key.
$ErrorActionPreference='Stop'
$location=if($MachineCertificate){'Cert:\LocalMachine\My'}else{'Cert:\CurrentUser\My'}
$certificate=Get-Item -LiteralPath "$location\$CertificateThumbprint"
if(!$certificate.HasPrivateKey -or $certificate.Subject -eq $certificate.Issuer -or $certificate.Subject -cne $ExpectedPublisher){throw 'A matching non-self-signed code-signing certificate and private key are required'}
if($certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)){throw 'Signing certificate is outside its validity period'}
if($certificate.EnhancedKeyUsageList.ObjectId -notcontains '1.3.6.1.5.5.7.3.3'){throw 'Certificate is not authorized for code signing'}
if(!$SignTool){$SignTool=(Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe"|Sort-Object FullName -Descending|Select-Object -First 1).FullName}
if(!$SignTool){throw 'Windows SDK SignTool is required'}
$arguments=@('sign','/sha1',$CertificateThumbprint,'/fd','SHA256','/tr',$TimestampUrl,'/td','SHA256')
if($MachineCertificate){$arguments+='/sm'}
$arguments+=(Resolve-Path -LiteralPath $Package).Path
& $SignTool @arguments
if($LASTEXITCODE -ne 0){throw 'Signing or timestamping failed'}
& (Join-Path $PSScriptRoot 'verify-release.ps1') -Package $Package -ExpectedPublisher $ExpectedPublisher -SignTool $SignTool
