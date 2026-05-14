/**
 * 🔊 FlexPhone Ringtone Service
 * Manages ringtones and audio notifications for FlexPhone
 */

const EventEmitter = require('events');

class RingtoneService extends EventEmitter {
    constructor() {
        super();
        this.currentRingtone = null;
        this.isPlaying = false;
        this.volume = 0.7;
        this.selectedRingtone = 'default';
        this.customRingtonePath = null;
        this.remoteRingtones = [];
        this.localServerRingtones = [];
        this.lastRemoteSync = null;

        // Built-in ringtone patterns
        this.ringtonePatterns = {
            default: {
                name: 'Default Ring',
                pattern: [
                    { freq: 440, duration: 400 },
                    { freq: 0, duration: 200 },
                    { freq: 440, duration: 400 },
                    { freq: 0, duration: 1000 }
                ]
            },
            classic: {
                name: 'Classic Phone',
                pattern: [
                    { freq: 440, duration: 500 },
                    { freq: 480, duration: 500 },
                    { freq: 0, duration: 500 }
                ]
            },
            modern: {
                name: 'Modern Ring',
                pattern: [
                    { freq: 523, duration: 150 },
                    { freq: 659, duration: 150 },
                    { freq: 784, duration: 150 },
                    { freq: 0, duration: 200 },
                    { freq: 523, duration: 150 },
                    { freq: 659, duration: 150 },
                    { freq: 784, duration: 150 },
                    { freq: 0, duration: 800 }
                ]
            },
            bell: {
                name: 'Bell',
                pattern: [
                    { freq: 800, duration: 200 },
                    { freq: 600, duration: 200 },
                    { freq: 400, duration: 200 },
                    { freq: 0, duration: 400 }
                ]
            },
            chime: {
                name: 'Chime',
                pattern: [
                    { freq: 523, duration: 300 },
                    { freq: 659, duration: 300 },
                    { freq: 784, duration: 300 },
                    { freq: 1047, duration: 600 },
                    { freq: 0, duration: 1200 }
                ]
            }
        };

        console.log('🔊 RingtoneService initialized');
    }

    async initialize() {
        try {
            // Initialize Web Audio API when needed
            console.log('✅ RingtoneService ready');
            return true;
        } catch (error) {
            console.error('❌ RingtoneService initialization failed:', error);
            return false;
        }
    }

    /**
     * Start playing the selected ringtone
     */
    async startRinging() {
        if (this.isPlaying) {
            return;
        }

        try {
            this.isPlaying = true;

            if (this.selectedRingtone === 'custom' && this.customRingtonePath) {
                await this.playCustomRingtone();
            } else {
                await this.playBuiltInRingtone(this.selectedRingtone);
            }

            console.log(`🔊 Started ringing with '${this.selectedRingtone}' ringtone`);
            this.emit('ringStart', { ringtone: this.selectedRingtone });

        } catch (error) {
            console.error('❌ Failed to start ringing:', error);
            this.isPlaying = false;
        }
    }

    /**
     * Stop playing the ringtone
     */
    stopRinging() {
        if (!this.isPlaying) {
            return;
        }

        this.isPlaying = false;

        if (this.currentRingtone) {
            this.currentRingtone.stop();
            this.currentRingtone = null;
        }

        console.log('🔇 Stopped ringing');
        this.emit('ringStop');
    }

    /**
     * Play a built-in ringtone pattern
     */
    async playBuiltInRingtone(ringtoneName) {
        const pattern = this.ringtonePatterns[ringtoneName] || this.ringtonePatterns.default;

        const playPattern = async () => {
            if (!this.isPlaying) return;

            for (const note of pattern.pattern) {
                if (!this.isPlaying) break;

                if (note.freq > 0) {
                    await this.playTone(note.freq, note.duration);
                } else {
                    await this.wait(note.duration);
                }
            }

            // Loop the pattern while ringing
            if (this.isPlaying) {
                setTimeout(playPattern, 100);
            }
        };

        playPattern();
    }

    /**
     * Play a custom ringtone file
     */
    async playCustomRingtone() {
        // Implementation for custom ringtone files
        // This would load and play a custom audio file
        console.log(`🎵 Playing custom ringtone: ${this.customRingtonePath}`);

        // Fallback to default if custom fails
        await this.playBuiltInRingtone('default');
    }

    /**
     * Play a single tone
     */
    async playTone(frequency, duration) {
        return new Promise((resolve) => {
            try {
                // Create a simple tone using Web Audio API
                const oscillator = this.createOscillator(frequency);
                const gainNode = this.createGainNode();

                oscillator.connect(gainNode);
                gainNode.connect(this.getAudioContext().destination);

                oscillator.start();

                setTimeout(() => {
                    oscillator.stop();
                    resolve();
                }, duration);

            } catch (error) {
                console.error('Failed to play tone:', error);
                resolve();
            }
        });
    }

    /**
     * Create an oscillator for tone generation
     */
    createOscillator(frequency) {
        const audioContext = this.getAudioContext();
        const oscillator = audioContext.createOscillator();

        oscillator.type = 'sine';
        oscillator.frequency.setValueAtTime(frequency, audioContext.currentTime);

        return oscillator;
    }

    /**
     * Create a gain node for volume control
     */
    createGainNode() {
        const audioContext = this.getAudioContext();
        const gainNode = audioContext.createGain();

        gainNode.gain.setValueAtTime(this.volume, audioContext.currentTime);

        return gainNode;
    }

    /**
     * Get or create audio context
     */
    getAudioContext() {
        if (!this.audioContext) {
            this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }
        return this.audioContext;
    }

    /**
     * Wait for a specified duration
     */
    async wait(duration) {
        return new Promise(resolve => setTimeout(resolve, duration));
    }

    /**
     * Test a specific ringtone
     */
    async testRingtone(ringtoneName = null) {
        const testRingtone = ringtoneName || this.selectedRingtone;

        try {
            this.stopRinging(); // Stop any current ringing

            const wasSelected = this.selectedRingtone;
            this.selectedRingtone = testRingtone;

            await this.startRinging();

            // Stop after 3 seconds
            setTimeout(() => {
                this.stopRinging();
                this.selectedRingtone = wasSelected; // Restore original selection
            }, 3000);

            console.log(`🔊 Testing ringtone: ${testRingtone}`);

        } catch (error) {
            console.error('❌ Failed to test ringtone:', error);
        }
    }

    /**
     * Set the selected ringtone
     */
    setRingtone(ringtoneName) {
        if (ringtoneName === 'custom') {
            this.selectedRingtone = 'custom';
        } else if (this.ringtonePatterns[ringtoneName]) {
            this.selectedRingtone = ringtoneName;
        } else {
            this.selectedRingtone = 'default';
        }

        console.log(`🔊 Ringtone set to: ${this.selectedRingtone}`);
        this.emit('ringtoneChanged', { ringtone: this.selectedRingtone });
    }

    /**
     * Set custom ringtone file path
     */
    setCustomRingtone(filePath) {
        this.customRingtonePath = filePath;
        this.selectedRingtone = 'custom';

        console.log(`🎵 Custom ringtone set: ${filePath}`);
        this.emit('customRingtoneSet', { path: filePath });
    }

    /**
     * Set volume (0.0 to 1.0)
     */
    setVolume(volume) {
        this.volume = Math.max(0, Math.min(1, volume));
        console.log(`🔊 Ringtone volume set to: ${this.volume * 100}%`);
    }

    /**
     * Get available ringtones
     */
    getAvailableRingtones() {
        return Object.keys(this.ringtonePatterns).map(key => ({
            id: key,
            name: this.ringtonePatterns[key].name
        }));
    }

    /**
     * Get current ringtone info
     */
    getCurrentRingtone() {
        return {
            selected: this.selectedRingtone,
            customPath: this.customRingtonePath,
            volume: this.volume,
            isPlaying: this.isPlaying
        };
    }

    /**
     * Sync ringtones from remote FlexPBX servers
     */
    async syncRemoteRingtones(serverUrl, authToken = null) {
        try {
            console.log(`🌐 Syncing ringtones from remote server: ${serverUrl}`);

            const headers = {
                'Content-Type': 'application/json'
            };

            if (authToken) {
                headers['Authorization'] = `Bearer ${authToken}`;
            }

            const response = await fetch(`${serverUrl}/api/ringtones/list`, {
                method: 'GET',
                headers
            });

            if (!response.ok) {
                throw new Error(`Remote server responded with ${response.status}`);
            }

            const data = await response.json();
            this.remoteRingtones = data.ringtones || [];
            this.lastRemoteSync = new Date();

            console.log(`✅ Synced ${this.remoteRingtones.length} ringtones from remote server`);
            this.emit('remoteRingtonesSynced', {
                count: this.remoteRingtones.length,
                server: serverUrl
            });

            return this.remoteRingtones;

        } catch (error) {
            console.error('❌ Failed to sync remote ringtones:', error);
            this.emit('remoteRingtoneSyncFailed', { error: error.message });
            return [];
        }
    }

    /**
     * Sync ringtones from local FlexPBX server
     */
    async syncLocalServerRingtones(serverUrl = 'http://localhost:8080') {
        try {
            console.log(`🏠 Syncing ringtones from local FlexPBX server: ${serverUrl}`);

            const response = await fetch(`${serverUrl}/api/ringtones/list`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error(`Local server responded with ${response.status}`);
            }

            const data = await response.json();
            this.localServerRingtones = data.ringtones || [];

            console.log(`✅ Synced ${this.localServerRingtones.length} ringtones from local server`);
            this.emit('localRingtonesSynced', {
                count: this.localServerRingtones.length,
                server: serverUrl
            });

            return this.localServerRingtones;

        } catch (error) {
            console.error('❌ Failed to sync local server ringtones:', error);
            this.emit('localRingtoneSyncFailed', { error: error.message });
            return [];
        }
    }

    /**
     * Download and cache a remote ringtone
     */
    async downloadRemoteRingtone(ringtoneId, serverUrl, authToken = null) {
        try {
            console.log(`⬇️ Downloading ringtone ${ringtoneId} from ${serverUrl}`);

            const headers = {};
            if (authToken) {
                headers['Authorization'] = `Bearer ${authToken}`;
            }

            const response = await fetch(`${serverUrl}/api/ringtones/${ringtoneId}/download`, {
                method: 'GET',
                headers
            });

            if (!response.ok) {
                throw new Error(`Failed to download ringtone: ${response.status}`);
            }

            const audioData = await response.arrayBuffer();

            // Store in browser cache or local storage for reuse
            const cachedRingtone = {
                id: ringtoneId,
                audioData,
                downloadedAt: new Date(),
                server: serverUrl
            };

            console.log(`✅ Downloaded ringtone ${ringtoneId}`);
            this.emit('ringtoneDownloaded', { ringtoneId, server: serverUrl });

            return cachedRingtone;

        } catch (error) {
            console.error(`❌ Failed to download ringtone ${ringtoneId}:`, error);
            return null;
        }
    }

    /**
     * Play a remote ringtone
     */
    async playRemoteRingtone(ringtoneId, serverUrl, authToken = null) {
        try {
            const audioContext = this.getAudioContext();

            // Try to get from cache first
            let ringtoneData = await this.downloadRemoteRingtone(ringtoneId, serverUrl, authToken);

            if (!ringtoneData) {
                console.error('Failed to download remote ringtone');
                return;
            }

            const audioBuffer = await audioContext.decodeAudioData(ringtoneData.audioData);
            const source = audioContext.createBufferSource();
            const gainNode = this.createGainNode();

            source.buffer = audioBuffer;
            source.connect(gainNode);
            gainNode.connect(audioContext.destination);

            source.loop = true; // Loop for ringing
            source.start();

            this.currentRingtone = source;

            console.log(`🔊 Playing remote ringtone: ${ringtoneId}`);

        } catch (error) {
            console.error('❌ Failed to play remote ringtone:', error);
            // Fallback to default ringtone
            await this.playBuiltInRingtone('default');
        }
    }

    /**
     * Get all available ringtones (built-in + remote + local server)
     */
    getAllAvailableRingtones() {
        const builtIn = this.getAvailableRingtones().map(r => ({
            ...r,
            type: 'built-in',
            source: 'local'
        }));

        const remote = this.remoteRingtones.map(r => ({
            id: `remote_${r.id}`,
            name: `${r.name} (Remote)`,
            type: 'remote',
            source: 'remote',
            originalId: r.id,
            server: r.server,
            description: r.description
        }));

        const localServer = this.localServerRingtones.map(r => ({
            id: `local_${r.id}`,
            name: `${r.name} (Local Server)`,
            type: 'server',
            source: 'local-server',
            originalId: r.id,
            description: r.description
        }));

        return [
            ...builtIn,
            ...remote,
            ...localServer,
            {
                id: 'custom',
                name: 'Custom Ringtone...',
                type: 'custom',
                source: 'local'
            }
        ];
    }

    /**
     * Auto-sync ringtones from configured servers
     */
    async autoSyncRingtones(config = {}) {
        const promises = [];

        // Sync from local server if configured
        if (config.localServerUrl) {
            promises.push(this.syncLocalServerRingtones(config.localServerUrl));
        }

        // Sync from remote servers if configured
        if (config.remoteServers && Array.isArray(config.remoteServers)) {
            for (const server of config.remoteServers) {
                promises.push(this.syncRemoteRingtones(server.url, server.authToken));
            }
        }

        // Wait for all syncs to complete
        const results = await Promise.allSettled(promises);

        const successful = results.filter(r => r.status === 'fulfilled').length;
        const failed = results.filter(r => r.status === 'rejected').length;

        console.log(`🔄 Ringtone sync complete: ${successful} successful, ${failed} failed`);

        this.emit('autoSyncComplete', { successful, failed, total: results.length });

        return { successful, failed, total: results.length };
    }
}

module.exports = RingtoneService;