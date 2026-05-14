/**
 * 📱 FlexPhone Frontend Application
 * Lightweight SIP phone interface
 */

class FlexPhoneApp {
    constructor() {
        this.currentNumber = '';
        this.isConnected = false;
        this.currentCall = null;
        this.activeTab = 'dialer';
        this.contacts = [];
        this.autoCompleteItems = [];
        this.selectedAutoCompleteIndex = -1;

        // Initialize DTMF service
        this.initializeDTMF();

        this.initializeElements();
        this.setupEventListeners();
        this.registerServiceWorker();
        this.setupIPCListeners();
        this.loadSettings();
        this.loadContacts();

        console.log('📱 FlexPhone App initialized');
    }

    initializeDTMF() {
        // DTMF frequency mappings
        this.dtmfFrequencies = {
            '1': [697, 1209], '2': [697, 1336], '3': [697, 1477],
            '4': [770, 1209], '5': [770, 1336], '6': [770, 1477],
            '7': [852, 1209], '8': [852, 1336], '9': [852, 1477],
            '*': [941, 1209], '0': [941, 1336], '#': [941, 1477]
        };

        this.dtmfEnabled = true;
        this.dtmfVolume = 0.1;
        this.dtmfDuration = 200;

        // Initialize AudioContext
        try {
            this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
            console.log('🎵 DTMF AudioContext initialized');
        } catch (error) {
            console.error('❌ Failed to initialize DTMF AudioContext:', error);
            this.dtmfEnabled = false;
        }
    }

    initializeElements() {
        // Dialer elements
        this.dialerDisplay = document.getElementById('dialerDisplay');
        this.dialerInput = document.getElementById('dialerInput');
        this.autoCompleteDropdown = document.getElementById('autoCompleteDropdown');
        this.dtmfStatus = document.getElementById('dtmfStatus');
        this.dtmfIndicator = document.getElementById('dtmfIndicator');
        this.dtmfEnabledText = document.getElementById('dtmfEnabled');
        this.callBtn = document.getElementById('callBtn');
        this.hangupBtn = document.getElementById('hangupBtn');
        this.clearBtn = document.getElementById('clearBtn');
        this.smsBtn = document.getElementById('smsBtn');
        this.testDTMFBtn = document.getElementById('testDTMFBtn');
        this.addContactBtn = document.getElementById('addContactBtn');

        // Settings elements
        this.sipProvider = document.getElementById('sipProvider');
        this.sipServer = document.getElementById('sipServer');
        this.sipPort = document.getElementById('sipPort');
        this.sipUsername = document.getElementById('sipUsername');
        this.sipPassword = document.getElementById('sipPassword');
        this.sipDisplayName = document.getElementById('sipDisplayName');
        this.connectBtn = document.getElementById('connectBtn');
        this.disconnectBtn = document.getElementById('disconnectBtn');

        // Status elements
        this.statusIndicator = document.getElementById('statusIndicator');
        this.connectionStatus = document.getElementById('connectionStatus');

        // Incoming call elements
        this.incomingCallOverlay = document.getElementById('incomingCallOverlay');
        this.callerName = document.getElementById('callerName');
        this.callerNumber = document.getElementById('callerNumber');
        this.answerBtn = document.getElementById('answerBtn');
        this.declineBtn = document.getElementById('declineBtn');

        // Tab elements
        this.navTabs = document.querySelectorAll('.nav-tab');
        this.tabContents = document.querySelectorAll('.tab-content');
    }

    setupEventListeners() {
        // Keypad
        document.querySelectorAll('.key').forEach(key => {
            key.addEventListener('click', () => {
                const digit = key.dataset.digit;
                this.addDigit(digit);
            });
        });

        // Call controls
        this.callBtn.addEventListener('click', () => this.makeCall());
        this.hangupBtn.addEventListener('click', () => this.hangupCall());
        this.clearBtn.addEventListener('click', () => this.clearDisplay());

        // Connection controls
        this.connectBtn.addEventListener('click', () => this.connectSIP());
        this.disconnectBtn.addEventListener('click', () => this.disconnectSIP());

        // Incoming call controls
        this.answerBtn.addEventListener('click', () => this.answerCall());
        this.declineBtn.addEventListener('click', () => this.declineCall());

        // Tab navigation
        this.navTabs.forEach(tab => {
            tab.addEventListener('click', () => {
                const tabName = tab.dataset.tab;
                this.switchTab(tabName);
            });
        });

        // Keyboard shortcuts
        document.addEventListener('keydown', (e) => {
            if (this.activeTab === 'dialer') {
                if (e.key >= '0' && e.key <= '9' || e.key === '*' || e.key === '#') {
                    this.addDigit(e.key);
                } else if (e.key === 'Backspace') {
                    this.removeDigit();
                } else if (e.key === 'Enter') {
                    this.makeCall();
                } else if (e.key === 'Escape') {
                    this.clearDisplay();
                }
            }
        });

        // Input field event listeners
        this.dialerInput.addEventListener('input', (e) => {
            this.currentNumber = e.target.value;
            this.updateDisplay();
            this.updateAutoComplete();
        });

        this.dialerInput.addEventListener('keydown', (e) => {
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                this.navigateAutoComplete(1);
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                this.navigateAutoComplete(-1);
            } else if (e.key === 'Enter') {
                e.preventDefault();
                if (this.selectedAutoCompleteIndex >= 0) {
                    this.selectAutoCompleteItem(this.selectedAutoCompleteIndex);
                } else {
                    this.makeCall();
                }
            } else if (e.key === 'Escape') {
                this.hideAutoComplete();
            }
        });

        // SMS button
        this.smsBtn.addEventListener('click', () => this.openSMS());

        // Test DTMF button
        this.testDTMFBtn.addEventListener('click', () => this.testAllDTMFTones());

        // Add Contact button
        this.addContactBtn.addEventListener('click', () => this.addContact());

        // DTMF status toggle
        this.dtmfStatus.addEventListener('click', () => this.toggleDTMF());

        // Click outside to hide auto-complete
        document.addEventListener('click', (e) => {
            if (!this.dialerInput.contains(e.target) && !this.autoCompleteDropdown.contains(e.target)) {
                this.hideAutoComplete();
            }
        });

        // Settings auto-save
        [this.sipProvider, this.sipServer, this.sipPort, this.sipUsername, this.sipPassword, this.sipDisplayName].forEach(element => {
            element.addEventListener('change', () => this.saveSettings());
        });
    }

    setupIPCListeners() {
        if (window.flexPhoneAPI) {
            // SIP events
            window.flexPhoneAPI.on('sip-connected', (event, data) => {
                this.onSIPConnected(data);
            });

            window.flexPhoneAPI.on('sip-disconnected', (event, data) => {
                this.onSIPDisconnected(data);
            });

            window.flexPhoneAPI.on('incoming-call', (event, call) => {
                this.onIncomingCall(call);
            });

            window.flexPhoneAPI.on('call-answered', (event, call) => {
                this.onCallAnswered(call);
            });

            window.flexPhoneAPI.on('call-ended', (event, call) => {
                this.onCallEnded(call);
            });

            // SMS events
            window.flexPhoneAPI.on('sms-received', (event, message) => {
                this.onSMSReceived(message);
            });

            // Menu events
            window.flexPhoneAPI.on('show-dialer', () => this.switchTab('dialer'));
            window.flexPhoneAPI.on('show-contacts', () => this.switchTab('contacts'));
            window.flexPhoneAPI.on('show-history', () => this.switchTab('history'));
            window.flexPhoneAPI.on('show-sms', () => this.switchTab('messages'));
            window.flexPhoneAPI.on('show-settings', () => this.switchTab('settings'));
        }
    }

    addDigit(digit) {
        this.currentNumber += digit;
        this.dialerInput.value = this.currentNumber;
        this.updateDisplay();

        // Play local DTMF tone for audible feedback
        this.playDTMFTone(digit);

        // Send DTMF if connected and on a call
        if (this.isConnected && this.currentCall) {
            window.flexPhoneAPI.sip.sendDTMF(digit);
        }

        // Update auto-complete
        this.updateAutoComplete();
    }

    async playDTMFTone(digit) {
        if (!this.dtmfEnabled || !this.audioContext) {
            return false;
        }

        const frequencies = this.dtmfFrequencies[digit.toString()];
        if (!frequencies) {
            console.log(`⚠️ Invalid DTMF digit: ${digit}`);
            return false;
        }

        try {
            // Resume AudioContext if suspended
            if (this.audioContext.state === 'suspended') {
                await this.audioContext.resume();
            }

            // Visual feedback
            this.dtmfStatus.classList.add('playing');
            setTimeout(() => {
                this.dtmfStatus.classList.remove('playing');
            }, this.dtmfDuration);

            // Create oscillators for the two DTMF frequencies
            const oscillator1 = this.audioContext.createOscillator();
            const oscillator2 = this.audioContext.createOscillator();
            const gainNode = this.audioContext.createGain();

            // Set frequencies
            oscillator1.frequency.setValueAtTime(frequencies[0], this.audioContext.currentTime);
            oscillator2.frequency.setValueAtTime(frequencies[1], this.audioContext.currentTime);

            // Set waveform
            oscillator1.type = 'sine';
            oscillator2.type = 'sine';

            // Connect oscillators to gain node
            oscillator1.connect(gainNode);
            oscillator2.connect(gainNode);

            // Set volume with envelope
            const fadeTime = 0.01;
            gainNode.gain.setValueAtTime(0, this.audioContext.currentTime);
            gainNode.gain.linearRampToValueAtTime(this.dtmfVolume, this.audioContext.currentTime + fadeTime);
            gainNode.gain.linearRampToValueAtTime(this.dtmfVolume, this.audioContext.currentTime + this.dtmfDuration / 1000 - fadeTime);
            gainNode.gain.linearRampToValueAtTime(0, this.audioContext.currentTime + this.dtmfDuration / 1000);

            // Connect to output
            gainNode.connect(this.audioContext.destination);

            // Start and stop oscillators
            const startTime = this.audioContext.currentTime;
            const stopTime = startTime + this.dtmfDuration / 1000;

            oscillator1.start(startTime);
            oscillator2.start(startTime);
            oscillator1.stop(stopTime);
            oscillator2.stop(stopTime);

            console.log(`🎵 DTMF tone played: ${digit} (${frequencies[0]}Hz + ${frequencies[1]}Hz)`);
            return true;

        } catch (error) {
            console.error(`❌ Failed to play DTMF tone for ${digit}:`, error);
            return false;
        }
    }

    removeDigit() {
        this.currentNumber = this.currentNumber.slice(0, -1);
        this.updateDisplay();
    }

    clearDisplay() {
        this.currentNumber = '';
        this.dialerInput.value = '';
        this.updateDisplay();
        this.hideAutoComplete();
    }

    updateDisplay() {
        if (this.currentNumber) {
            this.dialerDisplay.textContent = this.formatPhoneNumber(this.currentNumber);
        } else {
            this.dialerDisplay.textContent = 'Enter number...';
        }
    }

    formatPhoneNumber(number) {
        // Simple US phone number formatting
        const cleaned = number.replace(/\D/g, '');
        if (cleaned.length === 10) {
            return `(${cleaned.substr(0, 3)}) ${cleaned.substr(3, 3)}-${cleaned.substr(6, 4)}`;
        } else if (cleaned.length === 11 && cleaned.startsWith('1')) {
            return `+1 (${cleaned.substr(1, 3)}) ${cleaned.substr(4, 3)}-${cleaned.substr(7, 4)}`;
        }
        return number;
    }

    async makeCall() {
        if (!this.currentNumber) {
            this.showToast('Enter a number to call', 'warning');
            return;
        }

        if (!this.isConnected) {
            this.showToast('Connect to SIP server first', 'error');
            return;
        }

        try {
            const result = await window.flexPhoneAPI.sip.makeCall(this.currentNumber);
            if (result.success) {
                this.currentCall = result.callId;
                this.callBtn.style.display = 'none';
                this.hangupBtn.style.display = 'inline-block';
                this.showToast(`Calling ${this.currentNumber}...`, 'info');
            } else {
                this.showToast(`Call failed: ${result.error}`, 'error');
            }
        } catch (error) {
            this.showToast(`Call error: ${error.message}`, 'error');
        }
    }

    async hangupCall() {
        if (!this.currentCall) return;

        try {
            await window.flexPhoneAPI.sip.hangupCall(this.currentCall);
            this.currentCall = null;
            this.callBtn.style.display = 'inline-block';
            this.hangupBtn.style.display = 'none';
        } catch (error) {
            this.showToast(`Hangup error: ${error.message}`, 'error');
        }
    }

    async answerCall() {
        if (!this.currentCall) return;

        try {
            await window.flexPhoneAPI.sip.answerCall(this.currentCall);
            this.hideIncomingCallOverlay();
            this.callBtn.style.display = 'none';
            this.hangupBtn.style.display = 'inline-block';
        } catch (error) {
            this.showToast(`Answer error: ${error.message}`, 'error');
        }
    }

    async declineCall() {
        if (!this.currentCall) return;

        try {
            await window.flexPhoneAPI.sip.hangupCall(this.currentCall);
            this.hideIncomingCallOverlay();
            this.currentCall = null;
        } catch (error) {
            this.showToast(`Decline error: ${error.message}`, 'error');
        }
    }

    async connectSIP() {
        const config = {
            provider: this.sipProvider.value,
            server: this.sipServer.value,
            port: parseInt(this.sipPort.value),
            username: this.sipUsername.value,
            password: this.sipPassword.value,
            displayName: this.sipDisplayName.value
        };

        try {
            const result = await window.flexPhoneAPI.sip.connect(config);
            if (result.success) {
                this.isConnected = true;
                this.updateConnectionStatus(true, result.config.provider);
                this.connectBtn.style.display = 'none';
                this.disconnectBtn.style.display = 'inline-block';
                this.showToast('Connected to SIP server', 'success');
            } else {
                this.showToast(`Connection failed: ${result.error}`, 'error');
            }
        } catch (error) {
            this.showToast(`Connection error: ${error.message}`, 'error');
        }
    }

    async disconnectSIP() {
        try {
            await window.flexPhoneAPI.sip.disconnect();
            this.isConnected = false;
            this.updateConnectionStatus(false);
            this.connectBtn.style.display = 'inline-block';
            this.disconnectBtn.style.display = 'none';
            this.showToast('Disconnected from SIP server', 'info');
        } catch (error) {
            this.showToast(`Disconnect error: ${error.message}`, 'error');
        }
    }

    updateConnectionStatus(connected, provider = null) {
        this.isConnected = connected;

        if (connected) {
            this.statusIndicator.classList.add('connected');
            this.connectionStatus.textContent = `Connected to ${provider}`;
        } else {
            this.statusIndicator.classList.remove('connected');
            this.connectionStatus.textContent = 'Disconnected';
        }
    }

    onSIPConnected(data) {
        this.isConnected = true;
        this.updateConnectionStatus(true, data.provider);
        this.showToast('SIP connected', 'success');
    }

    onSIPDisconnected(data) {
        this.isConnected = false;
        this.updateConnectionStatus(false);
        this.showToast('SIP disconnected', 'info');
    }

    onIncomingCall(call) {
        this.currentCall = call.id;
        this.showIncomingCallOverlay(call);
    }

    onCallAnswered(call) {
        this.currentCall = call.id;
        this.callBtn.style.display = 'none';
        this.hangupBtn.style.display = 'inline-block';
        this.showToast('Call connected', 'success');
    }

    onCallEnded(call) {
        this.currentCall = null;
        this.callBtn.style.display = 'inline-block';
        this.hangupBtn.style.display = 'none';
        this.hideIncomingCallOverlay();
        this.showToast('Call ended', 'info');
    }

    onSMSReceived(message) {
        this.showToast(`New message from ${message.from}`, 'info');
        // Update messages tab if needed
    }

    showIncomingCallOverlay(call) {
        this.callerName.textContent = call.remoteName || 'Unknown Caller';
        this.callerNumber.textContent = call.remoteNumber;
        this.incomingCallOverlay.classList.add('show');
    }

    hideIncomingCallOverlay() {
        this.incomingCallOverlay.classList.remove('show');
    }

    switchTab(tabName) {
        // Update navigation
        this.navTabs.forEach(tab => {
            if (tab.dataset.tab === tabName) {
                tab.classList.add('active');
            } else {
                tab.classList.remove('active');
            }
        });

        // Update content
        this.tabContents.forEach(content => {
            if (content.id === tabName) {
                content.classList.add('active');
            } else {
                content.classList.remove('active');
            }
        });

        this.activeTab = tabName;

        // Load tab content if needed
        this.loadTabContent(tabName);
    }

    async loadTabContent(tabName) {
        const content = document.getElementById(tabName);

        switch (tabName) {
            case 'contacts':
                await this.loadContacts(content);
                break;
            case 'history':
                await this.loadCallHistory(content);
                break;
            case 'messages':
                await this.loadMessages(content);
                break;
            case 'settings':
                await this.loadSettings();
                break;
        }
    }

    async loadContacts(container) {
        try {
            const contacts = await window.flexPhoneAPI.contacts.getAll();

            if (contacts.length === 0) {
                container.innerHTML = '<div class="empty-state">📱 No contacts yet<br><small>Add contacts to get started</small></div>';
                return;
            }

            const list = document.createElement('ul');
            list.className = 'list';

            contacts.forEach(contact => {
                const item = document.createElement('li');
                item.className = 'list-item';
                item.innerHTML = `
                    <div class="list-item-icon">👤</div>
                    <div class="list-item-content">
                        <div class="list-item-title">${contact.displayName}</div>
                        <div class="list-item-subtitle">${contact.phoneNumbers[0]?.number || 'No phone'}</div>
                    </div>
                    <div class="list-item-meta">📞</div>
                `;

                item.addEventListener('click', () => {
                    if (contact.phoneNumbers[0]) {
                        this.currentNumber = contact.phoneNumbers[0].number;
                        this.switchTab('dialer');
                        this.updateDisplay();
                    }
                });

                list.appendChild(item);
            });

            container.innerHTML = '';
            container.appendChild(list);

        } catch (error) {
            container.innerHTML = '<div class="empty-state">❌ Failed to load contacts</div>';
        }
    }

    async loadCallHistory(container) {
        try {
            const history = await window.flexPhoneAPI.history.getCalls(50);

            if (history.length === 0) {
                container.innerHTML = '<div class="empty-state">📞 No call history<br><small>Make or receive calls to see history</small></div>';
                return;
            }

            const list = document.createElement('ul');
            list.className = 'list';

            history.forEach(call => {
                const item = document.createElement('li');
                item.className = 'list-item';

                const icon = call.direction === 'inbound' ?
                    (call.status === 'missed' ? '📞❌' : '📞⬇️') : '📞⬆️';

                const time = new Date(call.startTime).toLocaleString();

                item.innerHTML = `
                    <div class="list-item-icon">${icon}</div>
                    <div class="list-item-content">
                        <div class="list-item-title">${call.remoteName || call.remoteNumber}</div>
                        <div class="list-item-subtitle">${call.status} • ${this.formatDuration(call.duration)}</div>
                    </div>
                    <div class="list-item-meta">${time}</div>
                `;

                item.addEventListener('click', () => {
                    this.currentNumber = call.remoteNumber;
                    this.switchTab('dialer');
                    this.updateDisplay();
                });

                list.appendChild(item);
            });

            container.innerHTML = '';
            container.appendChild(list);

        } catch (error) {
            container.innerHTML = '<div class="empty-state">❌ Failed to load call history</div>';
        }
    }

    async loadMessages(container) {
        try {
            const conversations = await window.flexPhoneAPI.sms.getConversations();

            if (conversations.length === 0) {
                container.innerHTML = '<div class="empty-state">💬 No messages yet<br><small>Send SMS messages to get started</small></div>';
                return;
            }

            const list = document.createElement('ul');
            list.className = 'list';

            conversations.forEach(conv => {
                const item = document.createElement('li');
                item.className = 'list-item';

                const time = new Date(conv.lastMessageTime).toLocaleString();

                item.innerHTML = `
                    <div class="list-item-icon">💬</div>
                    <div class="list-item-content">
                        <div class="list-item-title">${conv.displayName}</div>
                        <div class="list-item-subtitle">${conv.lastMessage}</div>
                    </div>
                    <div class="list-item-meta">${time}</div>
                `;

                list.appendChild(item);
            });

            container.innerHTML = '';
            container.appendChild(list);

        } catch (error) {
            container.innerHTML = '<div class="empty-state">❌ Failed to load messages</div>';
        }
    }

    async loadSettings() {
        try {
            const settings = await window.flexPhoneAPI.settings.getAll();

            this.sipProvider.value = settings['sip.provider'] || 'FLEXPBX';
            this.sipServer.value = settings['sip.server'] || 'flexpbx.local';
            this.sipPort.value = settings['sip.port'] || 5070;
            this.sipUsername.value = settings['sip.username'] || '';
            this.sipPassword.value = settings['sip.password'] || '';
            this.sipDisplayName.value = settings['sip.displayName'] || '';

        } catch (error) {
            console.error('Failed to load settings:', error);
        }
    }

    async saveSettings() {
        try {
            await window.flexPhoneAPI.settings.set('sip.provider', this.sipProvider.value);
            await window.flexPhoneAPI.settings.set('sip.server', this.sipServer.value);
            await window.flexPhoneAPI.settings.set('sip.port', parseInt(this.sipPort.value));
            await window.flexPhoneAPI.settings.set('sip.username', this.sipUsername.value);
            await window.flexPhoneAPI.settings.set('sip.password', this.sipPassword.value);
            await window.flexPhoneAPI.settings.set('sip.displayName', this.sipDisplayName.value);

        } catch (error) {
            console.error('Failed to save settings:', error);
        }
    }

    formatDuration(duration) {
        if (!duration) return '0s';

        const seconds = Math.floor(duration / 1000);
        const minutes = Math.floor(seconds / 60);
        const remainingSeconds = seconds % 60;

        if (minutes > 0) {
            return `${minutes}m ${remainingSeconds}s`;
        } else {
            return `${remainingSeconds}s`;
        }
    }

    showToast(message, type = 'info') {
        // Simple toast notification
        const toast = document.createElement('div');
        toast.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: ${type === 'error' ? '#dc3545' : type === 'success' ? '#28a745' : type === 'warning' ? '#ffc107' : '#007bff'};
            color: white;
            padding: 12px 20px;
            border-radius: 6px;
            z-index: 1001;
            font-size: 14px;
            opacity: 0;
            transform: translateX(100%);
            transition: all 0.3s ease;
        `;
        toast.textContent = message;

        document.body.appendChild(toast);

        // Animate in
        setTimeout(() => {
            toast.style.opacity = '1';
            toast.style.transform = 'translateX(0)';
        }, 100);

        // Remove after 3 seconds
        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(100%)';
            setTimeout(() => {
                document.body.removeChild(toast);
            }, 300);
        }, 3000);
    }

    // Auto-complete functionality
    async loadContacts() {
        try {
            this.contacts = await window.flexPhoneAPI.contacts.getAll();
            console.log(`📱 Loaded ${this.contacts.length} contacts`);
        } catch (error) {
            console.error('❌ Failed to load contacts:', error);
            this.contacts = [];
        }
    }

    updateAutoComplete() {
        const query = this.currentNumber.toLowerCase();
        if (query.length < 1) {
            this.hideAutoComplete();
            return;
        }

        // Filter contacts by name or number
        this.autoCompleteItems = this.contacts.filter(contact => {
            return contact.name.toLowerCase().includes(query) ||
                   contact.numbers.some(num => num.includes(query));
        }).slice(0, 5); // Limit to 5 results

        if (this.autoCompleteItems.length > 0) {
            this.showAutoComplete();
        } else {
            this.hideAutoComplete();
        }
    }

    showAutoComplete() {
        this.autoCompleteDropdown.innerHTML = '';
        this.selectedAutoCompleteIndex = -1;

        this.autoCompleteItems.forEach((contact, index) => {
            const item = document.createElement('div');
            item.className = 'auto-complete-item';
            item.innerHTML = `
                <span class="auto-complete-name">${contact.name}</span>
                <span class="auto-complete-number">${contact.numbers[0]}</span>
            `;

            item.addEventListener('click', () => {
                this.selectAutoCompleteItem(index);
            });

            this.autoCompleteDropdown.appendChild(item);
        });

        this.autoCompleteDropdown.style.display = 'block';
    }

    hideAutoComplete() {
        this.autoCompleteDropdown.style.display = 'none';
        this.selectedAutoCompleteIndex = -1;
    }

    navigateAutoComplete(direction) {
        const items = this.autoCompleteDropdown.querySelectorAll('.auto-complete-item');
        if (items.length === 0) return;

        // Remove previous selection
        if (this.selectedAutoCompleteIndex >= 0) {
            items[this.selectedAutoCompleteIndex].classList.remove('selected');
        }

        // Update selection
        this.selectedAutoCompleteIndex += direction;
        if (this.selectedAutoCompleteIndex < 0) {
            this.selectedAutoCompleteIndex = items.length - 1;
        } else if (this.selectedAutoCompleteIndex >= items.length) {
            this.selectedAutoCompleteIndex = 0;
        }

        // Apply new selection
        items[this.selectedAutoCompleteIndex].classList.add('selected');
    }

    selectAutoCompleteItem(index) {
        const contact = this.autoCompleteItems[index];
        if (contact) {
            this.currentNumber = contact.numbers[0];
            this.dialerInput.value = this.currentNumber;
            this.updateDisplay();
            this.hideAutoComplete();
            this.showToast(`Selected: ${contact.name}`, 'info');
        }
    }

    // DTMF functionality
    toggleDTMF() {
        this.dtmfEnabled = !this.dtmfEnabled;
        this.dtmfEnabledText.textContent = this.dtmfEnabled ? 'Enabled' : 'Disabled';
        this.dtmfIndicator.textContent = this.dtmfEnabled ? '🔊' : '🔇';
        this.showToast(`DTMF tones ${this.dtmfEnabled ? 'enabled' : 'disabled'}`, 'info');
    }

    async testAllDTMFTones() {
        if (!this.dtmfEnabled) {
            this.showToast('DTMF tones are disabled', 'warning');
            return;
        }

        this.showToast('Testing DTMF tones...', 'info');
        const digits = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '*', '0', '#'];

        for (const digit of digits) {
            await this.playDTMFTone(digit);
            await new Promise(resolve => setTimeout(resolve, 300));
        }

        this.showToast('DTMF test completed', 'success');
    }

    // SMS functionality
    openSMS() {
        if (!this.currentNumber) {
            this.showToast('Enter a number first', 'warning');
            return;
        }

        // Create SMS modal/interface
        const smsText = prompt(`Send SMS to ${this.currentNumber}:`);
        if (smsText) {
            this.sendSMS(this.currentNumber, smsText);
        }
    }

    async sendSMS(to, message) {
        try {
            const result = await window.flexPhoneAPI.sms.send(to, message);
            if (result.success) {
                this.showToast(`SMS sent to ${to}`, 'success');
            } else {
                this.showToast(`SMS failed: ${result.error}`, 'error');
            }
        } catch (error) {
            this.showToast(`SMS error: ${error.message}`, 'error');
        }
    }

    // Contact management
    addContact() {
        if (!this.currentNumber) {
            this.showToast('Enter a number first', 'warning');
            return;
        }

        const name = prompt(`Add contact name for ${this.currentNumber}:`);
        if (name) {
            this.saveContact(name, this.currentNumber);
        }
    }

    async saveContact(name, number) {
        try {
            const contact = {
                name: name,
                numbers: [number],
                email: '',
                notes: ''
            };

            const result = await window.flexPhoneAPI.contacts.add(contact);
            if (result.success) {
                this.showToast(`Contact "${name}" added`, 'success');
                await this.loadContacts(); // Reload contacts
            } else {
                this.showToast(`Failed to add contact: ${result.error}`, 'error');
            }
        } catch (error) {
            this.showToast(`Contact error: ${error.message}`, 'error');
        }
    }

    // Register service worker for PWA functionality
    async registerServiceWorker() {
        if ('serviceWorker' in navigator) {
            try {
                const registration = await navigator.serviceWorker.register('/sw.js');
                console.log('FlexPhone: Service Worker registered:', registration);

                // Check for app updates
                registration.addEventListener('updatefound', () => {
                    console.log('FlexPhone: New version available');
                    this.showToast('New version available! Refresh to update.', 'info');
                });
            } catch (error) {
                console.log('FlexPhone: Service Worker registration failed:', error);
            }
        }
    }
}

// Initialize app when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.flexPhoneApp = new FlexPhoneApp();
});