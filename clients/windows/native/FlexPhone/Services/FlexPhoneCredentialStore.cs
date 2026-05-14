using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlexPhone.Models;

namespace FlexPhone.Services
{
    public sealed class FlexPhoneCredentialStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly string _path;

        public FlexPhoneCredentialStore()
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DevineCreations",
                "FlexPhone");
            _path = Path.Combine(root, "remembered-sign-in.json");
        }

        public bool Exists => File.Exists(_path);

        public void Save(RememberedFlexPhoneAccount account)
        {
            var accounts = LoadAll()
                .Where(item => !SameAccount(item, account))
                .ToList();
            accounts.Add(account);
            SaveAll(accounts);
        }

        public void SaveAll(IEnumerable<RememberedFlexPhoneAccount> accounts)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(accounts, JsonOptions);
            var protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(json),
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);

            var wrapper = JsonSerializer.Serialize(new StoredRememberedAccount
            {
                Version = 1,
                ProtectedPayload = Convert.ToBase64String(protectedBytes)
            }, JsonOptions);

            File.WriteAllText(_path, wrapper, Encoding.UTF8);
        }

        public RememberedFlexPhoneAccount? Load()
        {
            return LoadAll().FirstOrDefault();
        }

        public IReadOnlyList<RememberedFlexPhoneAccount> LoadAll()
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            var wrapper = JsonSerializer.Deserialize<StoredRememberedAccount>(File.ReadAllText(_path, Encoding.UTF8));
            if (wrapper is null || string.IsNullOrWhiteSpace(wrapper.ProtectedPayload))
            {
                return [];
            }

            var protectedBytes = Convert.FromBase64String(wrapper.ProtectedPayload);
            var clearBytes = ProtectedData.Unprotect(
                protectedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(clearBytes);
            try
            {
                if (JsonSerializer.Deserialize<List<RememberedFlexPhoneAccount>>(json) is { } accounts)
                {
                    return accounts;
                }
            }
            catch (JsonException)
            {
                // Older builds stored one remembered account instead of an account list.
            }

            var legacy = JsonSerializer.Deserialize<RememberedFlexPhoneAccount>(json);
            return legacy is null ? [] : [legacy];
        }

        public void Delete()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }

        private sealed class StoredRememberedAccount
        {
            public int Version { get; set; }
            public string ProtectedPayload { get; set; } = "";
        }

        private static bool SameAccount(RememberedFlexPhoneAccount left, RememberedFlexPhoneAccount right) =>
            left.Extension.Equals(right.Extension, StringComparison.OrdinalIgnoreCase)
            && left.Server.Equals(right.Server, StringComparison.OrdinalIgnoreCase);
    }
}
