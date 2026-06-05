param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.5",
    [string]$Build = "82",
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
    release_notes = "Flex Phone $Version.$Build fixes extension sign-in feedback so the app announces Flex PBX login, waits for SIP registration, and reports PBX route or SIP registration failures instead of appearing to do nothing. It also includes the NVDA controller client library for direct NVDA status announcements when NVDA is running and keeps the TappedIn and DevineCreations PBX domain chooser."
    release_notes_list = @(
        "Sign-in now announces the login step, the phone registration step, and any registration timeout or route failure.",
        "Flex Phone now waits for SIP registration before treating extension sign-in as complete.",
        "PBX login responses can provide a separate SIP server, which Flex Phone now honors.",
        "Windows builds include nvdaControllerClient64.dll for direct NVDA announcements when NVDA is running.",
        "Sign-in now offers TappedIn Flex PBX and DevineCreations Flex PBX domain choices.",
        "pbx.tappedin.fm remains the normal default server.",
        "DevineCreations clients can choose pbx.devinecreations.net without typing it manually.",
        "Manual PBX domain entry remains available for custom or client-owned servers.",
        "Flex PBX web requests now allow more time and show a clearer timeout message.",
        "The Help menu now includes Check for updates now and Check for updates automatically.",
        "Updater metadata points to the current installer and portable build."
    )
    checksum = $installerHash
    checksum_sha256 = $installerHash
    portable_checksum_sha256 = $portableHash
}
[System.IO.File]::WriteAllText(
    $manifestPath,
    ($manifest | ConvertTo-Json -Depth 4),
    [System.Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $OutputDir "flexphone-update.json") -Force

Write-Host "Installer: $installerPath"
Write-Host "Portable: $portablePath"
Write-Host "Manifest: $manifestPath"
