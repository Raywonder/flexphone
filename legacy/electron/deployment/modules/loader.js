/**
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
      console.log(`✅ Module ${manifest.name} loaded successfully`);

      return manifest;
    } catch (error) {
      console.error(`❌ Failed to load module: ${error.message}`);
      throw error;
    }
  }

  async loadDependency(depName) {
    try {
      // Check if dependency is installed
      require.resolve(depName);
      this.dependencies.set(depName, true);
    } catch (error) {
      console.warn(`⚠️ Dependency ${depName} not found, installing...`);
      // Auto-install dependency if needed
      const { execSync } = require('child_process');
      execSync(`npm install ${depName}`, { stdio: 'inherit' });
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
