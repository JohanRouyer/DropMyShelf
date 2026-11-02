# Builds dist\DropMyShelf-<version>.msix to upload to the Microsoft Store.
# Needs the .NET 8 SDK and the Windows SDK (winget install Microsoft.WindowsSDK.10.0.26100).
#
# The identity is the one Partner Center assigned (DropMyShelf > Product management > Product identity).
# -LocalTest builds dist\DropMyShelf-<version>-localtest.msix with a separate identity instead, for
# packaging\install-local-test.ps1 to sign and install on this PC.
param([switch]$LocalTest)
$ErrorActionPreference = 'Stop'

if ($LocalTest) {
    $IdentityName = 'DropMyShelf.LocalTest'
    $Publisher = 'CN=DropMyShelf Local Test'   # the subject of install-local-test.ps1's certificate
    $PublisherDisplayName = 'DropMyShelf local test'
    $suffix = '-localtest'
} else {
    $IdentityName = 'M1r4g3.DropMyShelf'
    $Publisher = 'CN=C1FC7468-8DBE-4F85-9C97-981B77A14779'
    $PublisherDisplayName = 'M1r4g3'
    $suffix = ''
}

$root = Split-Path $PSScriptRoot
$version = ([xml](Get-Content "$root\DropMyShelf.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
# The Store requires four parts, the last one 0.
$packageVersion = "$version.0"

$sdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64" -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending | Where-Object { Test-Path "$($_.FullName)\makeappx.exe" } | Select-Object -First 1
if (-not $sdkBin) { throw "Windows SDK not found: winget install Microsoft.WindowsSDK.10.0.26100" }
$makeappx = Join-Path $sdkBin.FullName 'makeappx.exe'
$makepri = Join-Path $sdkBin.FullName 'makepri.exe'

$layout = Join-Path $root 'bin\msix\layout'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }

dotnet publish "$root\DropMyShelf.csproj" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=true -p:DebugType=none -o $layout
if ($LASTEXITCODE) { throw "dotnet publish failed" }

Copy-Item "$root\LICENSE.md" $layout
Copy-Item "$PSScriptRoot\Assets" (Join-Path $layout 'Assets') -Recurse

$manifest = (Get-Content "$PSScriptRoot\AppxManifest.template.xml" -Raw).
    Replace('{{IdentityName}}', $IdentityName).
    Replace('{{Publisher}}', [Security.SecurityElement]::Escape($Publisher)).
    Replace('{{PublisherDisplayName}}', [Security.SecurityElement]::Escape($PublisherDisplayName)).
    Replace('{{Version}}', $packageVersion)
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

# resources.pri lets Windows pick the right logo size (scale-100/200, targetsize-*).
$priConfig = Join-Path $root 'bin\msix\priconfig.xml'
& $makepri createconfig /cf $priConfig /dq en-US /o | Out-Null
# Keep every scale in the main resources.pri: split resource packs only work in an .msixbundle.
$config = [xml](Get-Content $priConfig)
$config.resources.packaging | ForEach-Object { [void]$_.ParentNode.RemoveChild($_) }
$config.Save($priConfig)
& $makepri new /pr $layout /cf $priConfig /mn (Join-Path $layout 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o | Out-Null
if ($LASTEXITCODE) { throw "makepri failed" }

New-Item -ItemType Directory -Force "$root\dist" | Out-Null
$package = Join-Path $root "dist\DropMyShelf-$version$suffix.msix"
& $makeappx pack /o /d $layout /p $package | Out-Null
if ($LASTEXITCODE) { throw "makeappx failed" }

Get-Item $package
