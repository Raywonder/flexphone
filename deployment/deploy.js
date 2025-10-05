#!/usr/bin/env node
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
