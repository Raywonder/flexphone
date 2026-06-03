# Flex Phone Native for Windows

Native Windows softphone for Flex PBX calling, built with .NET 8 and WPF.

## Features

- Flex PBX extension registration with multiple accounts.
- Up to eight line slots with call waiting state.
- Dial, answer, hold, resume, hang up, and DTMF controls.
- Browser login/provisioning and password reset links for Flex PBX accounts.
- Confirmed extension account recovery for username reset, password reset, and current-password request where the backend allows it.
- Presence status publishing for compatible Flex PBX backends.
- Auto-answer, intercom, tray, startup-to-tray, and default call sounds.
- Screen reader names and help text on the primary calling controls.

## Defaults

- Default Flex PBX server: `pbx.tappedin.fm`
- DevineCreations clients can choose `pbx.devinecreations.net`; saved `flexpbx.devinecreations.net` settings are migrated there.
- Default TURN server: `turn.tappedin.fm`
- Local package handoff: `C:\Users\40493\Downloads\flexphone\`

## Build

```powershell
.\build.ps1 -Clean -Publish -Version 1.0.4 -Build 54
```

Direct .NET commands:

```powershell
dotnet restore FlexPhone\FlexPhone.csproj
dotnet build FlexPhone\FlexPhone.csproj -c Release
dotnet publish FlexPhone\FlexPhone.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64 /p:PublishSingleFile=false
```

WPF is published as a folder build so native Windows dependencies resolve correctly.

## Repository Location

This Windows client lives at:

```text
clients/windows/native/
```

Generated folders such as `bin`, `obj`, `publish`, `dist`, and release binaries in `artifacts` should not be committed. The update manifest `artifacts/flexphone-update.json` may be committed when it is intentionally changed.

## Project Structure

```text
clients/windows/native/
|-- FlexPhone.sln
|-- FlexPhone/
|   |-- FlexPhone.csproj
|   |-- App.xaml
|   |-- App.xaml.cs
|   |-- Assets/Sounds/
|   |-- Models/
|   |-- Services/
|   |-- Views/MainWindow.xaml
|   `-- Views/MainWindow.xaml.cs
|-- build.ps1
|-- build.bat
`-- build-windows.bat
```

## Flex PBX Integration Contract

Compatible Flex PBX backends should provide:

- `/flexphone/link?client=flexphone&extension=...`
- `/user/password/reset?extension=...&client=flexphone`
- `/api/flexphone-account-recovery.php` accepting confirmed JSON recovery requests for `reset_username`, `reset_password`, and `get_current_password`.
- `/downloads/flexphone/`
- `/api/flexphone/status`
- `/api/login.php` for username or extension password login.
- `/api/flexphone-client.php` for device pairing, voicemail, waiting calls, server recording, recordings, and people status.

These routes stay backend-owned so hosted, local-network, and virtual-machine installs can expose the same client connection flow without embedding secrets in the desktop app.
