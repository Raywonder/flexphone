/**
 * 👥 FlexPhone Contacts Service
 * Manages contacts for the SIP phone
 */

const EventEmitter = require('events');
const crypto = require('crypto');
const fs = require('fs').promises;
const path = require('path');

class ContactsService extends EventEmitter {
    constructor() {
        super();

        this.contacts = new Map();
        this.contactsPath = path.join(process.cwd(), 'data', 'contacts.json');

        console.log('👥 FlexPhone Contacts Service initialized');
    }

    async initialize() {
        try {
            await this.loadContacts();
            console.log('✅ Contacts Service ready');
            return true;
        } catch (error) {
            console.error('❌ Contacts Service initialization failed:', error);
            return false;
        }
    }

    async addContact(contactData) {
        try {
            const contactId = this.generateContactId();
            const contact = {
                id: contactId,
                firstName: contactData.firstName || '',
                lastName: contactData.lastName || '',
                displayName: contactData.displayName || `${contactData.firstName} ${contactData.lastName}`.trim(),
                phoneNumbers: contactData.phoneNumbers || [],
                emails: contactData.emails || [],
                organization: contactData.organization || '',
                notes: contactData.notes || '',
                avatar: contactData.avatar || null,
                favorite: contactData.favorite || false,
                blocked: contactData.blocked || false,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
                tags: contactData.tags || [],
                customFields: contactData.customFields || {}
            };

            this.contacts.set(contactId, contact);
            await this.saveContacts();

            console.log(`👤 Contact added: ${contact.displayName}`);
            this.emit('contact-added', contact);

            return {
                success: true,
                contactId: contactId,
                contact: contact
            };

        } catch (error) {
            console.error('❌ Add contact failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async updateContact(contactId, updates) {
        try {
            const contact = this.contacts.get(contactId);
            if (!contact) {
                throw new Error('Contact not found');
            }

            // Update fields
            Object.keys(updates).forEach(key => {
                if (key !== 'id' && key !== 'createdAt') {
                    contact[key] = updates[key];
                }
            });

            contact.updatedAt = new Date().toISOString();

            await this.saveContacts();

            console.log(`👤 Contact updated: ${contact.displayName}`);
            this.emit('contact-updated', contact);

            return {
                success: true,
                contact: contact
            };

        } catch (error) {
            console.error('❌ Update contact failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async deleteContact(contactId) {
        try {
            const contact = this.contacts.get(contactId);
            if (!contact) {
                throw new Error('Contact not found');
            }

            this.contacts.delete(contactId);
            await this.saveContacts();

            console.log(`🗑️ Contact deleted: ${contact.displayName}`);
            this.emit('contact-deleted', { contactId, contact });

            return {
                success: true,
                contactId: contactId
            };

        } catch (error) {
            console.error('❌ Delete contact failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    getAllContacts() {
        return Array.from(this.contacts.values())
            .sort((a, b) => a.displayName.localeCompare(b.displayName));
    }

    getContact(contactId) {
        return this.contacts.get(contactId);
    }

    searchContacts(query) {
        const searchTerm = query.toLowerCase();
        return Array.from(this.contacts.values()).filter(contact => {
            return (
                contact.displayName.toLowerCase().includes(searchTerm) ||
                contact.firstName.toLowerCase().includes(searchTerm) ||
                contact.lastName.toLowerCase().includes(searchTerm) ||
                contact.organization.toLowerCase().includes(searchTerm) ||
                contact.phoneNumbers.some(phone => phone.number.includes(searchTerm)) ||
                contact.emails.some(email => email.address.toLowerCase().includes(searchTerm))
            );
        });
    }

    findContactByPhoneNumber(phoneNumber) {
        const normalizedQuery = this.normalizePhoneNumber(phoneNumber);

        for (const contact of this.contacts.values()) {
            for (const phone of contact.phoneNumbers) {
                if (this.normalizePhoneNumber(phone.number) === normalizedQuery) {
                    return contact;
                }
            }
        }

        return null;
    }

    normalizePhoneNumber(phoneNumber) {
        // Remove all non-digit characters
        return phoneNumber.replace(/\D/g, '');
    }

    generateContactId() {
        return 'contact_' + crypto.randomBytes(8).toString('hex');
    }

    async loadContacts() {
        try {
            const data = await fs.readFile(this.contactsPath, 'utf8');
            const savedContacts = JSON.parse(data);

            this.contacts = new Map();
            savedContacts.forEach(contact => {
                this.contacts.set(contact.id, contact);
            });

            console.log(`📥 Loaded ${this.contacts.size} contacts`);

        } catch (error) {
            this.contacts = new Map();
            console.log('📝 Starting with empty contacts');
        }
    }

    async saveContacts() {
        try {
            const dataDir = path.dirname(this.contactsPath);

            try {
                await fs.access(dataDir);
            } catch {
                await fs.mkdir(dataDir, { recursive: true });
            }

            const contactsArray = Array.from(this.contacts.values());
            await fs.writeFile(this.contactsPath, JSON.stringify(contactsArray, null, 2));

        } catch (error) {
            console.error('❌ Failed to save contacts:', error);
        }
    }
}

module.exports = ContactsService;