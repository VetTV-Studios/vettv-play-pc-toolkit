# Generates winget manifests (schema 1.6) for a published GitHub release. Submit them to microsoft/winget-pkgs manually.
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$MsiUrl,
    [Parameter(Mandatory)][string]$MsiPath,
    [string]$OutDir = "artifacts/winget"
)
$ErrorActionPreference = 'Stop'
$id = 'VetTVStudios.PlayPCToolkit'
$sha = (Get-FileHash $MsiPath -Algorithm SHA256).Hash
$dir = Join-Path $OutDir "manifests/v/VetTVStudios/PlayPCToolkit/$Version"
New-Item -ItemType Directory -Force $dir | Out-Null
@"
PackageIdentifier: $id
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
"@ | Set-Content (Join-Path $dir "$id.yaml")
@"
PackageIdentifier: $id
PackageVersion: $Version
InstallerType: msi
Scope: machine
Installers:
- Architecture: x64
  InstallerUrl: $MsiUrl
  InstallerSha256: $sha
ManifestType: installer
ManifestVersion: 1.6.0
"@ | Set-Content (Join-Path $dir "$id.installer.yaml")
@"
PackageIdentifier: $id
PackageVersion: $Version
PackageLocale: en-US
Publisher: VetTV Studios
PackageName: VetTV Play PC Toolkit
License: MIT
LicenseUrl: https://github.com/VetTV-Studios/vettv-play-pc-toolkit/blob/main/LICENSE
PackageUrl: https://github.com/VetTV-Studios/vettv-play-pc-toolkit
ShortDescription: Windows helper for Google Play Games on PC. Not affiliated with Google.
ManifestType: defaultLocale
ManifestVersion: 1.6.0
"@ | Set-Content (Join-Path $dir "$id.locale.en-US.yaml")
Write-Host "winget manifests written to $dir"
