#Requires -RunAsAdministrator
# Installs dist\DropMe-<version>.msix on this PC to try the Store build before submitting it.
#
# Windows only installs signed packages, so this signs the test package with a self-signed
# certificate and trusts that certificate on this PC (Local Machine > Trusted People).
# The Store re-signs the package itself: none of this is needed for the real submission.
#
#   Install:  powershell -ExecutionPolicy Bypass -File packaging\install-local-test.ps1
#   Undo:     powershell -ExecutionPolicy Bypass -File packaging\install-local-test.ps1 -Uninstall
#
# Quit the regular DropMe (tray icon > Quit) first: only one DropMe can run at a time.
param([switch]$Uninstall)
$ErrorActionPreference = 'Stop'

$subject = 'CN=DropMe Local Test'   # must match -Publisher in build-msix.ps1 (its default)
$root = Split-Path $PSScriptRoot

if ($Uninstall) {
    Get-AppxPackage -Name 'DropMe.LocalTest' | Remove-AppxPackage
    Get-ChildItem Cert:\LocalMachine\TrustedPeople, Cert:\CurrentUser\My |
        Where-Object Subject -eq $subject | Remove-Item
    Write-Host 'Test package and test certificate removed.'
    return
}

$version = ([xml](Get-Content "$root\DropMe.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$package = Join-Path $root "dist\DropMe-$version.msix"
if (-not (Test-Path $package)) { throw "Build it first: packaging\build-msix.ps1" }

$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq $subject | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject $subject -KeyUsage DigitalSignature `
        -CertStoreLocation Cert:\CurrentUser\My -FriendlyName 'DropMe local test signing' `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}
$cer = Join-Path $env:TEMP 'DropMe-LocalTest.cer'
Export-Certificate -Cert $cert -FilePath $cer | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\signtool.exe" |
    Sort-Object FullName -Descending | Select-Object -First 1
& $signtool.FullName sign /fd SHA256 /sha1 $cert.Thumbprint $package
if ($LASTEXITCODE) { throw 'signtool failed' }

Add-AppxPackage -Path $package -ForceApplicationShutdown
Write-Host "Installed. Start DropMe from the Start menu; undo later with -Uninstall."
