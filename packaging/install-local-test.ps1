#Requires -RunAsAdministrator
# Installs dist\DropMyShelf-<version>-localtest.msix on this PC to try the Store build before
# submitting it. Build that package first with: packaging\build-msix.ps1 -LocalTest
#
# Windows only installs signed packages, so this signs the test package with a self-signed
# certificate and trusts that certificate on this PC (Local Machine > Trusted People).
# The Store re-signs the package itself: none of this is needed for the real submission.
#
#   Install:  powershell -ExecutionPolicy Bypass -File packaging\install-local-test.ps1
#   Undo:     powershell -ExecutionPolicy Bypass -File packaging\install-local-test.ps1 -Uninstall
#
# Quit the regular DropMyShelf (tray icon > Quit) first: only one DropMyShelf can run at a time.
param([switch]$Uninstall)
$ErrorActionPreference = 'Stop'

$subject = 'CN=DropMyShelf Local Test'   # must match the publisher build-msix.ps1 -LocalTest uses
$root = Split-Path $PSScriptRoot

if ($Uninstall) {
    Get-AppxPackage -Name 'DropMyShelf.LocalTest' | Remove-AppxPackage
    Get-ChildItem Cert:\LocalMachine\TrustedPeople, Cert:\CurrentUser\My |
        Where-Object Subject -eq $subject | Remove-Item
    Write-Host 'Test package and test certificate removed.'
    return
}

$version = ([xml](Get-Content "$root\DropMyShelf.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$package = Join-Path $root "dist\DropMyShelf-$version-localtest.msix"
if (-not (Test-Path $package)) { throw "Build it first: packaging\build-msix.ps1 -LocalTest" }

$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq $subject | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject $subject -KeyUsage DigitalSignature `
        -CertStoreLocation Cert:\CurrentUser\My -FriendlyName 'DropMyShelf local test signing' `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}
$cer = Join-Path $env:TEMP 'DropMyShelf-LocalTest.cer'
Export-Certificate -Cert $cert -FilePath $cer | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\signtool.exe" |
    Sort-Object FullName -Descending | Select-Object -First 1
& $signtool.FullName sign /fd SHA256 /sha1 $cert.Thumbprint $package
if ($LASTEXITCODE) { throw 'signtool failed' }

Add-AppxPackage -Path $package -ForceApplicationShutdown
Write-Host "Installed. Start DropMyShelf from the Start menu; undo later with -Uninstall."
