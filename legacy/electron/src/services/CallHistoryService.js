/**
 * 📞 FlexPhone Call History Service
 * Manages call logs and history
 */

const EventEmitter = require('events');
const fs = require('fs').promises;
const path = require('path');

class CallHistoryService extends EventEmitter {
    constructor() {
        super();

        this.callHistory = [];
        this.historyPath = path.join(process.cwd(), 'data', 'call-history.json');

        console.log('📞 FlexPhone Call History Service initialized');
    }

    async initialize() {
        try {
            await this.loadHistory();
            console.log('✅ Call History Service ready');
            return true;
        } catch (error) {
            console.error('❌ Call History Service initialization failed:', error);
            return false;
        }
    }

    async addCall(callData) {
        try {
            const call = {
                id: callData.id,
                direction: callData.direction, // 'inbound' or 'outbound'
                remoteNumber: callData.remoteNumber,
                remoteName: callData.remoteName || null,
                localNumber: callData.localNumber,
                status: callData.status, // 'completed', 'missed', 'busy', 'failed'
                startTime: callData.startTime,
                connectTime: callData.connectTime,
                endTime: callData.endTime,
                duration: callData.duration || 0,
                provider: callData.provider,
                recording: callData.recording || null,
                notes: callData.notes || '',
                tags: callData.tags || []
            };

            this.callHistory.unshift(call); // Add to beginning
            await this.saveHistory();

            console.log(`📞 Call logged: ${call.direction} ${call.remoteNumber} (${call.status})`);
            this.emit('call-logged', call);

            return {
                success: true,
                call: call
            };

        } catch (error) {
            console.error('❌ Add call to history failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    getCallHistory(limit = 50, filter = null) {
        let history = this.callHistory;

        if (filter) {
            if (filter.direction) {
                history = history.filter(call => call.direction === filter.direction);
            }
            if (filter.status) {
                history = history.filter(call => call.status === filter.status);
            }
            if (filter.dateFrom) {
                const fromDate = new Date(filter.dateFrom);
                history = history.filter(call => new Date(call.startTime) >= fromDate);
            }
            if (filter.dateTo) {
                const toDate = new Date(filter.dateTo);
                history = history.filter(call => new Date(call.startTime) <= toDate);
            }
        }

        return history.slice(0, limit);
    }

    getMissedCalls() {
        return this.callHistory.filter(call => call.status === 'missed');
    }

    getCallStatistics() {
        const stats = {
            total: this.callHistory.length,
            inbound: 0,
            outbound: 0,
            missed: 0,
            completed: 0,
            totalDuration: 0,
            averageDuration: 0,
            callsToday: 0,
            callsThisWeek: 0,
            callsThisMonth: 0
        };

        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        const weekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
        const monthAgo = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);

        this.callHistory.forEach(call => {
            const callTime = new Date(call.startTime);

            // Direction stats
            if (call.direction === 'inbound') {
                stats.inbound++;
            } else {
                stats.outbound++;
            }

            // Status stats
            if (call.status === 'missed') {
                stats.missed++;
            } else if (call.status === 'completed') {
                stats.completed++;
                stats.totalDuration += call.duration;
            }

            // Time-based stats
            if (callTime >= today) {
                stats.callsToday++;
            }
            if (callTime >= weekAgo) {
                stats.callsThisWeek++;
            }
            if (callTime >= monthAgo) {
                stats.callsThisMonth++;
            }
        });

        if (stats.completed > 0) {
            stats.averageDuration = stats.totalDuration / stats.completed;
        }

        return stats;
    }

    async clearHistory() {
        try {
            this.callHistory = [];
            await this.saveHistory();

            console.log('🗑️ Call history cleared');
            this.emit('history-cleared');

            return {
                success: true,
                message: 'Call history cleared'
            };

        } catch (error) {
            console.error('❌ Clear history failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async loadHistory() {
        try {
            const data = await fs.readFile(this.historyPath, 'utf8');
            this.callHistory = JSON.parse(data);

            console.log(`📥 Loaded ${this.callHistory.length} call records`);

        } catch (error) {
            this.callHistory = [];
            console.log('📝 Starting with empty call history');
        }
    }

    async saveHistory() {
        try {
            const dataDir = path.dirname(this.historyPath);

            try {
                await fs.access(dataDir);
            } catch {
                await fs.mkdir(dataDir, { recursive: true });
            }

            await fs.writeFile(this.historyPath, JSON.stringify(this.callHistory, null, 2));

        } catch (error) {
            console.error('❌ Failed to save call history:', error);
        }
    }
}

module.exports = CallHistoryService;