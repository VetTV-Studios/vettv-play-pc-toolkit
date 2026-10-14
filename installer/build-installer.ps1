# Builds the self-contained app, (optionally) signs it, and packs an MSI + zip.
# Signing happens only when SIGN_PFX_PATH and SIGN_PFX_PASSWORD are set; otherwise the output is UNSIGNED.
param(
    [Parameter(Mandatory)][string]$Version,   # numeric, e.g. 0.3.0
    [string]$OutDir = "artifacts"
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be MAJOR.MINOR.PATCH, got '$Version'." }

$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root $OutDir
$publish = Join-Path $out 'publish'
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $publish | Out-Null

dotnet publish (Join-Path $root 'src/VetTV.PlayPC.App/VetTV.PlayPC.App.csproj') -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:DebugType=none -o $publish
if ($LASTEXITCODE) { throw 'dotnet publish failed' }
foreach ($exe in 'VetTV.PlayPC.App.exe', 'VetTV.PlayPC.Elevated.exe') {
    if (-not (Test-Path (Join-Path $publish $exe))) { throw "$exe missing from publish output" }
}
Copy-Item (Join-Path $root 'src/VetTV.PlayPC.PowerShell') (Join-Path $publish 'PowerShell') -Recurse
Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'README.md') $publish

$signed = $false
function Invoke-Sign([string[]]$Files) {
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $signtool) { throw 'signtool.exe not found' }
    & $signtool.FullName sign /f $env:SIGN_PFX_PATH /p $env:SIGN_PFX_PASSWORD /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $Files
    if ($LASTEXITCODE) { throw 'signing failed' }
}
if ($env:SIGN_PFX_PATH -and $env:SIGN_PFX_PASSWORD) {
    Invoke-Sign @((Join-Path $publish 'VetTV.PlayPC.App.exe'), (Join-Path $publish 'VetTV.PlayPC.Elevated.exe'))
    $signed = $true
} else {
    Write-Warning 'No signing certificate configured: building UNSIGNED binaries and MSI.'
}

$msi = Join-Path $out "VetTV.PlayPC.Toolkit-$Version-x64.msi"
wix build (Join-Path $PSScriptRoot 'VetTV.PlayPC.wxs') -arch x64 -d "Version=$Version" -d "PublishDir=$publish" -o $msi
if ($LASTEXITCODE) { throw 'wix build failed' }
if ($signed) { Invoke-Sign @($msi) }

$zip = Join-Path $out "VetTV.PlayPC.Toolkit-$Version-win-x64-portable.zip"
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip
Remove-Item (Join-Path $out '*.wixpdb') -ErrorAction SilentlyContinue

$sums = Get-ChildItem $out -File | ForEach-Object { "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)" }
$sums | Set-Content (Join-Path $out 'SHA256SUMS.txt')
"signed=$($signed.ToString().ToLower())" | Set-Content (Join-Path $out 'SIGNING.txt')
Write-Host "Built $msi (signed: $signed)"
