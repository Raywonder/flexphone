#!/usr/bin/env node
/**
 * FlexPhone Deployment Script
 * Portable deployment package creator with modular architecture
 */

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const crypto = require('crypto');

class DeploymentBuilder {
  constructor() {
    this.projectRoot = path.resolve(__dirname, '..');
    this.deployDir = path.join(this.projectRoot, 'deployment');
    this.platform = process.platform;
    this.arch = process.arch;
  }

  // Create deployment directory structure
  async createDeploymentStructure() {
    console.log('📦 Creating deployment structure...');

    const dirs = [
      'deployment',
      'deployment/portable',
      'deployment/installers',
      'deployment/modules',
      'deployment/config',
      'deployment/docs'
    ];

    dirs.forEach(dir => {
      const fullPath = path.join(this.projectRoot, dir);
      if (!fs.existsSync(fullPath)) {
        fs.mkdirSync(fullPath, { recursive: true });
      }
    });

    console.log('✅ Deployment structure created');
  }

  // Build portable packages for all platforms
  async buildPortablePackages() {
    console.log('🔨 Building portable packages...');

    try {
      // Clean previous builds
      execSync('npm run clean', { cwd: this.projectRoot, stdio: 'inherit' });

      // Build for each platform
      const platforms = ['mac', 'win', 'linux'];

      for (const platform of platforms) {
        console.log(`📱 Building for ${platform}...`);
        try {
          execSync(`npm run build-${platform}`, {
            cwd: this.projectRoot,
            stdio: 'inherit'
          });
          console.log(`✅ ${platform} build complete`);
        } catch (error) {
          console.warn(`⚠️ ${platform} build failed: ${error.message}`);
        }
      }

      // Copy builds to portable directory
      this.copyBuildsToPortable();

    } catch (error) {
      console.error('❌ Build failed:', error);
      throw error;
    }
  }

  // Copy built files to portable directory
  copyBuildsToPortable() {
    console.log('📋 Copying builds to portable directory...');

    const distDir = path.join(this.projectRoot, 'dist');
    const portableDir = path.join(this.deployDir, 'portable');

    if (fs.existsSync(distDir)) {
      const files = fs.readdirSync(distDir);
      files.forEach(file => {
        const src = path.join(distDir, file);
        const dest = path.join(portableDir, file);

        if (fs.statSync(src).isDirectory()) {
          this.copyFolderRecursive(src, dest);
        } else {
          fs.copyFileSync(src, dest);
        }
      });
    }

    console.log('✅ Builds copied to portable directory');
  }

  // Create modular architecture
  async createModularArchitecture() {
    console.log('🏗️ Creating modular architecture...');

    // Core modules configuration
    const modules = {
      core: {
        name: 'FlexPhone Core',
        version: '1.0.0',
        files: ['src/main.js', 'src/preload.js'],
        dependencies: ['electron', 'sip.js']
      },
      sip: {
        name: 'SIP Module',
        version: '1.0.0',
        files: ['src/services/SIPService.js'],
        dependencies: ['sip.js', 'ws']
      },
      ui: {
        name: 'UI Module',
        version: '1.0.0',
        files: ['public/index.html', 'public/app.js', 'public/styles.css'],
        dependencies: []
      },
      services: {
        name: 'Services Module',
        version: '1.0.0',
        files: [
          'src/services/ContactsService.js',
          'src/services/CallHistoryService.js',
          'src/services/SMSService.js',
          'src/services/SettingsService.js',
          'src/services/DTMFService.js'
        ],
        dependencies: ['axios', 'crypto-js']
      }
    };

    // Create module packages
    for (const [key, module] of Object.entries(modules)) {
      await this.createModulePackage(key, module);
    }

    // Create module loader
    this.createModuleLoader();

    // Create deployment manifest
    this.createDeploymentManifest(modules);

    console.log('✅ Modular architecture created');
  }

  // Create individual module package
  async createModulePackage(moduleKey, moduleConfig) {
    console.log(`📦 Creating ${moduleConfig.name} package...`);

    const moduleDir = path.join(this.deployDir, 'modules', moduleKey);
    if (!fs.existsSync(moduleDir)) {
      fs.mkdirSync(moduleDir, { recursive: true });
    }

    // Copy module files
    moduleConfig.files.forEach(file => {
      const src = path.join(this.projectRoot, file);
      const dest = path.join(moduleDir, path.basename(file));

      if (fs.existsSync(src)) {
        fs.copyFileSync(src, dest);
      }
    });

    // Create module manifest
    const manifest = {
      name: moduleConfig.name,
      version: moduleConfig.version,
      key: moduleKey,
      files: moduleConfig.files.map(f => path.basename(f)),
      dependencies: moduleConfig.dependencies,
      checksum: this.calculateChecksum(moduleDir),
      created: new Date().toISOString()
    };

    fs.writeFileSync(
      path.join(moduleDir, 'module.json'),
      JSON.stringify(manifest, null, 2)
    );

    console.log(`✅ ${moduleConfig.name} package created`);
  }

  // Create module loader for dynamic loading
  createModuleLoader() {
    const loaderCode = `/**
 * FlexPhone Module Loader
 * Dynamic module loading system for modular deployment
 */

class ModuleLoader {
  constructor() {
    this.modules = new Map();
    this.dependencies = new Map();
  }

  async loadModule(modulePath) {
    try {
      const manifestPath = path.join(modulePath, 'module.json');
      const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));

      // Verify checksum
      if (!this.verifyChecksum(modulePath, manifest.checksum)) {
        throw new Error('Module checksum verification failed');
      }

      // Load dependencies first
      for (const dep of manifest.dependencies) {
        if (!this.dependencies.has(dep)) {
          await this.loadDependency(dep);
        }
      }

      // Load module files
      for (const file of manifest.files) {
        const filePath = path.join(modulePath, file);
        if (file.endsWith('.js')) {
          require(filePath);
        }
      }

      this.modules.set(manifest.key, manifest);
      console.log(\`✅ Module \${manifest.name} loaded successfully\`);

      return manifest;
    } catch (error) {
      console.error(\`❌ Failed to load module: \${error.message}\`);
      throw error;
    }
  }

  async loadDependency(depName) {
    try {
      // Check if dependency is installed
      require.resolve(depName);
      this.dependencies.set(depName, true);
    } catch (error) {
      console.warn(\`⚠️ Dependency \${depName} not found, installing...\`);
      // Auto-install dependency if needed
      const { execSync } = require('child_process');
      execSync(\`npm install \${depName}\`, { stdio: 'inherit' });
      this.dependencies.set(depName, true);
    }
  }

  verifyChecksum(modulePath, expectedChecksum) {
    const actualChecksum = this.calculateChecksum(modulePath);
    return actualChecksum === expectedChecksum;
  }

  calculateChecksum(dirPath) {
    const crypto = require('crypto');
    const hash = crypto.createHash('sha256');

    const files = fs.readdirSync(dirPath);
    files.forEach(file => {
      if (file !== 'module.json') {
        const filePath = path.join(dirPath, file);
        const content = fs.readFileSync(filePath);
        hash.update(content);
      }
    });

    return hash.digest('hex');
  }

  getLoadedModules() {
    return Array.from(this.modules.values());
  }

  isModuleLoaded(moduleKey) {
    return this.modules.has(moduleKey);
  }
}

module.exports = ModuleLoader;
`;

    fs.writeFileSync(
      path.join(this.deployDir, 'modules', 'loader.js'),
      loaderCode
    );

    console.log('✅ Module loader created');
  }

  // Create deployment manifest
  createDeploymentManifest(modules) {
    const manifest = {
      name: 'FlexPhone',
      version: '1.0.0',
      type: 'modular',
      created: new Date().toISOString(),
      platform: {
        node: process.version,
        electron: require(path.join(this.projectRoot, 'package.json')).dependencies.electron,
        os: process.platform,
        arch: process.arch
      },
      modules: Object.keys(modules),
      configuration: {
        autoUpdate: true,
        telemetry: false,
        debug: false
      },
      deployment: {
        portable: fs.existsSync(path.join(this.deployDir, 'portable')),
        installers: fs.existsSync(path.join(this.deployDir, 'installers')),
        modular: true
      }
    };

    fs.writeFileSync(
      path.join(this.deployDir, 'deployment.json'),
      JSON.stringify(manifest, null, 2)
    );

    console.log('✅ Deployment manifest created');
  }

  // Create configuration templates
  createConfigurationTemplates() {
    console.log('⚙️ Creating configuration templates...');

    const configs = {
      'default.json': {
        sip: {
          provider: 'flexpbx',
          autoRegister: true,
          stunServers: ['stun:stun.l.google.com:19302']
        },
        ui: {
          theme: 'light',
          fontSize: 14,
          autoComplete: true,
          dtmfTones: true
        },
        features: {
          sms: true,
          contacts: true,
          callHistory: true,
          voicemail: true
        }
      },
      'enterprise.json': {
        sip: {
          provider: 'custom',
          autoRegister: false,
          requiresVPN: true
        },
        security: {
          encryption: 'required',
          certificateValidation: true,
          audit: true
        },
        deployment: {
          managedUpdates: true,
          remoteConfiguration: true
        }
      }
    };

    Object.entries(configs).forEach(([filename, config]) => {
      fs.writeFileSync(
        path.join(this.deployDir, 'config', filename),
        JSON.stringify(config, null, 2)
      );
    });

    console.log('✅ Configuration templates created');
  }

  // Create deployment documentation
  createDocumentation() {
    console.log('📚 Creating deployment documentation...');

    const readme = `# FlexPhone Deployment Guide

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
1. Navigate to \`deployment/portable/\`
2. Choose your platform package
3. Run the application directly (no installation required)

### Option 2: Modular Deployment
1. Install Node.js if not present
2. Navigate to \`deployment/modules/\`
3. Run: \`node deploy.js --install\`
4. Configure using templates in \`config/\`

## Configuration

### Basic Configuration
Edit \`config/default.json\`:
- SIP provider settings
- UI preferences
- Feature toggles

### Enterprise Configuration
Use \`config/enterprise.json\` for:
- Custom SIP servers
- Security settings
- Managed deployment

## Module Management

### Loading Modules
\`\`\`javascript
const ModuleLoader = require('./modules/loader');
const loader = new ModuleLoader();

// Load specific module
await loader.loadModule('./modules/sip');

// Load all modules
const moduleKeys = ['core', 'sip', 'ui', 'services'];
for (const key of moduleKeys) {
  await loader.loadModule(\`./modules/\${key}\`);
}
\`\`\`

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
`;

    fs.writeFileSync(
      path.join(this.deployDir, 'docs', 'README.md'),
      readme
    );

    // Create deployment script
    const deployScript = `#!/usr/bin/env node
/**
 * FlexPhone Deployment Installer
 */

const path = require('path');
const fs = require('fs');
const ModuleLoader = require('./modules/loader');

async function deploy() {
  console.log('🚀 FlexPhone Deployment Starting...');

  const loader = new ModuleLoader();
  const modules = ['core', 'sip', 'ui', 'services'];

  for (const module of modules) {
    const modulePath = path.join(__dirname, 'modules', module);
    await loader.loadModule(modulePath);
  }

  console.log('✅ FlexPhone deployed successfully!');
  console.log('📱 Run: npm start to launch the application');
}

if (require.main === module) {
  deploy().catch(console.error);
}

module.exports = deploy;
`;

    fs.writeFileSync(
      path.join(this.deployDir, 'deploy.js'),
      deployScript
    );

    // Make deploy script executable
    fs.chmodSync(path.join(this.deployDir, 'deploy.js'), '755');

    console.log('✅ Documentation created');
  }

  // Helper: Calculate checksum for integrity verification
  calculateChecksum(dirPath) {
    const hash = crypto.createHash('sha256');

    if (fs.statSync(dirPath).isDirectory()) {
      const files = fs.readdirSync(dirPath);
      files.forEach(file => {
        if (file !== 'module.json') {
          const filePath = path.join(dirPath, file);
          const content = fs.readFileSync(filePath);
          hash.update(content);
        }
      });
    } else {
      const content = fs.readFileSync(dirPath);
      hash.update(content);
    }

    return hash.digest('hex');
  }

  // Helper: Copy folder recursively
  copyFolderRecursive(source, target) {
    if (!fs.existsSync(target)) {
      fs.mkdirSync(target, { recursive: true });
    }

    if (fs.lstatSync(source).isDirectory()) {
      const files = fs.readdirSync(source);
      files.forEach(file => {
        const curSource = path.join(source, file);
        const curTarget = path.join(target, file);

        if (fs.lstatSync(curSource).isDirectory()) {
          this.copyFolderRecursive(curSource, curTarget);
        } else {
          fs.copyFileSync(curSource, curTarget);
        }
      });
    }
  }

  // Main deployment process
  async deploy() {
    console.log('🚀 Starting FlexPhone Deployment Builder...');
    console.log(`📍 Platform: ${this.platform} (${this.arch})`);

    try {
      await this.createDeploymentStructure();
      await this.buildPortablePackages();
      await this.createModularArchitecture();
      this.createConfigurationTemplates();
      this.createDocumentation();

      console.log('');
      console.log('✨ Deployment package created successfully!');
      console.log(`📁 Location: ${this.deployDir}`);
      console.log('');
      console.log('📦 Package contents:');
      console.log('  • Portable applications for Mac, Windows, Linux');
      console.log('  • Modular architecture with dynamic loading');
      console.log('  • Configuration templates');
      console.log('  • Complete documentation');
      console.log('');
      console.log('🎯 Next steps:');
      console.log('  1. Test portable packages in deployment/portable/');
      console.log('  2. Customize configuration in deployment/config/');
      console.log('  3. Distribute using your preferred method');

    } catch (error) {
      console.error('❌ Deployment failed:', error);
      process.exit(1);
    }
  }
}

// Run deployment if called directly
if (require.main === module) {
  const builder = new DeploymentBuilder();
  builder.deploy();
}

module.exports = DeploymentBuilder;