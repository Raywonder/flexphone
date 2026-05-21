param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.5",
    [string]$Build = "76",
    [string]$OutputDir = "$env:USERPROFILE\Downloads\FlexPhone"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "FlexPhone\FlexPhone.csproj"
$publishDir = Join-Path $root "publish\$Runtime"
$artifactsDir = Join-Path $root "artifacts"
$iss = Join-Path $root "installer\FlexPhone.iss"
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$dotnet = "C:\Program Files\dotnet\dotnet.exe"

if (-not (Test-Path $iscc)) {
    throw "Inno Setup compiler not found at $iscc"
}
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

New-Item -ItemType Directory -Force -Path $publishDir, $artifactsDir, $OutputDir | Out-Null

& $dotnet publish $project -c $Configuration -r $Runtime --self-contained true -o $publishDir `
    /p:PublishSingleFile=false `
    /p:Version="$Version" `
    /p:FileVersion="$Version.$Build" `
    /p:InformationalVersion="$Version+$Build"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$portableName = "FlexPhone-$Version-$Runtime-portable.zip"
$portablePath = Join-Path $artifactsDir $portableName
if (Test-Path $portablePath) {
    Remove-Item -LiteralPath $portablePath -Force
}
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portablePath -Force
Copy-Item -LiteralPath $portablePath -Destination (Join-Path $OutputDir $portableName) -Force

& $iscc "/DAppVersion=$Version" "/DSourceDir=$publishDir" "/DOutputDir=$artifactsDir" $iss
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe exited with code $LASTEXITCODE"
}

$installerName = "FlexPhone-Setup-$Version.exe"
$installerPath = Join-Path $artifactsDir $installerName
Copy-Item -LiteralPath $installerPath -Destination (Join-Path $OutputDir $installerName) -Force

$installerHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installerPath).Hash.ToLowerInvariant()
$portableHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $portablePath).Hash.ToLowerInvariant()
$manifestPath = Join-Path $artifactsDir "flexphone-update.json"
$manifest = [ordered]@{
    version = "$Version.$Build"
    latest_version = "$Version.$Build"
    installer_url = "/downloads/flexphone/$installerName"
    download_url = "/downloads/flexphone/$installerName"
    portable_url = "/downloads/flexphone/$portableName"
    file_name = $installerName
    release_notes = "Flex Phone $Version.$Build keeps pbx.tappedin.fm as the default server, moves server changes into Advanced settings, adds show password, adds account menu actions for email linking, reset, and login pages, separates account login from SIP registration credentials when Flex PBX provides a SIP password, and refreshes updater metadata for the current installer."
    release_notes_list = @(
        "pbx.tappedin.fm is the normal default server.",
        "Changing server or provider domain now lives in Advanced settings.",
        "Existing sign-in includes a show password checkbox.",
        "The File, Account menu can link an email, reset password, open user login, and show admin login for admin roles.",
        "Email linking shows the old linked email when known and requires confirmation for the new email.",
        "Flex Phone uses a returned SIP password for SIP registration when it differs from the account login password.",
        "Updater metadata points to the current installer and portable build."
    )
    checksum = $installerHash
    checksum_sha256 = $installerHash
    portable_checksum_sha256 = $portableHash
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $OutputDir "flexphone-update.json") -Force

Write-Host "Installer: $installerPath"
Write-Host "Portable: $portablePath"
Write-Host "Manifest: $manifestPath"
