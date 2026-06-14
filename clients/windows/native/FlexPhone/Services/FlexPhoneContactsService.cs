using System.IO;
using System.Text.Json;
using FlexPhone.Models;

namespace FlexPhone.Services
{
    public sealed class FlexPhoneContactsService
    {
        private readonly string _contactsPath;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public FlexPhoneContactsService()
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DevineCreations",
                "FlexPhone");
            Directory.CreateDirectory(root);
            _contactsPath = Path.Combine(root, "contacts.json");
        }

        public List<FlexPhoneContact> Load()
        {
            try
            {
                if (!File.Exists(_contactsPath))
                {
                    return [];
                }

                var contacts = JsonSerializer.Deserialize<List<FlexPhoneContact>>(File.ReadAllText(_contactsPath), _jsonOptions);
                return Normalize(contacts ?? []);
            }
            catch
            {
                return [];
            }
        }

        public void Save(IEnumerable<FlexPhoneContact> contacts)
        {
            var normalized = Normalize(contacts.ToList());
            File.WriteAllText(_contactsPath, JsonSerializer.Serialize(normalized, _jsonOptions));
        }

        private static List<FlexPhoneContact> Normalize(List<FlexPhoneContact> contacts)
        {
            foreach (var contact in contacts)
            {
                if (string.IsNullOrWhiteSpace(contact.Id))
                {
                    contact.Id = Guid.NewGuid().ToString("N");
                }

                contact.DisplayName = contact.DisplayName.Trim();
                contact.Extension = contact.Extension.Trim();
                contact.PhoneNumber = contact.PhoneNumber.Trim();
                contact.Email = contact.Email.Trim();
                contact.Group = string.IsNullOrWhiteSpace(contact.Group) ? "General" : contact.Group.Trim();
                contact.Notes = contact.Notes.Trim();
            }

            return contacts
                .Where(contact => !string.IsNullOrWhiteSpace(contact.DisplayName)
                    || !string.IsNullOrWhiteSpace(contact.Extension)
                    || !string.IsNullOrWhiteSpace(contact.PhoneNumber)
                    || !string.IsNullOrWhiteSpace(contact.Email))
                .OrderByDescending(contact => contact.IsFavorite)
                .ThenBy(contact => contact.Group)
                .ThenBy(contact => contact.DisplayName)
                .ToList();
        }
    }
}
