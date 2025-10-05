/**
 * 💬 FlexPhone SMS Service
 * Handles SMS messaging through SIP providers
 */

const EventEmitter = require('events');
const crypto = require('crypto');
const fs = require('fs').promises;
const path = require('path');

class SMSService extends EventEmitter {
    constructor() {
        super();

        this.conversations = new Map();
        this.messages = new Map();
        this.messagesPath = path.join(process.cwd(), 'data', 'messages.json');

        // SMS provider capabilities
        this.smsProviders = {
            FLEXPBX: {
                supportsInbound: true,
                supportsOutbound: true,
                maxLength: 1600, // Long SMS support
                features: ['delivery_receipts', 'read_receipts', 'typing_indicators']
            },
            CALLCENTRIC: {
                supportsInbound: true,
                supportsOutbound: true,
                maxLength: 160,
                features: ['delivery_receipts']
            },
            VOIPMS: {
                supportsInbound: true,
                supportsOutbound: true,
                maxLength: 160,
                features: ['delivery_receipts']
            },
            TWILIO: {
                supportsInbound: true,
                supportsOutbound: true,
                maxLength: 1600,
                features: ['delivery_receipts', 'read_receipts', 'media_messages']
            },
            GOOGLE_VOICE: {
                supportsInbound: true,
                supportsOutbound: true,
                maxLength: 1600,
                features: ['delivery_receipts', 'read_receipts', 'typing_indicators', 'media_messages']
            }
        };

        console.log('💬 FlexPhone SMS Service initialized');
    }

    async initialize() {
        try {
            await this.loadMessages();
            console.log('✅ SMS Service ready');
            return true;
        } catch (error) {
            console.error('❌ SMS Service initialization failed:', error);
            return false;
        }
    }

    async sendSMS(to, message, options = {}) {
        try {
            const messageId = this.generateMessageId();
            const timestamp = new Date();

            // Get current SIP provider (simulated)
            const provider = options.provider || 'FLEXPBX';
            const providerConfig = this.smsProviders[provider];

            if (!providerConfig || !providerConfig.supportsOutbound) {
                throw new Error(`Provider ${provider} does not support outbound SMS`);
            }

            // Validate message length
            if (message.length > providerConfig.maxLength) {
                throw new Error(`Message too long (max ${providerConfig.maxLength} characters)`);
            }

            const smsMessage = {
                id: messageId,
                conversationId: this.getConversationId(to),
                direction: 'outbound',
                from: options.from || 'Your Number',
                to: to,
                message: message,
                timestamp: timestamp.toISOString(),
                status: 'sending',
                provider: provider,
                type: 'text',
                metadata: {
                    length: message.length,
                    encoding: this.detectEncoding(message),
                    parts: Math.ceil(message.length / 160)
                }
            };

            // Store message
            this.messages.set(messageId, smsMessage);
            await this.updateConversation(to, smsMessage);
            await this.saveMessages();

            console.log(`💬 Sending SMS: ${options.from || 'You'} → ${to}`);
            console.log(`   Message: "${message.substring(0, 50)}${message.length > 50 ? '...' : ''}"`);

            // Simulate SMS sending
            await this.simulateSMSSending(smsMessage);

            this.emit('message-sent', smsMessage);

            return {
                success: true,
                messageId: messageId,
                message: 'SMS sent successfully'
            };

        } catch (error) {
            console.error('❌ SMS send failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async receiveSMS(from, message, options = {}) {
        try {
            const messageId = this.generateMessageId();
            const timestamp = new Date();

            const smsMessage = {
                id: messageId,
                conversationId: this.getConversationId(from),
                direction: 'inbound',
                from: from,
                to: options.to || 'Your Number',
                message: message,
                timestamp: timestamp.toISOString(),
                status: 'received',
                provider: options.provider || 'FLEXPBX',
                type: 'text',
                read: false,
                metadata: {
                    length: message.length,
                    encoding: this.detectEncoding(message)
                }
            };

            // Store message
            this.messages.set(messageId, smsMessage);
            await this.updateConversation(from, smsMessage);
            await this.saveMessages();

            console.log(`💬 SMS received from: ${from}`);
            console.log(`   Message: "${message.substring(0, 50)}${message.length > 50 ? '...' : ''}"`);

            this.emit('message-received', smsMessage);

            return {
                success: true,
                messageId: messageId
            };

        } catch (error) {
            console.error('❌ SMS receive failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async simulateSMSSending(message) {
        // Simulate network delay
        await new Promise(resolve => setTimeout(resolve, 1000));

        // Update status to sent
        message.status = 'sent';
        message.sentAt = new Date().toISOString();

        console.log(`✅ SMS sent: ${message.id}`);

        // Simulate delivery receipt
        setTimeout(() => {
            message.status = 'delivered';
            message.deliveredAt = new Date().toISOString();
            console.log(`📬 SMS delivered: ${message.id}`);
            this.emit('message-delivered', message);
        }, 2000);
    }

    async markAsRead(messageId) {
        try {
            const message = this.messages.get(messageId);
            if (!message) {
                throw new Error('Message not found');
            }

            if (message.direction === 'inbound' && !message.read) {
                message.read = true;
                message.readAt = new Date().toISOString();

                await this.saveMessages();

                console.log(`👁️ Message marked as read: ${messageId}`);

                this.emit('message-read', message);

                return {
                    success: true,
                    messageId: messageId
                };
            }

            return {
                success: true,
                messageId: messageId,
                message: 'Already read or outbound message'
            };

        } catch (error) {
            console.error('❌ Mark as read failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async deleteMessage(messageId) {
        try {
            const message = this.messages.get(messageId);
            if (!message) {
                throw new Error('Message not found');
            }

            this.messages.delete(messageId);

            // Update conversation
            const conversation = this.conversations.get(message.conversationId);
            if (conversation) {
                conversation.messageCount = Math.max(0, conversation.messageCount - 1);
                if (conversation.lastMessageId === messageId) {
                    // Find new last message
                    const conversationMessages = this.getMessagesForConversation(message.conversationId);
                    if (conversationMessages.length > 0) {
                        const lastMessage = conversationMessages[conversationMessages.length - 1];
                        conversation.lastMessageId = lastMessage.id;
                        conversation.lastMessage = lastMessage.message;
                        conversation.lastMessageTime = lastMessage.timestamp;
                    } else {
                        conversation.lastMessageId = null;
                        conversation.lastMessage = null;
                        conversation.lastMessageTime = null;
                    }
                }
            }

            await this.saveMessages();

            console.log(`🗑️ Message deleted: ${messageId}`);

            this.emit('message-deleted', { messageId, conversationId: message.conversationId });

            return {
                success: true,
                messageId: messageId
            };

        } catch (error) {
            console.error('❌ Delete message failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async deleteConversation(conversationId) {
        try {
            const conversation = this.conversations.get(conversationId);
            if (!conversation) {
                throw new Error('Conversation not found');
            }

            // Delete all messages in conversation
            const conversationMessages = this.getMessagesForConversation(conversationId);
            for (const message of conversationMessages) {
                this.messages.delete(message.id);
            }

            this.conversations.delete(conversationId);

            await this.saveMessages();

            console.log(`🗑️ Conversation deleted: ${conversationId}`);

            this.emit('conversation-deleted', conversationId);

            return {
                success: true,
                conversationId: conversationId
            };

        } catch (error) {
            console.error('❌ Delete conversation failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    getConversationId(phoneNumber) {
        // Normalize phone number for consistent conversation IDs
        const normalized = phoneNumber.replace(/[^\d]/g, '');
        return `conv_${crypto.createHash('md5').update(normalized).digest('hex').substring(0, 16)}`;
    }

    async updateConversation(phoneNumber, message) {
        const conversationId = this.getConversationId(phoneNumber);
        let conversation = this.conversations.get(conversationId);

        if (!conversation) {
            conversation = {
                id: conversationId,
                phoneNumber: phoneNumber,
                displayName: phoneNumber, // Could be enriched with contact name
                messageCount: 0,
                unreadCount: 0,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
                lastMessageId: null,
                lastMessage: null,
                lastMessageTime: null,
                muted: false,
                archived: false
            };
        }

        conversation.messageCount += 1;
        conversation.updatedAt = new Date().toISOString();
        conversation.lastMessageId = message.id;
        conversation.lastMessage = message.message;
        conversation.lastMessageTime = message.timestamp;

        if (message.direction === 'inbound' && !message.read) {
            conversation.unreadCount += 1;
        }

        this.conversations.set(conversationId, conversation);

        return conversation;
    }

    getConversations() {
        return Array.from(this.conversations.values())
            .sort((a, b) => new Date(b.lastMessageTime) - new Date(a.lastMessageTime));
    }

    getMessages(conversationId, limit = 50) {
        const messages = this.getMessagesForConversation(conversationId);
        return messages
            .sort((a, b) => new Date(b.timestamp) - new Date(a.timestamp))
            .slice(0, limit);
    }

    getMessagesForConversation(conversationId) {
        return Array.from(this.messages.values())
            .filter(msg => msg.conversationId === conversationId);
    }

    searchMessages(query, conversationId = null) {
        let messages = Array.from(this.messages.values());

        if (conversationId) {
            messages = messages.filter(msg => msg.conversationId === conversationId);
        }

        return messages.filter(msg =>
            msg.message.toLowerCase().includes(query.toLowerCase()) ||
            msg.from.toLowerCase().includes(query.toLowerCase()) ||
            msg.to.toLowerCase().includes(query.toLowerCase())
        );
    }

    detectEncoding(message) {
        // Simple encoding detection
        if (/[^\x00-\x7F]/.test(message)) {
            return 'UTF-8';
        }
        return 'ASCII';
    }

    generateMessageId() {
        return 'msg_' + crypto.randomBytes(8).toString('hex');
    }

    async loadMessages() {
        try {
            const data = await fs.readFile(this.messagesPath, 'utf8');
            const savedData = JSON.parse(data);

            // Restore messages
            this.messages = new Map();
            if (savedData.messages) {
                savedData.messages.forEach(msg => {
                    this.messages.set(msg.id, msg);
                });
            }

            // Restore conversations
            this.conversations = new Map();
            if (savedData.conversations) {
                savedData.conversations.forEach(conv => {
                    this.conversations.set(conv.id, conv);
                });
            }

            console.log(`📥 Loaded ${this.messages.size} messages in ${this.conversations.size} conversations`);

        } catch (error) {
            // File doesn't exist or is corrupted, start fresh
            this.messages = new Map();
            this.conversations = new Map();
            console.log('📝 Starting with empty message history');
        }
    }

    async saveMessages() {
        try {
            const dataDir = path.dirname(this.messagesPath);

            // Ensure directory exists
            try {
                await fs.access(dataDir);
            } catch {
                await fs.mkdir(dataDir, { recursive: true });
            }

            const saveData = {
                messages: Array.from(this.messages.values()),
                conversations: Array.from(this.conversations.values()),
                exportDate: new Date().toISOString(),
                version: '1.0.0'
            };

            await fs.writeFile(this.messagesPath, JSON.stringify(saveData, null, 2));

        } catch (error) {
            console.error('❌ Failed to save messages:', error);
        }
    }

    getStatistics() {
        const stats = {
            totalMessages: this.messages.size,
            totalConversations: this.conversations.size,
            unreadMessages: 0,
            sentMessages: 0,
            receivedMessages: 0,
            messagesLast24h: 0,
            messagesThisWeek: 0
        };

        const now = new Date();
        const yesterday = new Date(now.getTime() - 24 * 60 * 60 * 1000);
        const weekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);

        for (const message of this.messages.values()) {
            const messageTime = new Date(message.timestamp);

            if (message.direction === 'inbound' && !message.read) {
                stats.unreadMessages++;
            }

            if (message.direction === 'outbound') {
                stats.sentMessages++;
            } else {
                stats.receivedMessages++;
            }

            if (messageTime > yesterday) {
                stats.messagesLast24h++;
            }

            if (messageTime > weekAgo) {
                stats.messagesThisWeek++;
            }
        }

        return stats;
    }

    getSupportedProviders() {
        return Object.entries(this.smsProviders).map(([key, provider]) => ({
            id: key,
            supportsInbound: provider.supportsInbound,
            supportsOutbound: provider.supportsOutbound,
            maxLength: provider.maxLength,
            features: provider.features
        }));
    }

    // Test methods for development
    async testReceiveMessage() {
        const testMessages = [
            'Hello from FlexPhone!',
            'This is a test SMS message.',
            'How are you doing today? 😊',
            'Testing emoji support: 🚀📱💬'
        ];

        const randomMessage = testMessages[Math.floor(Math.random() * testMessages.length)];
        return await this.receiveSMS('+1 (336) 462-6141', randomMessage, {
            provider: 'FLEXPBX'
        });
    }

    async testSendMessage() {
        return await this.sendSMS('+1 (281) 301-5784', 'Test message from FlexPhone!', {
            provider: 'FLEXPBX'
        });
    }
}

module.exports = SMSService;