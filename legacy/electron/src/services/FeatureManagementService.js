/**
 * 🎛️ FlexPhone Dynamic Feature Management Service
 * Allows admin versions to push features and unlock capabilities dynamically
 */

const EventEmitter = require('events');
const fs = require('fs');
const path = require('path');
const os = require('os');

class FeatureManagementService extends EventEmitter {
    constructor() {
        super();
        this.features = new Map();
        this.licenseLevel = 'community'; // community, professional, enterprise, lifetime
        this.adminModules = new Map();
        this.networkDetectedInstances = [];
        this.localDetectedInstances = [];
        this.featureConfigPath = path.join(os.homedir(), '.flexphone', 'features.json');
        this.lastSync = null;
        this.autoSyncInterval = null;

        // Default feature definitions
        this.defaultFeatures = {
            // Core Features (Always Available)
            'core.dialer': { enabled: true, level: 'community', name: 'Basic Dialer' },
            'core.contacts': { enabled: true, level: 'community', name: 'Contacts Management' },
            'core.sip_basic': { enabled: true, level: 'community', name: 'Basic SIP Support' },
            'core.accessibility': { enabled: true, level: 'community', name: 'Accessibility Features' },
            'core.ringtones_basic': { enabled: true, level: 'community', name: 'Basic Ringtones' },

            // Professional Features
            'pro.external_providers': { enabled: false, level: 'professional', name: 'External SIP Providers', maxProviders: 2 },
            'pro.call_recording_local': { enabled: false, level: 'professional', name: 'Local Call Recording' },
            'pro.concurrent_calls': { enabled: false, level: 'professional', name: 'Multiple Concurrent Calls', maxCalls: 5 },
            'pro.call_waiting': { enabled: false, level: 'professional', name: 'Call Waiting' },
            'pro.voicemail_advanced': { enabled: false, level: 'professional', name: 'Advanced Voicemail' },
            'pro.conference_calls': { enabled: false, level: 'professional', name: 'Conference Calling' },

            // Enterprise Features
            'ent.cloud_recording': { enabled: false, level: 'enterprise', name: 'Cloud Call Recording' },
            'ent.analytics': { enabled: false, level: 'enterprise', name: 'Call Analytics' },
            'ent.multi_tenant': { enabled: false, level: 'enterprise', name: 'Multi-Tenant Support' },
            'ent.api_access': { enabled: false, level: 'enterprise', name: 'API Access' },
            'ent.custom_integrations': { enabled: false, level: 'enterprise', name: 'Custom Integrations' },
            'ent.unlimited_providers': { enabled: false, level: 'enterprise', name: 'Unlimited SIP Providers' },

            // Admin-Only Features
            'admin.feature_push': { enabled: false, level: 'admin', name: 'Feature Push to Other Instances' },
            'admin.license_management': { enabled: false, level: 'admin', name: 'License Management' },
            'admin.global_settings': { enabled: false, level: 'admin', name: 'Global Settings Management' },
            'admin.user_management': { enabled: false, level: 'admin', name: 'User Management' },
            'admin.server_monitoring': { enabled: false, level: 'admin', name: 'Server Monitoring' },

            // Dynamic Features (Pushed by Admin)
            'dynamic.custom_modules': { enabled: false, level: 'dynamic', name: 'Custom Modules' },
            'dynamic.beta_features': { enabled: false, level: 'dynamic', name: 'Beta Features' },
            'dynamic.special_integrations': { enabled: false, level: 'dynamic', name: 'Special Integrations' }
        };

        this.initializeFeatures();
        console.log('🎛️ FeatureManagementService initialized');
    }

    async initialize() {
        try {
            await this.loadFeaturesFromDisk();
            await this.detectLocalInstances();
            // Skip network detection for now - it blocks initialization
            // await this.detectNetworkInstances();

            // Start auto-sync for dynamic features
            this.startAutoSync();

            console.log(`✅ FeatureManagementService ready - License: ${this.licenseLevel}`);
            this.emit('initialized', { licenseLevel: this.licenseLevel });

            return true;
        } catch (error) {
            console.error('❌ FeatureManagementService initialization failed:', error);
            return false;
        }
    }

    /**
     * Initialize default features based on license level
     */
    initializeFeatures() {
        this.features.clear();

        for (const [featureId, config] of Object.entries(this.defaultFeatures)) {
            this.features.set(featureId, {
                ...config,
                enabled: this.isFeatureAllowed(config.level)
            });
        }
    }

    /**
     * Check if a feature level is allowed for current license
     */
    isFeatureAllowed(requiredLevel) {
        const levelHierarchy = {
            'community': 1,
            'professional': 2,
            'enterprise': 3,
            'lifetime': 2, // Same as professional but permanent
            'admin': 4,
            'dynamic': 5 // Highest level, requires admin push
        };

        const currentLevel = levelHierarchy[this.licenseLevel] || 1;
        const required = levelHierarchy[requiredLevel] || 1;

        return currentLevel >= required;
    }

    /**
     * Enable a specific feature
     */
    enableFeature(featureId, adminOverride = false) {
        const feature = this.features.get(featureId);

        if (!feature) {
            console.warn(`⚠️ Unknown feature: ${featureId}`);
            return false;
        }

        if (!adminOverride && !this.isFeatureAllowed(feature.level)) {
            console.warn(`⚠️ Feature ${featureId} requires ${feature.level} license`);
            return false;
        }

        feature.enabled = true;
        this.features.set(featureId, feature);

        console.log(`✅ Feature enabled: ${feature.name}`);
        this.emit('featureEnabled', { featureId, feature });

        return true;
    }

    /**
     * Disable a specific feature
     */
    disableFeature(featureId) {
        const feature = this.features.get(featureId);

        if (!feature) {
            console.warn(`⚠️ Unknown feature: ${featureId}`);
            return false;
        }

        feature.enabled = false;
        this.features.set(featureId, feature);

        console.log(`❌ Feature disabled: ${feature.name}`);
        this.emit('featureDisabled', { featureId, feature });

        return true;
    }

    /**
     * Check if a feature is enabled
     */
    isFeatureEnabled(featureId) {
        const feature = this.features.get(featureId);
        return feature ? feature.enabled : false;
    }

    /**
     * Get feature limits (e.g., max concurrent calls)
     */
    getFeatureLimit(featureId, limitType) {
        const feature = this.features.get(featureId);

        if (!feature || !feature.enabled) {
            return 0;
        }

        return feature[limitType] || Infinity;
    }

    /**
     * Set license level and update features
     */
    setLicenseLevel(level, licenseKey = null) {
        const validLevels = ['community', 'professional', 'enterprise', 'lifetime', 'admin'];

        if (!validLevels.includes(level)) {
            console.error(`❌ Invalid license level: ${level}`);
            return false;
        }

        const oldLevel = this.licenseLevel;
        this.licenseLevel = level;

        // Re-initialize features with new license level
        this.initializeFeatures();

        console.log(`🎫 License updated: ${oldLevel} → ${level}`);
        this.emit('licenseChanged', { oldLevel, newLevel: level, licenseKey });

        // Save to disk
        this.saveFeaturesToDisk();

        return true;
    }

    /**
     * Detect local FlexPhone instances on the same machine
     */
    async detectLocalInstances() {
        try {
            const commonPaths = [
                '/Applications/FlexPhone.app',
                '/Applications/FlexPBX Admin.app',
                '/Applications/FlexPBX.app',
                path.join(os.homedir(), 'Applications', 'FlexPhone.app'),
                path.join(os.homedir(), 'Desktop', 'FlexPhone.app')
            ];

            this.localDetectedInstances = [];

            for (const appPath of commonPaths) {
                if (fs.existsSync(appPath)) {
                    const packagePath = path.join(appPath, 'Contents', 'Resources', 'app', 'package.json');

                    let appInfo = {
                        path: appPath,
                        name: path.basename(appPath, '.app'),
                        type: appPath.includes('Admin') ? 'admin' : 'public',
                        version: 'unknown'
                    };

                    if (fs.existsSync(packagePath)) {
                        try {
                            const packageData = JSON.parse(fs.readFileSync(packagePath, 'utf8'));
                            appInfo.version = packageData.version || 'unknown';
                        } catch (error) {
                            console.warn(`⚠️ Could not read package.json for ${appPath}`);
                        }
                    }

                    this.localDetectedInstances.push(appInfo);
                }
            }

            console.log(`🔍 Detected ${this.localDetectedInstances.length} local FlexPhone instances`);
            this.emit('localInstancesDetected', { instances: this.localDetectedInstances });

            return this.localDetectedInstances;

        } catch (error) {
            console.error('❌ Failed to detect local instances:', error);
            return [];
        }
    }

    /**
     * Detect FlexPhone instances on the network
     */
    async detectNetworkInstances() {
        try {
            // Scan common ports for FlexPhone/FlexPBX services
            const commonPorts = [8080, 8081, 3000, 3001, 5060, 5061];
            const networkBase = '192.168.1'; // Common network base

            this.networkDetectedInstances = [];

            // Simple network detection (would be enhanced in production)
            for (let i = 1; i <= 254; i++) {
                const ip = `${networkBase}.${i}`;

                for (const port of commonPorts) {
                    try {
                        const response = await fetch(`http://${ip}:${port}/api/flexphone/info`, {
                            method: 'GET',
                            timeout: 1000
                        });

                        if (response.ok) {
                            const info = await response.json();
                            this.networkDetectedInstances.push({
                                ip,
                                port,
                                type: info.type || 'unknown',
                                version: info.version || 'unknown',
                                name: info.name || `FlexPhone@${ip}`,
                                capabilities: info.capabilities || []
                            });
                        }
                    } catch (error) {
                        // Ignore connection errors (expected for most IPs)
                    }
                }
            }

            console.log(`🌐 Detected ${this.networkDetectedInstances.length} network FlexPhone instances`);
            this.emit('networkInstancesDetected', { instances: this.networkDetectedInstances });

            return this.networkDetectedInstances;

        } catch (error) {
            console.error('❌ Failed to detect network instances:', error);
            return [];
        }
    }

    /**
     * Push features to detected instances (Admin only)
     */
    async pushFeaturesToInstances(targetInstances, features) {
        if (!this.isFeatureEnabled('admin.feature_push')) {
            console.error('❌ Feature push requires admin privileges');
            return false;
        }

        const results = [];

        for (const instance of targetInstances) {
            try {
                const endpoint = instance.ip ?
                    `http://${instance.ip}:${instance.port}/api/features/push` :
                    `file://${instance.path}/features-push.json`;

                if (instance.ip) {
                    // Network instance - HTTP push
                    const response = await fetch(endpoint, {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': `Bearer ${this.getAdminToken()}`
                        },
                        body: JSON.stringify({
                            features,
                            pushedBy: 'FlexPhone Admin',
                            timestamp: new Date().toISOString()
                        })
                    });

                    results.push({
                        instance: instance.name,
                        success: response.ok,
                        status: response.status
                    });

                } else {
                    // Local instance - File-based push
                    const pushData = {
                        features,
                        pushedBy: 'FlexPhone Admin',
                        timestamp: new Date().toISOString()
                    };

                    const pushFile = path.join(instance.path, 'Contents', 'Resources', 'features-push.json');
                    fs.writeFileSync(pushFile, JSON.stringify(pushData, null, 2));

                    results.push({
                        instance: instance.name,
                        success: true,
                        method: 'file'
                    });
                }

                console.log(`📡 Pushed features to ${instance.name}`);

            } catch (error) {
                console.error(`❌ Failed to push to ${instance.name}:`, error);
                results.push({
                    instance: instance.name,
                    success: false,
                    error: error.message
                });
            }
        }

        this.emit('featuresPushed', { results, features });
        return results;
    }

    /**
     * Receive pushed features from admin instances
     */
    async receivePushedFeatures(pushedData) {
        try {
            console.log('📥 Receiving pushed features from admin...');

            for (const [featureId, config] of Object.entries(pushedData.features)) {
                // Validate the feature before applying
                if (this.validatePushedFeature(featureId, config)) {
                    this.features.set(featureId, {
                        ...config,
                        pushedBy: pushedData.pushedBy,
                        pushedAt: pushedData.timestamp
                    });

                    console.log(`✅ Applied pushed feature: ${config.name}`);
                }
            }

            // Save updated features
            await this.saveFeaturesToDisk();

            this.emit('pushedFeaturesReceived', { pushedData });

            return true;

        } catch (error) {
            console.error('❌ Failed to receive pushed features:', error);
            return false;
        }
    }

    /**
     * Validate a pushed feature before applying
     */
    validatePushedFeature(featureId, config) {
        // Basic validation
        if (!featureId || !config || !config.name) {
            console.warn(`⚠️ Invalid feature data: ${featureId}`);
            return false;
        }

        // Security check - don't allow certain admin features to be pushed
        const restrictedFeatures = ['admin.license_management', 'admin.user_management'];
        if (restrictedFeatures.includes(featureId)) {
            console.warn(`⚠️ Restricted feature cannot be pushed: ${featureId}`);
            return false;
        }

        return true;
    }

    /**
     * Start auto-sync for dynamic features
     */
    startAutoSync() {
        if (this.autoSyncInterval) {
            clearInterval(this.autoSyncInterval);
        }

        // Sync every 5 minutes
        this.autoSyncInterval = setInterval(async () => {
            await this.syncDynamicFeatures();
        }, 5 * 60 * 1000);

        console.log('🔄 Auto-sync started for dynamic features');
    }

    /**
     * Sync dynamic features from admin instances
     */
    async syncDynamicFeatures() {
        try {
            // Check for pushed features file
            const pushFile = path.join(process.cwd(), 'features-push.json');

            if (fs.existsSync(pushFile)) {
                const pushedData = JSON.parse(fs.readFileSync(pushFile, 'utf8'));
                await this.receivePushedFeatures(pushedData);

                // Remove the file after processing
                fs.unlinkSync(pushFile);
            }

            // Also check network instances for updates
            // Skip network detection for now - it blocks initialization
            // await this.detectNetworkInstances();

            this.lastSync = new Date();

        } catch (error) {
            console.error('❌ Auto-sync failed:', error);
        }
    }

    /**
     * Save features configuration to disk
     */
    async saveFeaturesToDisk() {
        try {
            const configDir = path.dirname(this.featureConfigPath);

            if (!fs.existsSync(configDir)) {
                fs.mkdirSync(configDir, { recursive: true });
            }

            const config = {
                licenseLevel: this.licenseLevel,
                features: Object.fromEntries(this.features),
                lastUpdated: new Date().toISOString(),
                localInstances: this.localDetectedInstances,
                networkInstances: this.networkDetectedInstances
            };

            fs.writeFileSync(this.featureConfigPath, JSON.stringify(config, null, 2));
            console.log('💾 Features configuration saved');

        } catch (error) {
            console.error('❌ Failed to save features configuration:', error);
        }
    }

    /**
     * Load features configuration from disk
     */
    async loadFeaturesFromDisk() {
        try {
            if (fs.existsSync(this.featureConfigPath)) {
                const config = JSON.parse(fs.readFileSync(this.featureConfigPath, 'utf8'));

                this.licenseLevel = config.licenseLevel || 'community';

                if (config.features) {
                    this.features.clear();
                    for (const [featureId, featureConfig] of Object.entries(config.features)) {
                        this.features.set(featureId, featureConfig);
                    }
                }

                this.localDetectedInstances = config.localInstances || [];
                this.networkDetectedInstances = config.networkInstances || [];

                console.log('📁 Features configuration loaded from disk');
            }
        } catch (error) {
            console.error('❌ Failed to load features configuration:', error);
            // Fall back to defaults
            this.initializeFeatures();
        }
    }

    /**
     * Get admin token for authentication
     */
    getAdminToken() {
        // In a real implementation, this would be a secure token
        return 'flexphone-admin-token-' + Date.now();
    }

    /**
     * Get all enabled features
     */
    getEnabledFeatures() {
        const enabled = {};
        for (const [featureId, config] of this.features) {
            if (config.enabled) {
                enabled[featureId] = config;
            }
        }
        return enabled;
    }

    /**
     * Get features by category
     */
    getFeaturesByCategory(category) {
        const categoryFeatures = {};

        for (const [featureId, config] of this.features) {
            if (featureId.startsWith(category + '.')) {
                categoryFeatures[featureId] = config;
            }
        }

        return categoryFeatures;
    }

    /**
     * Get current license and feature summary
     */
    getFeatureSummary() {
        const summary = {
            licenseLevel: this.licenseLevel,
            totalFeatures: this.features.size,
            enabledFeatures: Array.from(this.features.values()).filter(f => f.enabled).length,
            categories: {
                core: Object.keys(this.getFeaturesByCategory('core')).length,
                professional: Object.keys(this.getFeaturesByCategory('pro')).length,
                enterprise: Object.keys(this.getFeaturesByCategory('ent')).length,
                admin: Object.keys(this.getFeaturesByCategory('admin')).length,
                dynamic: Object.keys(this.getFeaturesByCategory('dynamic')).length
            },
            instances: {
                local: this.localDetectedInstances.length,
                network: this.networkDetectedInstances.length
            },
            lastSync: this.lastSync
        };

        return summary;
    }

    /**
     * Cleanup and shutdown
     */
    shutdown() {
        if (this.autoSyncInterval) {
            clearInterval(this.autoSyncInterval);
        }

        this.saveFeaturesToDisk();
        console.log('🎛️ FeatureManagementService shutdown');
    }
}

module.exports = FeatureManagementService;