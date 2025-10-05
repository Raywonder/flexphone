/**
 * 🎵 FlexPhone DTMF Service
 * Generates and plays DTMF (Dual-Tone Multi-Frequency) tones for dialer feedback
 */

class DTMFService {
    constructor() {
        this.audioContext = null;
        this.enabled = true;
        this.volume = 0.1;
        this.toneDuration = 200; // milliseconds

        // DTMF frequency mappings
        this.dtmfFrequencies = {
            '1': [697, 1209], '2': [697, 1336], '3': [697, 1477], 'A': [697, 1633],
            '4': [770, 1209], '5': [770, 1336], '6': [770, 1477], 'B': [770, 1633],
            '7': [852, 1209], '8': [852, 1336], '9': [852, 1477], 'C': [852, 1633],
            '*': [941, 1209], '0': [941, 1336], '#': [941, 1477], 'D': [941, 1633]
        };

        this.initializeAudioContext();
        console.log('🎵 FlexPhone DTMF Service initialized');
    }

    async initializeAudioContext() {
        try {
            // Create AudioContext for tone generation
            this.audioContext = new (window.AudioContext || window.webkitAudioContext)();

            // Resume context if it's suspended (browser autoplay policy)
            if (this.audioContext.state === 'suspended') {
                await this.audioContext.resume();
            }

            console.log('🎵 DTMF AudioContext initialized');
        } catch (error) {
            console.error('❌ Failed to initialize DTMF AudioContext:', error);
            this.enabled = false;
        }
    }

    async playDTMFTone(digit) {
        if (!this.enabled || !this.audioContext) {
            console.log('⚠️ DTMF not available');
            return false;
        }

        const frequencies = this.dtmfFrequencies[digit.toString().toUpperCase()];
        if (!frequencies) {
            console.log(`⚠️ Invalid DTMF digit: ${digit}`);
            return false;
        }

        try {
            // Ensure AudioContext is running
            if (this.audioContext.state === 'suspended') {
                await this.audioContext.resume();
            }

            // Create oscillators for the two DTMF frequencies
            const oscillator1 = this.audioContext.createOscillator();
            const oscillator2 = this.audioContext.createOscillator();
            const gainNode = this.audioContext.createGain();

            // Set frequencies
            oscillator1.frequency.setValueAtTime(frequencies[0], this.audioContext.currentTime);
            oscillator2.frequency.setValueAtTime(frequencies[1], this.audioContext.currentTime);

            // Set waveform (sine wave for clean tones)
            oscillator1.type = 'sine';
            oscillator2.type = 'sine';

            // Connect oscillators to gain node
            oscillator1.connect(gainNode);
            oscillator2.connect(gainNode);

            // Set volume
            gainNode.gain.setValueAtTime(this.volume, this.audioContext.currentTime);

            // Apply envelope (fade in/out to prevent clicks)
            const fadeTime = 0.01; // 10ms fade
            gainNode.gain.setValueAtTime(0, this.audioContext.currentTime);
            gainNode.gain.linearRampToValueAtTime(this.volume, this.audioContext.currentTime + fadeTime);
            gainNode.gain.linearRampToValueAtTime(this.volume, this.audioContext.currentTime + this.toneDuration / 1000 - fadeTime);
            gainNode.gain.linearRampToValueAtTime(0, this.audioContext.currentTime + this.toneDuration / 1000);

            // Connect to output
            gainNode.connect(this.audioContext.destination);

            // Start and stop oscillators
            const startTime = this.audioContext.currentTime;
            const stopTime = startTime + this.toneDuration / 1000;

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

    async playDialSequence(digits, delay = 100) {
        for (let i = 0; i < digits.length; i++) {
            await this.playDTMFTone(digits[i]);
            if (i < digits.length - 1) {
                await new Promise(resolve => setTimeout(resolve, delay));
            }
        }
    }

    setVolume(volume) {
        this.volume = Math.max(0, Math.min(1, volume));
        console.log(`🎵 DTMF volume set to ${Math.round(this.volume * 100)}%`);
    }

    setToneDuration(duration) {
        this.toneDuration = Math.max(50, Math.min(1000, duration));
        console.log(`🎵 DTMF tone duration set to ${this.toneDuration}ms`);
    }

    setEnabled(enabled) {
        this.enabled = enabled;
        console.log(`🎵 DTMF ${enabled ? 'enabled' : 'disabled'}`);
    }

    getStatus() {
        return {
            enabled: this.enabled,
            volume: this.volume,
            toneDuration: this.toneDuration,
            audioContextState: this.audioContext ? this.audioContext.state : 'not initialized',
            supportedDigits: Object.keys(this.dtmfFrequencies)
        };
    }

    // Test all DTMF tones
    async testAllTones() {
        console.log('🎵 Testing all DTMF tones...');
        const digits = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '*', '0', '#'];

        for (const digit of digits) {
            console.log(`Testing DTMF: ${digit}`);
            await this.playDTMFTone(digit);
            await new Promise(resolve => setTimeout(resolve, 300));
        }

        console.log('🎵 DTMF test completed');
    }
}

module.exports = DTMFService;