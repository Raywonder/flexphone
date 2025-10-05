# FlexPhone Deployment Guide

## Overview
FlexPhone portable deployment package with modular architecture for easy distribution and deployment.

## Package Contents

### 📦 Portable Applications
- **macOS**: FlexPhone.app (Universal - Intel & ARM64)
- **Windows**: FlexPhone.exe (32-bit & 64-bit)
- **Linux**: FlexPhone.AppImage

### 🧩 Modular Architecture
- **Core Module**: Main application framework
- **SIP Module**: VoIP calling functionality
- **UI Module**: User interface components
- **Services Module**: Backend services (Contacts, SMS, Call History)

## Quick Start

### Option 1: Portable Deployment
1. Navigate to `deployment/portable/`
2. Choose your platform package
3. Run the application directly (no installation required)

### Option 2: Modular Deployment
1. Install Node.js if not present
2. Navigate to `deployment/modules/`
3. Run: `node deploy.js --install`
4. Configure using templates in `config/`

## Configuration

### Basic Configuration
Edit `config/default.json`:
- SIP provider settings
- UI preferences
- Feature toggles

### Enterprise Configuration
Use `config/enterprise.json` for:
- Custom SIP servers
- Security settings
- Managed deployment

## Module Management

### Loading Modules
```javascript
const ModuleLoader = require('./modules/loader');
const loader = new ModuleLoader();

// Load specific module
await loader.loadModule('./modules/sip');

// Load all modules
const moduleKeys = ['core', 'sip', 'ui', 'services'];
for (const key of moduleKeys) {
  await loader.loadModule(`./modules/${key}`);
}
```

### Module Verification
Each module includes SHA-256 checksum verification for integrity.

## Platform-Specific Notes

### macOS
- Universal binary supports Intel and Apple Silicon
- Code signed for Gatekeeper compliance
- Notarization ready

### Windows
- NSIS installer for system-wide installation
- Portable executable for USB deployment
- Windows Defender SmartScreen compatible

### Linux
- AppImage for distribution independence
- Works on most Linux distributions
- No root required for portable version

## Troubleshooting

### Module Load Failures
- Verify checksums in module.json
- Check Node.js version compatibility
- Ensure all dependencies are installed

### SIP Connection Issues
- Verify network connectivity
- Check firewall settings for ports 5060-5061
- Confirm SIP credentials in configuration

## Support
- GitHub: https://github.com/flexpbx/flexphone
- Email: support@flexpbx.com
- Documentation: https://docs.flexpbx.com

## License
MIT License - See LICENSE file for details
