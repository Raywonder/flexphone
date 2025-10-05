/**
 * 📞 FlexPhone SIP Service
 * Lightweight SIP client for universal provider support
 */

const EventEmitter = require('events');
const crypto = require('crypto');

class SIPService extends EventEmitter {
    constructor() {
        super();

        this.sipProviders = {
            FLEXPBX: {
                name: 'FlexPBX',
                defaultServer: 'flexpbx.local',
                defaultPort: 5070,
                transport: 'UDP',
                features: ['calls', 'sms', 'presence', 'conference']
            },
            CALLCENTRIC: {
                name: 'CallCentric',
                defaultServer: 'sip.callcentric.com',
                defaultPort: 5060,
                transport: 'UDP',
                features: ['calls', 'sms']
            },
            VOIPMS: {
                name: 'VoIP.ms',
                defaultServer: 'server.voip.ms',
                defaultPort: 5060,
                transport: 'UDP',
                features: ['calls', 'sms']
            },
            TWILIO: {
                name: 'Twilio',
                defaultServer: 'edge.twilio.com',
                defaultPort: 5060,
                transport: 'TLS',
                features: ['calls', 'sms', 'video']
            },
            GOOGLE_VOICE: {
                name: 'Google Voice',
                defaultServer: 'voice.google.com',
                defaultPort: 5060,
                transport: 'TLS',
                features: ['calls', 'sms', 'voicemail']
            },
            CUSTOM: {
                name: 'Custom SIP Provider',
                defaultServer: '',
                defaultPort: 5060,
                transport: 'UDP',
                features: ['calls']
            }
        };

        // Connection state
        this.isConnected = false;
        this.currentConfig = null;
        this.registrationState = 'unregistered';

        // Call management
        this.activeCalls = new Map();
        this.callHistory = [];

        // Audio management
        this.audioContext = null;
        this.localStream = null;
        this.remoteStream = null;

        console.log('📞 FlexPhone SIP Service initialized');
    }

    async initialize() {
        try {
            // Initialize audio context for WebRTC
            if (typeof window !== 'undefined' && window.AudioContext) {
                this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
            }

            console.log('✅ SIP Service ready');
            return true;

        } catch (error) {
            console.error('❌ SIP Service initialization failed:', error);
            return false;
        }
    }

    async connect(config) {
        try {
            console.log(`📞 Connecting to ${config.provider} SIP server...`);

            // Validate configuration
            const validationResult = this.validateConfig(config);
            if (!validationResult.valid) {
                throw new Error(`Invalid config: ${validationResult.error}`);
            }

            // Get provider defaults
            const provider = this.sipProviders[config.provider];
            const sipConfig = {
                provider: config.provider,
                server: config.server || provider.defaultServer,
                port: config.port || provider.defaultPort,
                username: config.username,
                password: config.password,
                displayName: config.displayName || config.username,
                transport: config.transport || provider.transport,
                features: provider.features
            };

            // Store current config
            this.currentConfig = sipConfig;

            // Simulate SIP registration
            await this.simulateSIPRegistration(sipConfig);

            // Set connected state
            this.isConnected = true;
            this.registrationState = 'registered';

            console.log(`✅ Connected to ${provider.name}`);
            console.log(`   Server: ${sipConfig.server}:${sipConfig.port}`);
            console.log(`   Username: ${sipConfig.username}`);
            console.log(`   Transport: ${sipConfig.transport}`);

            this.emit('connected', {
                provider: sipConfig.provider,
                server: sipConfig.server,
                username: sipConfig.username,
                features: sipConfig.features
            });

            return {
                success: true,
                message: `Connected to ${provider.name}`,
                config: sipConfig
            };

        } catch (error) {
            console.error('❌ SIP connection failed:', error);
            this.isConnected = false;
            this.registrationState = 'failed';

            this.emit('connection-failed', { error: error.message });

            return {
                success: false,
                error: error.message
            };
        }
    }

    async disconnect() {
        try {
            if (!this.isConnected) {
                return { success: true, message: 'Not connected' };
            }

            console.log('📞 Disconnecting from SIP server...');

            // End all active calls
            for (const [callId, call] of this.activeCalls) {
                await this.hangupCall(callId);
            }

            // Clear state
            this.isConnected = false;
            this.registrationState = 'unregistered';
            this.currentConfig = null;

            console.log('✅ Disconnected from SIP server');

            this.emit('disconnected', {
                timestamp: new Date().toISOString()
            });

            return {
                success: true,
                message: 'Disconnected successfully'
            };

        } catch (error) {
            console.error('❌ SIP disconnect failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async makeCall(number, options = {}) {
        try {
            if (!this.isConnected) {
                throw new Error('Not connected to SIP server');
            }

            const callId = this.generateCallId();
            const call = {
                id: callId,
                direction: 'outbound',
                remoteNumber: number,
                localNumber: this.currentConfig.username,
                status: 'connecting',
                startTime: new Date(),
                connectTime: null,
                endTime: null,
                duration: 0,
                provider: this.currentConfig.provider,
                options: options
            };

            this.activeCalls.set(callId, call);

            console.log(`📞 Making call: ${this.currentConfig.username} → ${number}`);

            // Simulate call connection process
            await this.simulateCallConnection(call);

            this.emit('call-initiated', call);

            return {
                success: true,
                callId: callId,
                message: `Calling ${number}...`
            };

        } catch (error) {
            console.error('❌ Call failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async answerCall(callId) {
        try {
            const call = this.activeCalls.get(callId);
            if (!call) {
                throw new Error('Call not found');
            }

            if (call.status !== 'ringing') {
                throw new Error(`Cannot answer call in state: ${call.status}`);
            }

            console.log(`📞 Answering call: ${callId}`);

            call.status = 'connected';
            call.connectTime = new Date();

            // Initialize audio for the call
            await this.initializeCallAudio(call);

            console.log(`✅ Call answered: ${call.remoteNumber}`);

            this.emit('call-answered', call);

            return {
                success: true,
                callId: callId,
                message: 'Call answered'
            };

        } catch (error) {
            console.error('❌ Answer call failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async hangupCall(callId) {
        try {
            const call = this.activeCalls.get(callId);
            if (!call) {
                throw new Error('Call not found');
            }

            console.log(`📞 Hanging up call: ${callId}`);

            call.status = 'ended';
            call.endTime = new Date();

            if (call.connectTime) {
                call.duration = call.endTime - call.connectTime;
            }

            // Clean up audio
            await this.cleanupCallAudio(call);

            // Move to call history
            this.callHistory.push({ ...call });
            this.activeCalls.delete(callId);

            console.log(`✅ Call ended: ${call.remoteNumber} (Duration: ${Math.round(call.duration / 1000)}s)`);

            this.emit('call-ended', call);

            return {
                success: true,
                callId: callId,
                duration: call.duration
            };

        } catch (error) {
            console.error('❌ Hangup failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async sendDTMF(digits) {
        try {
            if (this.activeCalls.size === 0) {
                throw new Error('No active calls');
            }

            const activeCall = Array.from(this.activeCalls.values())[0];
            console.log(`🔢 Sending DTMF: ${digits}`);

            // Simulate DTMF sending
            for (const digit of digits) {
                await this.simulateDTMFTone(digit);
                await new Promise(resolve => setTimeout(resolve, 100));
            }

            this.emit('dtmf-sent', {
                callId: activeCall.id,
                digits: digits
            });

            return {
                success: true,
                message: `DTMF sent: ${digits}`
            };

        } catch (error) {
            console.error('❌ DTMF send failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async simulateSIPRegistration(config) {
        console.log('🔐 Registering with SIP server...');

        // Simulate network delay
        await new Promise(resolve => setTimeout(resolve, 1000));

        // Simulate authentication
        console.log('🔑 Authenticating credentials...');
        await new Promise(resolve => setTimeout(resolve, 500));

        // Success
        console.log('✅ SIP registration successful');
    }

    async simulateCallConnection(call) {
        // Update call status through connection phases
        setTimeout(() => {
            call.status = 'ringing';
            console.log(`📞 Call ${call.id}: Ringing`);
            this.emit('call-ringing', call);
        }, 1000);

        setTimeout(() => {
            call.status = 'connected';
            call.connectTime = new Date();
            console.log(`📞 Call ${call.id}: Connected`);
            this.emit('call-connected', call);
        }, 3000);

        return call;
    }

    async simulateIncomingCall(fromNumber, fromName = null) {
        const callId = this.generateCallId();
        const call = {
            id: callId,
            direction: 'inbound',
            remoteNumber: fromNumber,
            remoteName: fromName,
            localNumber: this.currentConfig.username,
            status: 'ringing',
            startTime: new Date(),
            connectTime: null,
            endTime: null,
            duration: 0,
            provider: this.currentConfig.provider
        };

        this.activeCalls.set(callId, call);

        console.log(`📞 Incoming call from: ${fromName || fromNumber}`);

        this.emit('incoming-call', call);

        // Auto-hangup after 30 seconds if not answered
        setTimeout(() => {
            if (this.activeCalls.has(callId) && call.status === 'ringing') {
                this.hangupCall(callId);
            }
        }, 30000);

        return call;
    }

    async initializeCallAudio(call) {
        try {
            if (typeof navigator !== 'undefined' && navigator.mediaDevices) {
                // Get user media for WebRTC
                this.localStream = await navigator.mediaDevices.getUserMedia({
                    audio: {
                        echoCancellation: true,
                        noiseSuppression: true,
                        autoGainControl: true
                    }
                });

                console.log('🎤 Audio initialized for call');
            }
        } catch (error) {
            console.warn('⚠️ Audio initialization failed:', error);
        }
    }

    async cleanupCallAudio(call) {
        try {
            if (this.localStream) {
                this.localStream.getTracks().forEach(track => track.stop());
                this.localStream = null;
            }

            console.log('🔇 Audio cleaned up for call');
        } catch (error) {
            console.warn('⚠️ Audio cleanup failed:', error);
        }
    }

    async simulateDTMFTone(digit) {
        console.log(`🔢 Playing DTMF tone: ${digit}`);

        // DTMF frequencies
        const dtmfFreqs = {
            '1': [697, 1209], '2': [697, 1336], '3': [697, 1477],
            '4': [770, 1209], '5': [770, 1336], '6': [770, 1477],
            '7': [852, 1209], '8': [852, 1336], '9': [852, 1477],
            '*': [941, 1209], '0': [941, 1336], '#': [941, 1477]
        };

        if (this.audioContext && dtmfFreqs[digit]) {
            const [lowFreq, highFreq] = dtmfFreqs[digit];
            const oscillator1 = this.audioContext.createOscillator();
            const oscillator2 = this.audioContext.createOscillator();
            const gainNode = this.audioContext.createGain();

            oscillator1.frequency.setValueAtTime(lowFreq, this.audioContext.currentTime);
            oscillator2.frequency.setValueAtTime(highFreq, this.audioContext.currentTime);

            oscillator1.connect(gainNode);
            oscillator2.connect(gainNode);
            gainNode.connect(this.audioContext.destination);

            gainNode.gain.setValueAtTime(0.1, this.audioContext.currentTime);

            oscillator1.start();
            oscillator2.start();

            setTimeout(() => {
                oscillator1.stop();
                oscillator2.stop();
            }, 200);
        }
    }

    validateConfig(config) {
        if (!config.provider) {
            return { valid: false, error: 'Provider is required' };
        }

        if (!this.sipProviders[config.provider]) {
            return { valid: false, error: 'Unknown provider' };
        }

        if (!config.username) {
            return { valid: false, error: 'Username is required' };
        }

        if (!config.password) {
            return { valid: false, error: 'Password is required' };
        }

        if (config.provider === 'CUSTOM' && !config.server) {
            return { valid: false, error: 'Server is required for custom provider' };
        }

        return { valid: true };
    }

    generateCallId() {
        return 'call_' + crypto.randomBytes(8).toString('hex');
    }

    getStatus() {
        return {
            isConnected: this.isConnected,
            registrationState: this.registrationState,
            provider: this.currentConfig?.provider || null,
            server: this.currentConfig?.server || null,
            username: this.currentConfig?.username || null,
            activeCalls: this.activeCalls.size,
            totalCalls: this.callHistory.length,
            features: this.currentConfig?.features || []
        };
    }

    getActiveCalls() {
        return Array.from(this.activeCalls.values());
    }

    getCallHistory(limit = 50) {
        return this.callHistory
            .sort((a, b) => b.startTime - a.startTime)
            .slice(0, limit);
    }

    getSupportedProviders() {
        return Object.entries(this.sipProviders).map(([key, provider]) => ({
            id: key,
            name: provider.name,
            defaultServer: provider.defaultServer,
            defaultPort: provider.defaultPort,
            transport: provider.transport,
            features: provider.features
        }));
    }

    // Test methods for development
    async testIncomingCall() {
        if (!this.isConnected) {
            throw new Error('Not connected to SIP server');
        }

        return await this.simulateIncomingCall('+1 (336) 462-6141', 'Test Caller');
    }

    async testOutgoingCall() {
        if (!this.isConnected) {
            throw new Error('Not connected to SIP server');
        }

        return await this.makeCall('+1 (281) 301-5784', { test: true });
    }
}

module.exports = SIPService;