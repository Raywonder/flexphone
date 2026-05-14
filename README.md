# Flex Phone

**Standalone SIP client with Flex PBX integration**

## Overview

Flex Phone is a lightweight, cross-platform SIP client designed to work seamlessly with Flex PBX systems while maintaining compatibility with third-party SIP providers.

## Repository Layout

```text
clients/
|-- windows/native/   # Native Windows WPF Flex Phone client
|-- macos/native/     # Native macOS Swift/AppKit or SwiftUI client
|-- ios/react-native/ # Existing iOS React Native work for review/reuse
|-- ios/pwa/          # Existing iOS/PWA install surface for review/reuse
`-- shared/assets/    # Reusable sounds and media assets
legacy/electron/      # Older Electron-era app and deployment modules for review
```

Keep platform-specific native UI code in `clients/<platform>/native`. Flex PBX server code belongs in the `flexpbx` repository, and VoiceLink chat/rooms code belongs in the `voicelink` repository.

## Key Features

### 🔐 **SIP Client Capabilities**
- Multi-provider SIP registration (CallCentric, Google Voice, custom providers)
- HD audio codecs (G.722, Opus, G.711)
- Real-time call management with hold, transfer, conference
- DTMF tone generation and detection
- Call recording and playback

### 🎯 **FlexPBX Integration**
- Enhanced features when connected to FlexPBX systems
- Extension-to-extension calling with presence
- Access to advanced PBX features (IVR, call queues, voicemail)
- Unified communications with chat and file sharing
- Administrative controls and monitoring

### ♿ **Accessibility**
- Full screen reader support (VoiceOver, NVDA, JAWS)
- High contrast themes and customizable fonts
- Complete keyboard navigation
- Audio feedback and voice prompts
- WCAG 2.1 AA compliance

### 🌐 **Cross-Platform**
- **Desktop**: macOS (Intel/ARM64), Windows, Linux
- **Mobile**: iOS and Android (React Native)
- **Web**: Progressive Web App (PWA)

## Getting Started

### Prerequisites
- Node.js 18+
- npm or yarn
- For mobile builds: React Native development environment

### Installation

```bash
# Clone the repository
git clone https://github.com/Raywonder/flexphone.git
cd flexphone

# Install legacy Electron dependencies
npm install --prefix legacy/electron

# Start legacy Electron development
npm run legacy:electron:dev
```

### Building

```bash
# Native Windows application
cd clients/windows/native
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Clean -Publish -Version 1.0.4 -Build 75

# Workspace build helpers
npm run build:windows
npm run build:windows:installer

# Legacy Electron app, for review or reuse
npm run legacy:electron:build
```

Root `npm` scripts are only orchestration helpers. Electron-era dependencies and package metadata live under `legacy/electron`.

## Configuration

### Basic SIP Setup

1. Configure your SIP provider in the settings
2. Enter your credentials (username, password, server)
3. Test the connection
4. Start making calls!

### FlexPBX Integration

When connected to a FlexPBX system, FlexPhone automatically detects and enables:
- Enhanced call features
- Extension directory
- Unified messaging
- Administrative tools (if authorized)

## Third-Party Provider Support

FlexPhone works with any standard SIP provider:
- **CallCentric**: Pre-configured templates
- **Google Voice**: Direct integration
- **Generic SIP**: Manual configuration
- **Custom Providers**: Full customization options

## Architecture

FlexPhone uses a modular architecture:
- **Core SIP Engine**: Standards-compliant SIP stack
- **FlexPBX Connector**: Enhanced integration layer
- **UI Framework**: Accessible, responsive interface
- **Platform Adapters**: Native desktop and mobile apps

## Development

### Project Structure
```text
clients/
|-- windows/native/
|-- macos/native/
|-- ios/react-native/
|-- ios/pwa/
`-- shared/assets/
legacy/
`-- electron/
```

### Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Support

- **Documentation**: [FlexPhone Docs](https://docs.flexpbx.com/flexphone)
- **Issues**: [GitHub Issues](https://github.com/Raywonder/flexphone/issues)
- **Community**: [FlexPBX Discord](https://discord.gg/flexpbx)

## Relationship to Flex PBX

While Flex Phone is a standalone application that works with any SIP provider, it offers enhanced functionality when paired with Flex PBX systems:

- **Standalone**: Basic SIP calling, standard features
- **Flex PBX Enhanced**: Advanced PBX features, unified communications, enterprise tools

Choose the deployment that best fits your needs!
