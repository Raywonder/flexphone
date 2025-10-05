/**
 * ⚙️ FlexPhone Settings Service
 * Manages app settings and preferences
 */

const EventEmitter = require('events');
const fs = require('fs').promises;
const path = require('path');

class SettingsService extends EventEmitter {
    constructor() {
        super();

        this.settings = new Map();
        this.settingsPath = path.join(process.cwd(), 'data', 'settings.json');

        // Default settings
        this.defaultSettings = {
            // SIP Configuration
            'sip.provider': 'FLEXPBX',
            'sip.server': 'flexpbx.local',
            'sip.port': 5070,
            'sip.username': '',
            'sip.password': '',
            'sip.displayName': '',
            'sip.transport': 'UDP',
            'sip.autoConnect': false,
            'sip.keepAlive': true,

            // Audio Settings
            'audio.inputDevice': 'default',
            'audio.outputDevice': 'default',
            'audio.ringtone': 'default',
            'audio.dtmfTone': true,
            'audio.microphoneVolume': 80,
            'audio.speakerVolume': 80,
            'audio.echoCancellation': true,
            'audio.noiseSuppression': true,
            'audio.autoGainControl': true,

            // UI Settings
            'ui.theme': 'dark',
            'ui.fontSize': 'medium',
            'ui.language': 'en',
            'ui.compactMode': false,
            'ui.showCallTimer': true,
            'ui.showCallQuality': true,
            'ui.minimizeToTray': true,
            'ui.startMinimized': false,

            // Notifications
            'notifications.enabled': true,
            'notifications.sound': true,
            'notifications.desktop': true,
            'notifications.incomingCalls': true,
            'notifications.missedCalls': true,
            'notifications.newMessages': true,

            // Privacy & Security
            'privacy.sharePresence': true,
            'privacy.shareTyping': true,
            'privacy.autoReadReceipts': true,
            'privacy.encryptMessages': false,
            'privacy.logCalls': true,
            'privacy.logMessages': true,

            // Advanced
            'advanced.debugMode': false,
            'advanced.maxCallHistory': 1000,
            'advanced.maxMessageHistory': 5000,
            'advanced.networkTimeout': 30000,
            'advanced.codecPreference': 'G722,PCMU,PCMA',
            'advanced.videoEnabled': false,

            // Accessibility
            'accessibility.highContrast': false,
            'accessibility.largeText': false,
            'accessibility.screenReader': false,
            'accessibility.keyboardNavigation': true,

            // App Settings
            'app.autoUpdate': true,
            'app.telemetry': true,
            'app.crashReports': true,
            'app.lastVersion': '1.0.0'
        };

        console.log('⚙️ FlexPhone Settings Service initialized');
    }

    async initialize() {
        try {
            await this.loadSettings();
            console.log('✅ Settings Service ready');
            return true;
        } catch (error) {
            console.error('❌ Settings Service initialization failed:', error);
            return false;
        }
    }

    async get(key) {
        try {
            if (this.settings.has(key)) {
                return this.settings.get(key);
            }

            if (this.defaultSettings.hasOwnProperty(key)) {
                return this.defaultSettings[key];
            }

            return null;

        } catch (error) {
            console.error(`❌ Get setting failed for key: ${key}`, error);
            return null;
        }
    }

    async set(key, value) {
        try {
            const oldValue = this.settings.get(key);
            this.settings.set(key, value);

            await this.saveSettings();

            console.log(`⚙️ Setting updated: ${key} = ${value}`);

            this.emit('setting-changed', {
                key,
                value,
                oldValue
            });

            return {
                success: true,
                key,
                value
            };

        } catch (error) {
            console.error(`❌ Set setting failed for key: ${key}`, error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async getAll() {
        try {
            const allSettings = {};

            // Add default settings
            Object.keys(this.defaultSettings).forEach(key => {
                allSettings[key] = this.defaultSettings[key];
            });

            // Override with user settings
            this.settings.forEach((value, key) => {
                allSettings[key] = value;
            });

            return allSettings;

        } catch (error) {
            console.error('❌ Get all settings failed:', error);
            return {};
        }
    }

    async reset(key = null) {
        try {
            if (key) {
                // Reset specific setting
                if (this.defaultSettings.hasOwnProperty(key)) {
                    this.settings.set(key, this.defaultSettings[key]);
                    await this.saveSettings();

                    console.log(`⚙️ Setting reset: ${key}`);

                    this.emit('setting-reset', { key });

                    return {
                        success: true,
                        key
                    };
                } else {
                    throw new Error(`Unknown setting: ${key}`);
                }
            } else {
                // Reset all settings
                this.settings.clear();
                await this.saveSettings();

                console.log('⚙️ All settings reset to defaults');

                this.emit('settings-reset');

                return {
                    success: true,
                    message: 'All settings reset'
                };
            }

        } catch (error) {
            console.error('❌ Reset settings failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async export() {
        try {
            const allSettings = await this.getAll();
            const exportData = {
                settings: allSettings,
                exportDate: new Date().toISOString(),
                version: '1.0.0',
                app: 'FlexPhone'
            };

            const exportPath = path.join(process.cwd(), 'data', `flexphone-settings-${Date.now()}.json`);
            await fs.writeFile(exportPath, JSON.stringify(exportData, null, 2));

            console.log(`📤 Settings exported: ${exportPath}`);

            return {
                success: true,
                exportPath
            };

        } catch (error) {
            console.error('❌ Export settings failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async import(importPath) {
        try {
            const data = await fs.readFile(importPath, 'utf8');
            const importData = JSON.parse(data);

            if (!importData.settings) {
                throw new Error('Invalid settings file format');
            }

            // Import settings
            for (const [key, value] of Object.entries(importData.settings)) {
                if (this.defaultSettings.hasOwnProperty(key)) {
                    this.settings.set(key, value);
                }
            }

            await this.saveSettings();

            console.log(`📥 Settings imported: ${importPath}`);

            this.emit('settings-imported', { importPath });

            return {
                success: true,
                message: 'Settings imported successfully'
            };

        } catch (error) {
            console.error('❌ Import settings failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    getCategory(category) {
        const categorySettings = {};
        const allSettings = this.getAll();

        Object.keys(allSettings).forEach(key => {
            if (key.startsWith(category + '.')) {
                categorySettings[key] = allSettings[key];
            }
        });

        return categorySettings;
    }

    getSIPConfig() {
        return {
            provider: this.get('sip.provider'),
            server: this.get('sip.server'),
            port: this.get('sip.port'),
            username: this.get('sip.username'),
            password: this.get('sip.password'),
            displayName: this.get('sip.displayName'),
            transport: this.get('sip.transport'),
            autoConnect: this.get('sip.autoConnect'),
            keepAlive: this.get('sip.keepAlive')
        };
    }

    getAudioConfig() {
        return {
            inputDevice: this.get('audio.inputDevice'),
            outputDevice: this.get('audio.outputDevice'),
            ringtone: this.get('audio.ringtone'),
            dtmfTone: this.get('audio.dtmfTone'),
            microphoneVolume: this.get('audio.microphoneVolume'),
            speakerVolume: this.get('audio.speakerVolume'),
            echoCancellation: this.get('audio.echoCancellation'),
            noiseSuppression: this.get('audio.noiseSuppression'),
            autoGainControl: this.get('audio.autoGainControl')
        };
    }

    validateSetting(key, value) {
        // Validate setting values
        switch (key) {
            case 'sip.port':
                return value >= 1 && value <= 65535;
            case 'audio.microphoneVolume':
            case 'audio.speakerVolume':
                return value >= 0 && value <= 100;
            case 'advanced.maxCallHistory':
            case 'advanced.maxMessageHistory':
                return value > 0;
            case 'ui.theme':
                return ['light', 'dark', 'auto'].includes(value);
            case 'ui.fontSize':
                return ['small', 'medium', 'large'].includes(value);
            case 'ui.language':
                return ['en', 'es', 'fr', 'de', 'it', 'pt', 'ru', 'zh', 'ja'].includes(value);
            default:
                return true; // Allow other values
        }
    }

    async loadSettings() {
        try {
            const data = await fs.readFile(this.settingsPath, 'utf8');
            const savedSettings = JSON.parse(data);

            this.settings = new Map();
            Object.keys(savedSettings).forEach(key => {
                this.settings.set(key, savedSettings[key]);
            });

            console.log(`📥 Loaded ${this.settings.size} settings`);

        } catch (error) {
            this.settings = new Map();
            console.log('📝 Starting with default settings');
        }
    }

    async saveSettings() {
        try {
            const dataDir = path.dirname(this.settingsPath);

            try {
                await fs.access(dataDir);
            } catch {
                await fs.mkdir(dataDir, { recursive: true });
            }

            const settingsObject = {};
            this.settings.forEach((value, key) => {
                settingsObject[key] = value;
            });

            await fs.writeFile(this.settingsPath, JSON.stringify(settingsObject, null, 2));

        } catch (error) {
            console.error('❌ Failed to save settings:', error);
        }
    }
}

module.exports = SettingsService;