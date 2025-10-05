/**
 * 🔒 FlexPhone Preload Script
 * Secure bridge between main and renderer processes
 */

const { contextBridge, ipcRenderer } = require('electron');

// Expose protected methods that allow the renderer process to use
// the ipcRenderer without exposing the entire object
contextBridge.exposeInMainWorld('flexPhoneAPI', {
    // SIP API
    sip: {
        connect: (config) => ipcRenderer.invoke('sip-connect', config),
        disconnect: () => ipcRenderer.invoke('sip-disconnect'),
        makeCall: (number) => ipcRenderer.invoke('sip-make-call', number),
        answerCall: (callId) => ipcRenderer.invoke('sip-answer-call', callId),
        hangupCall: (callId) => ipcRenderer.invoke('sip-hangup-call', callId),
        sendDTMF: (digits) => ipcRenderer.invoke('sip-send-dtmf', digits),
        getStatus: () => ipcRenderer.invoke('sip-get-status')
    },

    // SMS API
    sms: {
        send: (to, message) => ipcRenderer.invoke('sms-send', to, message),
        getConversations: () => ipcRenderer.invoke('sms-get-conversations'),
        getMessages: (conversationId) => ipcRenderer.invoke('sms-get-messages', conversationId),
        markRead: (messageId) => ipcRenderer.invoke('sms-mark-read', messageId)
    },

    // Contacts API
    contacts: {
        getAll: () => ipcRenderer.invoke('contacts-get-all'),
        add: (contact) => ipcRenderer.invoke('contacts-add', contact),
        update: (contactId, updates) => ipcRenderer.invoke('contacts-update', contactId, updates),
        delete: (contactId) => ipcRenderer.invoke('contacts-delete', contactId),
        search: (query) => ipcRenderer.invoke('contacts-search', query)
    },

    // Call History API
    history: {
        getCalls: (limit) => ipcRenderer.invoke('history-get-calls', limit),
        clear: () => ipcRenderer.invoke('history-clear')
    },

    // Settings API
    settings: {
        get: (key) => ipcRenderer.invoke('settings-get', key),
        set: (key, value) => ipcRenderer.invoke('settings-set', key, value),
        getAll: () => ipcRenderer.invoke('settings-get-all')
    },

    // App API
    app: {
        getVersion: () => ipcRenderer.invoke('app-get-version'),
        minimize: () => ipcRenderer.invoke('app-minimize'),
        close: () => ipcRenderer.invoke('app-close'),
        openExternal: (url) => ipcRenderer.invoke('app-open-external', url)
    },

    // File API
    file: {
        select: (options) => ipcRenderer.invoke('file-select', options),
        save: (options) => ipcRenderer.invoke('file-save', options)
    },

    // Event listeners
    on: (channel, callback) => {
        const validChannels = [
            'sip-connected',
            'sip-disconnected',
            'incoming-call',
            'call-answered',
            'call-ended',
            'sms-received',
            'sms-sent',
            'show-settings',
            'show-dialer',
            'show-history',
            'show-sms',
            'show-conversations',
            'show-contacts',
            'add-contact',
            'answer-call',
            'hangup-call',
            'check-updates'
        ];

        if (validChannels.includes(channel)) {
            ipcRenderer.on(channel, callback);
        }
    },

    // Remove event listeners
    removeListener: (channel, callback) => {
        ipcRenderer.removeListener(channel, callback);
    },

    // Remove all listeners for a channel
    removeAllListeners: (channel) => {
        ipcRenderer.removeAllListeners(channel);
    }
});

console.log('🔒 FlexPhone preload script loaded');