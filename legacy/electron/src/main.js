/**
 * 📱 FlexPhone - Lightweight SIP Client
 * Main electron process for FlexPhone SIP client
 */

const { app, BrowserWindow, ipcMain, Menu, dialog, shell } = require('electron');
const path = require('path');
const fs = require('fs');
const keytar = require('keytar');

// Enable audio engine and disable restrictions
app.commandLine.appendSwitch('enable-web-audio');
app.commandLine.appendSwitch('autoplay-policy', 'no-user-gesture-required');
app.commandLine.appendSwitch('disable-features', 'MediaSessionService');
app.commandLine.appendSwitch('enable-features', 'AudioWorklet');
app.commandLine.appendSwitch('disable-background-throttling');
app.commandLine.appendSwitch('disable-renderer-backgrounding');
app.commandLine.appendSwitch('disable-backgrounding-occluded-windows');
console.log('🔊 Audio engine flags enabled for FlexPhone');

// Simple single instance handling (prevents multiple windows)

// Import FlexPhone services
const SIPService = require('./services/SIPService');
const ContactsService = require('./services/ContactsService');
const CallHistoryService = require('./services/CallHistoryService');
const SMSService = require('./services/SMSService');
const SettingsService = require('./services/SettingsService');
const RingtoneService = require('./services/RingtoneService');
const FeatureManagementService = require('./services/FeatureManagementService');

class FlexPhoneMain {
    constructor() {
        this.mainWindow = null;
        this.isReady = false;

        // Initialize services
        this.sipService = new SIPService();
        this.contactsService = new ContactsService();
        this.callHistoryService = new CallHistoryService();
        this.smsService = new SMSService();
        this.settingsService = new SettingsService();
        this.ringtoneService = new RingtoneService();
        this.featureManager = new FeatureManagementService();

        console.log('📱 FlexPhone v1.0.0 - Lightweight SIP Client');
    }

    async initialize() {
        try {
            // Initialize all services
            await this.sipService.initialize();
            await this.contactsService.initialize();
            await this.callHistoryService.initialize();
            await this.smsService.initialize();
            await this.settingsService.initialize();
            await this.featureManager.initialize();

            // Setup IPC handlers
            this.setupIPCHandlers();

            console.log('✅ FlexPhone services initialized');
            this.isReady = true;

        } catch (error) {
            console.error('❌ FlexPhone initialization failed:', error);
        }
    }

    createMainWindow() {
        // Prevent creating multiple windows
        if (this.mainWindow && !this.mainWindow.isDestroyed()) {
            console.log('📱 FlexPhone: Main window already exists, showing it');
            this.mainWindow.show();
            this.mainWindow.focus();
            return this.mainWindow;
        }

        console.log('📱 FlexPhone: Creating new main window');
        this.mainWindow = new BrowserWindow({
            width: 400,
            height: 700,
            minWidth: 350,
            minHeight: 600,
            webPreferences: {
                javascript: true,          // Explicitly enable JavaScript
                nodeIntegration: true,     // Enable Node.js integration
                contextIsolation: false,   // Allow frontend JS execution
                webSecurity: false,        // Allow local file access
                enableRemoteModule: false,
                allowRunningInsecureContent: true,
                experimentalFeatures: true,
                // Audio permissions for modern Electron
                backgroundThrottling: false,
                autoplayPolicy: 'no-user-gesture-required'
                // Removed preload to avoid conflicts
            },
            icon: path.join(__dirname, '../assets/icon.png'),
            title: 'FlexPhone',
            titleBarStyle: 'default',
            resizable: true,
            show: true, // Show immediately for debugging
            backgroundColor: '#1e1e1e',
            alwaysOnTop: false,
            center: true,
            frame: true,
            fullscreenable: true,
            maximizable: true,
            minimizable: true,
            skipTaskbar: false
        });

        // Load the app
        if (process.env.NODE_ENV === 'development') {
            const htmlPath = path.join(__dirname, '../public/index.html');
            if (!fs.existsSync(htmlPath)) {
                console.error('❌ HTML file not found:', htmlPath);
                // Fallback to a simple HTML string
                this.mainWindow.loadURL(`data:text/html;charset=utf-8,${encodeURIComponent(`
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>FlexPhone</title>
                        <style>
                            body { background: #1e1e1e; color: white; font-family: system-ui;
                                   display: flex; align-items: center; justify-content: center; height: 100vh; }
                            h1 { margin: 0; }
                        </style>
                    </head>
                    <body>
                        <div style="text-align: center;">
                            <h1>📱 FlexPhone</h1>
                            <p>Loading interface...</p>
                        </div>
                    </body>
                    </html>
                `)}`);
            } else {
                // Use file:// URL to ensure proper resource loading
                const fileUrl = `file://${htmlPath}`;
                this.mainWindow.loadURL(fileUrl);
            }
            this.mainWindow.webContents.openDevTools();
        } else {
            const htmlPath = path.join(__dirname, '../public/index.html');
            if (!fs.existsSync(htmlPath)) {
                console.error('❌ HTML file not found:', htmlPath);
                // Fallback to a simple HTML string
                this.mainWindow.loadURL(`data:text/html;charset=utf-8,${encodeURIComponent(`
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>FlexPhone</title>
                        <style>
                            body { background: #1e1e1e; color: white; font-family: system-ui;
                                   display: flex; align-items: center; justify-content: center; height: 100vh; }
                            h1 { margin: 0; }
                        </style>
                    </head>
                    <body>
                        <div style="text-align: center;">
                            <h1>📱 FlexPhone</h1>
                            <p>Loading interface...</p>
                        </div>
                    </body>
                    </html>
                `)}`);
            } else {
                // Use file:// URL to ensure proper resource loading
                const fileUrl = `file://${htmlPath}`;
                this.mainWindow.loadURL(fileUrl);
            }
        }

        // Show window when ready
        this.mainWindow.once('ready-to-show', () => {
            console.log('📱 FlexPhone: Main window ready, showing...');
            if (this.mainWindow && !this.mainWindow.isDestroyed()) {
                this.mainWindow.show();
                this.mainWindow.focus();
                this.mainWindow.moveTop();

                // Focus on startup
                if (process.platform === 'darwin') {
                    app.dock.show();
                    app.dock.bounce('critical');
                }
            }
        });

        // Additional debugging for content loading
        this.mainWindow.webContents.once('dom-ready', () => {
            console.log('📱 FlexPhone: DOM ready');
        });

        this.mainWindow.webContents.once('did-finish-load', () => {
            console.log('📱 FlexPhone: Content finished loading');
            // Ensure window is shown after content loads
            if (this.mainWindow && !this.mainWindow.isDestroyed() && !this.mainWindow.isVisible()) {
                console.log('📱 FlexPhone: Showing window after content load');
                this.mainWindow.show();
                this.mainWindow.focus();
            }
        });

        // Add error handling for load failures
        this.mainWindow.webContents.on('did-fail-load', (event, errorCode, errorDescription) => {
            console.error('📱 FlexPhone: Failed to load window:', errorCode, errorDescription);
            // Show window anyway with fallback content
            if (this.mainWindow && !this.mainWindow.isDestroyed()) {
                this.mainWindow.show();
            }
        });

        // Force show after timeout as fallback
        setTimeout(() => {
            if (this.mainWindow && !this.mainWindow.isDestroyed() && !this.mainWindow.isVisible()) {
                console.log('📱 FlexPhone: Force showing window after timeout');
                this.mainWindow.show();
                this.mainWindow.focus();
                this.mainWindow.moveTop();
                if (process.platform === 'darwin') {
                    this.mainWindow.setVisibleOnAllWorkspaces(true, { visibleOnFullScreen: true });
                    this.mainWindow.setVisibleOnAllWorkspaces(false, { visibleOnFullScreen: false });
                }
            }
        }, 2000); // Increased timeout to 2 seconds

        // Handle window closed
        this.mainWindow.on('closed', () => {
            this.mainWindow = null;
        });

        // Handle window minimize (keep SIP connection alive)
        this.mainWindow.on('minimize', () => {
            console.log('📱 FlexPhone minimized - SIP connection maintained');
        });

        return this.mainWindow;
    }

    setupIPCHandlers() {
        // SIP Service handlers
        ipcMain.handle('sip-connect', async (event, config) => {
            return await this.sipService.connect(config);
        });

        ipcMain.handle('sip-disconnect', async () => {
            return await this.sipService.disconnect();
        });

        ipcMain.handle('sip-make-call', async (event, number) => {
            return await this.sipService.makeCall(number);
        });

        ipcMain.handle('sip-answer-call', async (event, callId) => {
            return await this.sipService.answerCall(callId);
        });

        ipcMain.handle('sip-hangup-call', async (event, callId) => {
            return await this.sipService.hangupCall(callId);
        });

        ipcMain.handle('sip-send-dtmf', async (event, digits) => {
            return await this.sipService.sendDTMF(digits);
        });

        ipcMain.handle('sip-get-status', async () => {
            return this.sipService.getStatus();
        });

        // SMS Service handlers
        ipcMain.handle('sms-send', async (event, to, message) => {
            return await this.smsService.sendSMS(to, message);
        });

        ipcMain.handle('sms-get-conversations', async () => {
            return await this.smsService.getConversations();
        });

        ipcMain.handle('sms-get-messages', async (event, conversationId) => {
            return await this.smsService.getMessages(conversationId);
        });

        ipcMain.handle('sms-mark-read', async (event, messageId) => {
            return await this.smsService.markAsRead(messageId);
        });

        // Contacts Service handlers
        ipcMain.handle('contacts-get-all', async () => {
            return await this.contactsService.getAllContacts();
        });

        ipcMain.handle('contacts-add', async (event, contact) => {
            return await this.contactsService.addContact(contact);
        });

        ipcMain.handle('contacts-update', async (event, contactId, updates) => {
            return await this.contactsService.updateContact(contactId, updates);
        });

        ipcMain.handle('contacts-delete', async (event, contactId) => {
            return await this.contactsService.deleteContact(contactId);
        });

        ipcMain.handle('contacts-search', async (event, query) => {
            return await this.contactsService.searchContacts(query);
        });

        // Call History handlers
        ipcMain.handle('history-get-calls', async (event, limit = 50) => {
            return await this.callHistoryService.getCallHistory(limit);
        });

        ipcMain.handle('history-clear', async () => {
            return await this.callHistoryService.clearHistory();
        });

        // Settings handlers
        ipcMain.handle('settings-get', async (event, key) => {
            return await this.settingsService.get(key);
        });

        ipcMain.handle('settings-set', async (event, key, value) => {
            return await this.settingsService.set(key, value);
        });

        ipcMain.handle('settings-get-all', async () => {
            return await this.settingsService.getAll();
        });

        // App handlers
        ipcMain.handle('app-get-version', () => {
            return app.getVersion();
        });

        ipcMain.handle('app-minimize', () => {
            if (this.mainWindow) {
                this.mainWindow.minimize();
            }
        });

        ipcMain.handle('app-close', () => {
            app.quit();
        });

        ipcMain.handle('app-open-external', (event, url) => {
            shell.openExternal(url);
        });

        // File handlers
        ipcMain.handle('file-select', async (event, options) => {
            const result = await dialog.showOpenDialog(this.mainWindow, options);
            return result;
        });

        ipcMain.handle('file-save', async (event, options) => {
            const result = await dialog.showSaveDialog(this.mainWindow, options);
            return result;
        });

        // Recording handlers
        ipcMain.handle('recording-start', async (event, config) => {
            return await this.startRecording(config);
        });

        ipcMain.handle('recording-stop', async () => {
            return await this.stopRecording();
        });

        ipcMain.handle('recording-pause', async () => {
            return await this.pauseRecording();
        });

        ipcMain.handle('recording-resume', async () => {
            return await this.resumeRecording();
        });

        ipcMain.handle('recording-get-list', async () => {
            return await this.getRecordingsList();
        });

        ipcMain.handle('recording-play', async (event, filePath) => {
            return await this.playRecording(filePath);
        });

        ipcMain.handle('recording-delete', async (event, filePath) => {
            return await this.deleteRecording(filePath);
        });

        ipcMain.handle('recording-browse-storage', async () => {
            const result = await dialog.showOpenDialog(this.mainWindow, {
                properties: ['openDirectory', 'createDirectory'],
                title: 'Select Recording Storage Directory',
                buttonLabel: 'Select Folder'
            });
            if (!result.canceled && result.filePaths.length > 0) {
                return result.filePaths[0];
            }
            return null;
        });

        // Keychain handlers for iCloud Keychain integration
        ipcMain.handle('keychain-get-password', async (event, service, account) => {
            try {
                const password = await keytar.getPassword(service, account);
                return { success: true, password };
            } catch (error) {
                console.error('❌ Keychain get password failed:', error);
                return { success: false, error: error.message };
            }
        });

        ipcMain.handle('keychain-set-password', async (event, service, account, password) => {
            try {
                await keytar.setPassword(service, account, password);
                console.log(`🔐 Password saved to keychain for ${service}:${account}`);
                return { success: true };
            } catch (error) {
                console.error('❌ Keychain set password failed:', error);
                return { success: false, error: error.message };
            }
        });

        ipcMain.handle('keychain-delete-password', async (event, service, account) => {
            try {
                const deleted = await keytar.deletePassword(service, account);
                if (deleted) {
                    console.log(`🗑️ Password deleted from keychain for ${service}:${account}`);
                    return { success: true };
                } else {
                    return { success: false, error: 'Password not found' };
                }
            } catch (error) {
                console.error('❌ Keychain delete password failed:', error);
                return { success: false, error: error.message };
            }
        });

        ipcMain.handle('keychain-find-credentials', async (event, service) => {
            try {
                const credentials = await keytar.findCredentials(service);
                return { success: true, credentials };
            } catch (error) {
                console.error('❌ Keychain find credentials failed:', error);
                return { success: false, error: error.message };
            }
        });

        // Apple Pay and Payment handlers for app purchases
        ipcMain.handle('payment-check-apple-pay', async (event) => {
            try {
                // Check if Apple Pay is available (macOS only)
                if (process.platform === 'darwin') {
                    // Use Apple's StoreKit framework for in-app purchases
                    return { success: true, available: true, methods: ['apple-pay', 'credit-card'] };
                } else {
                    return { success: true, available: false, methods: ['credit-card', 'paypal'] };
                }
            } catch (error) {
                console.error('❌ Payment check failed:', error);
                return { success: false, error: error.message };
            }
        });

        ipcMain.handle('payment-process-purchase', async (event, purchaseData) => {
            try {
                const { productId, paymentMethod, amount } = purchaseData;
                console.log(`💳 Processing purchase: ${productId} via ${paymentMethod} for $${amount}`);

                // Simulate payment processing (replace with actual payment gateway)
                if (paymentMethod === 'apple-pay' && process.platform === 'darwin') {
                    // Apple Pay processing
                    console.log('🍎 Processing Apple Pay payment...');

                    // In production, integrate with Apple's StoreKit
                    return {
                        success: true,
                        transactionId: `ap_${Date.now()}`,
                        receiptData: `receipt_${productId}_${Date.now()}`,
                        method: 'apple-pay'
                    };
                } else {
                    // Credit card or other payment methods
                    console.log(`💳 Processing ${paymentMethod} payment...`);

                    return {
                        success: true,
                        transactionId: `cc_${Date.now()}`,
                        receiptData: `receipt_${productId}_${Date.now()}`,
                        method: paymentMethod
                    };
                }
            } catch (error) {
                console.error('❌ Payment processing failed:', error);
                return { success: false, error: error.message };
            }
        });

        ipcMain.handle('payment-verify-receipt', async (event, receiptData) => {
            try {
                console.log('🧾 Verifying payment receipt...');

                // In production, verify receipt with payment provider
                // For Apple Pay, use Apple's receipt validation
                // For other methods, use respective payment gateway APIs

                return {
                    success: true,
                    valid: true,
                    productIds: ['flexphone-premium', 'flexphone-pro'],
                    expirationDate: new Date(Date.now() + 365 * 24 * 60 * 60 * 1000) // 1 year
                };
            } catch (error) {
                console.error('❌ Receipt verification failed:', error);
                return { success: false, error: error.message };
            }
        });

        ipcMain.handle('payment-get-products', async (event) => {
            try {
                // Return available in-app purchase products
                const products = [
                    {
                        id: 'flexphone-premium',
                        name: 'FlexPhone Premium',
                        description: 'Advanced SIP features, call recording, and premium support',
                        price: 9.99,
                        currency: 'USD',
                        type: 'subscription',
                        duration: 'monthly'
                    },
                    {
                        id: 'flexphone-pro',
                        name: 'FlexPhone Pro',
                        description: 'All premium features plus enterprise integrations',
                        price: 19.99,
                        currency: 'USD',
                        type: 'subscription',
                        duration: 'monthly'
                    },
                    {
                        id: 'flexphone-lifetime',
                        name: 'FlexPhone Lifetime',
                        description: 'Lifetime access to all features',
                        price: 99.99,
                        currency: 'USD',
                        type: 'non-consumable'
                    }
                ];

                return { success: true, products };
            } catch (error) {
                console.error('❌ Failed to get products:', error);
                return { success: false, error: error.message };
            }
        });

        // Passkey (WebAuthn) support for passwordless authentication
        ipcMain.handle('passkey-check-support', async (event) => {
            try {
                // Check if WebAuthn/Passkey is supported
                return {
                    success: true,
                    supported: true, // Electron supports WebAuthn
                    authenticatorTypes: ['platform', 'cross-platform'],
                    features: ['touchid', 'faceid', 'yubikey', 'fingerprint']
                };
            } catch (error) {
                console.error('❌ Passkey support check failed:', error);
                return { success: false, error: error.message };
            }
        });

        ipcMain.handle('passkey-register', async (event, options) => {
            try {
                const { username, displayName, service } = options;
                console.log(`🔐 Registering passkey for ${username} on ${service}`);

                // In production, this would integrate with WebAuthn API
                // For now, simulate passkey registration
                const passkey = {
                    id: `pk_${Date.now()}_${Math.random().toString(36).substring(7)}`,
                    service: service,
                    username: username,
                    displayName: displayName,
                    credentialId: btoa(`${service}:${username}:${Date.now()}`),
                    publicKey: `pk_${Date.now()}`,
                    counter: 0,
                    created: new Date().toISOString()
                };

                // Store in keychain as passkey data
                await keytar.setPassword(`FlexPhone-Passkey-${service}`, username, JSON.stringify(passkey));

                console.log(`✅ Passkey registered for ${username}`);
                return { success: true, passkeyId: passkey.id, credentialId: passkey.credentialId };
            } catch (error) {
                console.error('❌ Passkey registration failed:', error);
                return { success: false, error: error.message };
            }
        });

        ipcMain.handle('passkey-authenticate', async (event, options) => {
            try {
                const { service, username, challenge } = options;
                console.log(`🔐 Authenticating with passkey for ${username} on ${service}`);

                // Retrieve passkey from keychain
                const passkeyData = await keytar.getPassword(`FlexPhone-Passkey-${service}`, username);

                if (!passkeyData) {
                    return { success: false, error: 'Passkey not found' };
                }

                const passkey = JSON.parse(passkeyData);

                // In production, this would verify the WebAuthn assertion
                // For now, simulate successful authentication
                const authResult = {
                    credentialId: passkey.credentialId,
                    authenticatorData: btoa(`auth_${Date.now()}`),
                    signature: btoa(`sig_${challenge}_${passkey.id}`),
                    userHandle: btoa(username)
                };

                console.log(`✅ Passkey authentication successful for ${username}`);
                return { success: true, authResult, userInfo: { username, displayName: passkey.displayName } };
            } catch (error) {
                console.error('❌ Passkey authentication failed:', error);
                return { success: false, error: error.message };
            }
        });

        ipcMain.handle('passkey-list', async (event, service) => {
            try {
                console.log(`🔍 Listing passkeys for service: ${service}`);

                // Find all passkeys for the service
                const credentials = await keytar.findCredentials(`FlexPhone-Passkey-${service}`);

                const passkeys = credentials.map(cred => {
                    try {
                        const passkey = JSON.parse(cred.password);
                        return {
                            id: passkey.id,
                            username: cred.account,
                            displayName: passkey.displayName,
                            created: passkey.created,
                            service: service
                        };
                    } catch (error) {
                        console.error('Failed to parse passkey data:', error);
                        return null;
                    }
                }).filter(Boolean);

                return { success: true, passkeys };
            } catch (error) {
                console.error('❌ Failed to list passkeys:', error);
                return { success: false, error: error.message };
            }
        });

        ipcMain.handle('passkey-delete', async (event, service, username) => {
            try {
                console.log(`🗑️ Deleting passkey for ${username} on ${service}`);

                const deleted = await keytar.deletePassword(`FlexPhone-Passkey-${service}`, username);

                if (deleted) {
                    console.log(`✅ Passkey deleted for ${username}`);
                    return { success: true };
                } else {
                    return { success: false, error: 'Passkey not found' };
                }
            } catch (error) {
                console.error('❌ Passkey deletion failed:', error);
                return { success: false, error: error.message };
            }
        });

        console.log('📡 IPC handlers registered (including keychain, payment, and passkey support)');
    }

    createMenu() {
        const template = [
            {
                label: 'FlexPhone',
                submenu: [
                    {
                        label: 'About FlexPhone',
                        click: () => {
                            dialog.showMessageBox(this.mainWindow, {
                                type: 'info',
                                title: 'About FlexPhone',
                                message: 'FlexPhone v1.0.0',
                                detail: 'Lightweight SIP Client for iOS and Desktop\nBuilt with Electron and React\n\n© 2024 FlexPBX Team'
                            });
                        }
                    },
                    { type: 'separator' },
                    {
                        label: 'Preferences...',
                        accelerator: 'CmdOrCtrl+,',
                        click: () => {
                            this.mainWindow.webContents.send('show-settings');
                        }
                    },
                    { type: 'separator' },
                    { role: 'hide' },
                    { role: 'hideothers' },
                    { role: 'unhide' },
                    { type: 'separator' },
                    { role: 'quit' }
                ]
            },
            {
                label: 'Call',
                submenu: [
                    {
                        label: 'New Call...',
                        accelerator: 'CmdOrCtrl+N',
                        click: () => {
                            this.mainWindow.webContents.send('show-dialer');
                        }
                    },
                    {
                        label: 'Answer Call',
                        accelerator: 'CmdOrCtrl+A',
                        click: () => {
                            this.mainWindow.webContents.send('answer-call');
                        }
                    },
                    {
                        label: 'Hang Up',
                        accelerator: 'CmdOrCtrl+H',
                        click: () => {
                            this.mainWindow.webContents.send('hangup-call');
                        }
                    },
                    { type: 'separator' },
                    {
                        label: 'Call History',
                        accelerator: 'CmdOrCtrl+R',
                        click: () => {
                            this.mainWindow.webContents.send('show-history');
                        }
                    }
                ]
            },
            {
                label: 'Messages',
                submenu: [
                    {
                        label: 'New Message...',
                        accelerator: 'CmdOrCtrl+M',
                        click: () => {
                            this.mainWindow.webContents.send('show-sms');
                        }
                    },
                    {
                        label: 'All Conversations',
                        accelerator: 'CmdOrCtrl+Shift+M',
                        click: () => {
                            this.mainWindow.webContents.send('show-conversations');
                        }
                    }
                ]
            },
            {
                label: 'Contacts',
                submenu: [
                    {
                        label: 'All Contacts',
                        accelerator: 'CmdOrCtrl+B',
                        click: () => {
                            this.mainWindow.webContents.send('show-contacts');
                        }
                    },
                    {
                        label: 'Add Contact...',
                        accelerator: 'CmdOrCtrl+Shift+N',
                        click: () => {
                            this.mainWindow.webContents.send('add-contact');
                        }
                    }
                ]
            },
            {
                label: 'View',
                submenu: [
                    { role: 'reload' },
                    { role: 'forceReload' },
                    { role: 'toggleDevTools' },
                    { type: 'separator' },
                    { role: 'resetZoom' },
                    { role: 'zoomIn' },
                    { role: 'zoomOut' },
                    { type: 'separator' },
                    { role: 'togglefullscreen' }
                ]
            },
            {
                label: 'Window',
                submenu: [
                    { role: 'minimize' },
                    { role: 'close' }
                ]
            },
            {
                label: 'Help',
                submenu: [
                    {
                        label: 'FlexPhone Documentation',
                        click: () => {
                            shell.openExternal('https://docs.flexpbx.com/flexphone');
                        }
                    },
                    {
                        label: 'Report Issue',
                        click: () => {
                            shell.openExternal('https://github.com/flexpbx/flexphone/issues');
                        }
                    },
                    { type: 'separator' },
                    {
                        label: 'Check for Updates...',
                        click: () => {
                            this.mainWindow.webContents.send('check-updates');
                        }
                    }
                ]
            }
        ];

        // macOS specific menu adjustments
        if (process.platform === 'darwin') {
            template[0].label = app.getName();
        } else {
            // Remove macOS specific items for other platforms
            template[0].submenu = template[0].submenu.filter(item =>
                !['hide', 'hideothers', 'unhide'].includes(item.role)
            );
        }

        const menu = Menu.buildFromTemplate(template);
        Menu.setApplicationMenu(menu);
    }

    // Event forwarding from services to renderer
    setupServiceEventForwarding() {
        // SIP events
        this.sipService.on('connected', (data) => {
            this.mainWindow?.webContents.send('sip-connected', data);
        });

        this.sipService.on('disconnected', (data) => {
            this.mainWindow?.webContents.send('sip-disconnected', data);
        });

        this.sipService.on('incoming-call', (data) => {
            this.mainWindow?.webContents.send('incoming-call', data);
        });

        this.sipService.on('call-answered', (data) => {
            this.mainWindow?.webContents.send('call-answered', data);
        });

        this.sipService.on('call-ended', (data) => {
            this.mainWindow?.webContents.send('call-ended', data);
        });

        // SMS events
        this.smsService.on('message-received', (data) => {
            this.mainWindow?.webContents.send('sms-received', data);
        });

        this.smsService.on('message-sent', (data) => {
            this.mainWindow?.webContents.send('sms-sent', data);
        });

        console.log('📡 Service event forwarding setup');
    }

    // Recording methods
    async startRecording(config) {
        try {
            // Use user-configurable path or default to ~/FlexPhone Recordings
            let recordingPath = config.recordingPath || '~/FlexPhone Recordings';

            // Expand tilde to home directory using helper method
            const defaultPath = this.expandUserPath(recordingPath);

            // Ensure recording directory exists
            if (!fs.existsSync(defaultPath)) {
                fs.mkdirSync(defaultPath, { recursive: true });
            }

            const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
            const filename = `FlexPhone_Recording_${timestamp}`;
            const extension = config.quality === 'high' ? 'wav' : 'mp3';
            const filePath = path.join(config.storagePath || defaultPath, `${filename}.${extension}`);

            // Store recording info
            this.currentRecording = {
                filePath,
                startTime: new Date(),
                mode: config.mode,
                quality: config.quality,
                isRecording: true,
                isPaused: false
            };

            console.log(`🎙️ Started recording: ${filePath}`);

            // If remote recording is enabled, notify FlexPBX
            if (config.mode === 'remote' || config.mode === 'both') {
                // TODO: Implement remote recording via FlexPBX API
                console.log('📡 Remote recording mode enabled');
            }

            return { success: true, filePath, recording: this.currentRecording };
        } catch (error) {
            console.error('❌ Recording start failed:', error);
            return { success: false, error: error.message };
        }
    }

    async stopRecording() {
        try {
            if (!this.currentRecording) {
                return { success: false, error: 'No active recording' };
            }

            const duration = new Date() - this.currentRecording.startTime;
            this.currentRecording.isRecording = false;
            this.currentRecording.endTime = new Date();
            this.currentRecording.duration = Math.floor(duration / 1000); // seconds

            console.log(`🎙️ Stopped recording: ${this.currentRecording.filePath}`);

            const recording = { ...this.currentRecording };
            this.currentRecording = null;

            return { success: true, recording };
        } catch (error) {
            console.error('❌ Recording stop failed:', error);
            return { success: false, error: error.message };
        }
    }

    async pauseRecording() {
        try {
            if (!this.currentRecording || !this.currentRecording.isRecording) {
                return { success: false, error: 'No active recording to pause' };
            }

            this.currentRecording.isPaused = true;
            console.log('⏸️ Recording paused');

            return { success: true, recording: this.currentRecording };
        } catch (error) {
            console.error('❌ Recording pause failed:', error);
            return { success: false, error: error.message };
        }
    }

    async resumeRecording() {
        try {
            if (!this.currentRecording || !this.currentRecording.isPaused) {
                return { success: false, error: 'No paused recording to resume' };
            }

            this.currentRecording.isPaused = false;
            console.log('▶️ Recording resumed');

            return { success: true, recording: this.currentRecording };
        } catch (error) {
            console.error('❌ Recording resume failed:', error);
            return { success: false, error: error.message };
        }
    }

    // Helper method to expand user paths
    expandUserPath(userPath) {
        const os = require('os');
        if (userPath.startsWith('~/')) {
            return path.join(os.homedir(), userPath.substring(2));
        } else if (userPath.startsWith('~')) {
            return path.join(os.homedir(), userPath.substring(1));
        }
        return userPath;
    }

    async getRecordingsList(customPath = null) {
        try {
            // Use custom path or get from settings, default to ~/FlexPhone Recordings
            let recordingPath = customPath || await this.settingsService?.get('recordingPath') || '~/FlexPhone Recordings';
            const defaultPath = this.expandUserPath(recordingPath);

            if (!fs.existsSync(defaultPath)) {
                return { success: true, recordings: [], path: recordingPath };
            }

            const files = fs.readdirSync(defaultPath)
                .filter(file => file.endsWith('.wav') || file.endsWith('.mp3'))
                .map(file => {
                    const filePath = path.join(defaultPath, file);
                    const stats = fs.statSync(filePath);
                    return {
                        name: file,
                        filePath,
                        size: stats.size,
                        created: stats.birthtime,
                        modified: stats.mtime
                    };
                })
                .sort((a, b) => b.created - a.created); // Latest first

            return { success: true, recordings: files };
        } catch (error) {
            console.error('❌ Get recordings list failed:', error);
            return { success: false, error: error.message };
        }
    }

    async playRecording(filePath) {
        try {
            shell.openExternal(filePath);
            return { success: true };
        } catch (error) {
            console.error('❌ Play recording failed:', error);
            return { success: false, error: error.message };
        }
    }

    async deleteRecording(filePath) {
        try {
            if (fs.existsSync(filePath)) {
                fs.unlinkSync(filePath);
                console.log(`🗑️ Deleted recording: ${filePath}`);
                return { success: true };
            } else {
                return { success: false, error: 'File not found' };
            }
        } catch (error) {
            console.error('❌ Delete recording failed:', error);
            return { success: false, error: error.message };
        }
    }
}

// App event handlers
const flexPhone = new FlexPhoneMain();

// Handle second instance - focus existing window
app.on('second-instance', () => {
    // Someone tried to run a second instance, focus our window instead
    if (flexPhone.mainWindow && !flexPhone.mainWindow.isDestroyed()) {
        if (flexPhone.mainWindow.isMinimized()) {
            flexPhone.mainWindow.restore();
        }
        flexPhone.mainWindow.show();
        flexPhone.mainWindow.focus();
    }
});

app.whenReady().then(async () => {
    console.log('📱 FlexPhone: App ready, initializing...');

    // Simple single instance check after app is ready
    const gotTheLock = app.requestSingleInstanceLock();
    if (!gotTheLock) {
        console.log('📱 FlexPhone: Another instance is already running');
        app.quit();
        return;
    }

    await flexPhone.initialize();
    flexPhone.createMainWindow();
    flexPhone.createMenu();
    flexPhone.setupServiceEventForwarding();

    console.log('📱 FlexPhone ready');
});

app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

app.on('activate', () => {
    // Only create window if none exist AND app is initialized
    if (BrowserWindow.getAllWindows().length === 0 && flexPhone.isReady) {
        console.log('📱 FlexPhone: Creating window on activate');
        flexPhone.createMainWindow();
    } else if (flexPhone.mainWindow) {
        // If window exists but is hidden, show it
        if (!flexPhone.mainWindow.isVisible()) {
            console.log('📱 FlexPhone: Showing existing window on activate');
            flexPhone.mainWindow.show();
            flexPhone.mainWindow.focus();
        }
    }
});

app.on('before-quit', async () => {
    console.log('📱 FlexPhone shutting down...');

    // Cleanup services
    if (flexPhone.sipService) {
        await flexPhone.sipService.disconnect();
    }
});

module.exports = FlexPhoneMain;