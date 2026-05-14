/**
 * 📱 FlexPhone - Lightweight SIP Client
 * Main electron process for FlexPhone SIP client
 */

const { app, BrowserWindow, ipcMain, Menu, dialog, shell } = require('electron');
const path = require('path');
const fs = require('fs');

// Import FlexPhone services
const SIPService = require('./services/SIPService');
const ContactsService = require('./services/ContactsService');
const CallHistoryService = require('./services/CallHistoryService');
const SMSService = require('./services/SMSService');
const SettingsService = require('./services/SettingsService');

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

            // Setup IPC handlers
            this.setupIPCHandlers();

            console.log('✅ FlexPhone services initialized');
            this.isReady = true;

        } catch (error) {
            console.error('❌ FlexPhone initialization failed:', error);
        }
    }

    createMainWindow() {
        this.mainWindow = new BrowserWindow({
            width: 400,
            height: 700,
            minWidth: 350,
            minHeight: 600,
            webPreferences: {
                nodeIntegration: false,
                contextIsolation: true,
                enableRemoteModule: false,
                preload: path.join(__dirname, 'preload.js')
            },
            icon: path.join(__dirname, '../assets/icon.png'),
            title: 'FlexPhone',
            titleBarStyle: 'default',
            resizable: true,
            show: false, // Show after load
            backgroundColor: '#1e1e1e'
        });

        // Load the app
        if (process.env.NODE_ENV === 'development') {
            this.mainWindow.loadFile(path.join(__dirname, '../public/index.html'));
            this.mainWindow.webContents.openDevTools();
        } else {
            this.mainWindow.loadFile(path.join(__dirname, '../public/index.html'));
        }

        // Show window when ready
        this.mainWindow.once('ready-to-show', () => {
            this.mainWindow.show();

            // Focus on startup
            if (process.platform === 'darwin') {
                app.dock.show();
            }
        });

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

        console.log('📡 IPC handlers registered');
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
}

// App event handlers
const flexPhone = new FlexPhoneMain();

app.whenReady().then(async () => {
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
    if (BrowserWindow.getAllWindows().length === 0) {
        flexPhone.createMainWindow();
    }
});

app.on('before-quit', async () => {
    console.log('📱 FlexPhone shutting down...');

    // Cleanup services
    if (flexPhone.sipService) {
        await flexPhone.sipService.disconnect();
    }
});

// Prevent multiple instances
const gotTheLock = app.requestSingleInstanceLock();

if (!gotTheLock) {
    app.quit();
} else {
    app.on('second-instance', () => {
        // Someone tried to run a second instance, focus our window instead
        if (flexPhone.mainWindow) {
            if (flexPhone.mainWindow.isMinimized()) {
                flexPhone.mainWindow.restore();
            }
            flexPhone.mainWindow.focus();
        }
    });
}

module.exports = FlexPhoneMain;