param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.4",
    [string]$Build = "73",
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
    release_notes = "Flex Phone $Version.$Build hides update URLs behind advanced settings, adds provider selection, clarifies new extension provisioning and existing extension sign-in, signs in remembered accounts automatically on launch, uses the configured queue toggle code defaulting to *45, routes Voicemail to the configured voicemail code defaulting to *97, adds Directory with readable people names and quiet 30/60 second background refresh, fixes double-Escape hangup across active lines, stops incoming calls from stealing keyboard focus, adds minimized global call hotkeys, call-safe updates, update postponing, accessible update notes, keypad navigation, People actions, remembered sign-in, SMS/provider messages, and trusted-network firewall setup."
    release_notes_list = @(
        "Updates wait until calls are over before installing.",
        "Incoming calls notify without stealing keyboard focus.",
        "Update URLs and backend paths are hidden behind Advanced server paths with a warning.",
        "Provider selection supports Flex PBX, Callcentric, or another SIP provider.",
        "Queue toggle uses the configured queue code, defaulting to *45.",
        "Voicemail routes to the configured voicemail code, defaulting to *97.",
        "Directory opens the Flex PBX people directory.",
        "Directory entries read as name, extension, role, and status instead of raw app type names.",
        "The Directory refreshes quietly in the background and closes with Escape.",
        "Remembered accounts sign in automatically when Flex Phone opens.",
        "Double-Escape hangup now checks active calls across lines.",
        "Double-Escape minimizes Flex Phone from the main window when no call is active.",
        "Control+Alt+A answers, Control+Alt+D declines, and Control+Alt+H answers and holds incoming calls while minimized.",
        "Users can postpone an update for a short time until the postpone limit is reached.",
        "Update notes and links are shown as list items for screen readers.",
        "The keypad is one tab stop with arrow-key navigation in a phone layout.",
        "The People window uses a listbox and context-menu actions."
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
