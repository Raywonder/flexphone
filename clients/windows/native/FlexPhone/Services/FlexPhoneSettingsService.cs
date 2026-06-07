using System.IO;
using System.Text.Json;
using FlexPhone.Models;

namespace FlexPhone.Services
{
    public sealed class FlexPhoneSettingsService
    {
        private readonly string _settingsPath;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public FlexPhoneSettingsService()
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DevineCreations",
                "FlexPhone");
            Directory.CreateDirectory(root);
            _settingsPath = Path.Combine(root, "settings.json");
        }

        public FlexPhoneSettings Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var settings = JsonSerializer.Deserialize<FlexPhoneSettings>(File.ReadAllText(_settingsPath));
                    if (settings is not null)
                    {
                        return Normalize(settings);
                    }
                }
            }
            catch
            {
                // Fall back to safe defaults when settings are unreadable.
            }

            return new FlexPhoneSettings();
        }

        public void Save(FlexPhoneSettings settings)
        {
            var normalized = Normalize(settings);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(normalized, _jsonOptions));
        }

        private static FlexPhoneSettings Normalize(FlexPhoneSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.DefaultPbxServer))
            {
                settings.DefaultPbxServer = "pbx.tappedin.fm";
            }
            else if (settings.DefaultPbxServer.Equals("flexpbx.devinecreations.net", StringComparison.OrdinalIgnoreCase))
            {
                settings.DefaultPbxServer = "pbx.devinecreations.net";
            }

            if (string.IsNullOrWhiteSpace(settings.DefaultTurnServer))
            {
                settings.DefaultTurnServer = "turn.tappedin.fm";
            }

            if (string.IsNullOrWhiteSpace(settings.UserStatus))
            {
                settings.UserStatus = "Available";
            }

            if (string.IsNullOrWhiteSpace(settings.ProviderType))
            {
                settings.ProviderType = "Flex PBX";
            }

            if (string.IsNullOrWhiteSpace(settings.UpdateManifestPath))
            {
                settings.UpdateManifestPath = "/downloads/flexphone/flexphone-update.json";
            }

            if (string.IsNullOrWhiteSpace(settings.QueueToggleCode))
            {
                settings.QueueToggleCode = "*45";
            }

            if (string.IsNullOrWhiteSpace(settings.QueueLoginCode))
            {
                settings.QueueLoginCode = settings.QueueToggleCode;
            }

            if (string.IsNullOrWhiteSpace(settings.QueueLogoutCode))
            {
                settings.QueueLogoutCode = "*46";
            }

            if (string.Equals(settings.QueueLogoutCode, settings.QueueLoginCode, StringComparison.OrdinalIgnoreCase))
            {
                settings.QueueUsesSingleToggleCode = true;
            }

            if (string.IsNullOrWhiteSpace(settings.VoicemailCode))
            {
                settings.VoicemailCode = "*97";
            }

            if (string.IsNullOrWhiteSpace(settings.DndToggleCode))
            {
                settings.DndToggleCode = "*76";
            }

            if (string.IsNullOrWhiteSpace(settings.CallScreeningToggleCode))
            {
                settings.CallScreeningToggleCode = "*56";
            }

            if (string.IsNullOrWhiteSpace(settings.AnswerHotKey))
            {
                settings.AnswerHotKey = "Ctrl+Shift+A";
            }

            if (string.IsNullOrWhiteSpace(settings.HangupHotKey))
            {
                settings.HangupHotKey = "Ctrl+Shift+H";
            }

            if (string.IsNullOrWhiteSpace(settings.HoldHotKey))
            {
                settings.HoldHotKey = "Ctrl+Shift+O";
            }

            return settings;
        }
    }
}
