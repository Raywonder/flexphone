using System.Media;
using System.Windows;

namespace FlexPhone.Services
{
    public sealed class FlexPhoneSoundService
    {
        private static readonly Dictionary<string, string> RingtoneFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Incoming call"] = "Ringtones/ringtone-incoming-call.wav",
            ["Incoming call alternate"] = "Ringtones/ringtone-incoming-call-alt.wav",
            ["Ring Ring Flitch"] = "Ringtones/ringtone-ring-ring-flitch.wav",
            ["Are you gonna answer"] = "Ringtones/ringtone-are-you-gonna-answer.wav"
        };

        public static IReadOnlyList<string> AvailableRingtones { get; } =
            RingtoneFiles.Keys.OrderBy(name => name).ToArray();

        public void PlayIncomingRing(bool enabled, string? ringtone = null)
        {
            if (!enabled)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(ringtone) &&
                RingtoneFiles.TryGetValue(ringtone.Trim(), out var selectedFile) &&
                PlayBundledSound(selectedFile, null))
            {
                return;
            }

            PlayBundledSound("incoming-ring.wav", SystemSounds.Asterisk);
        }

        public void PreviewRingtone(string? ringtone)
        {
            if (!string.IsNullOrWhiteSpace(ringtone) &&
                RingtoneFiles.TryGetValue(ringtone.Trim(), out var selectedFile) &&
                PlayBundledSound(selectedFile, null))
            {
                return;
            }

            PlayBundledSound("incoming-ring.wav", SystemSounds.Asterisk);
        }

        public void PlayCallConnected(bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            PlayBundledSound("call-connected.wav", SystemSounds.Beep);
        }

        public void PlayCallEnded(bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            PlayBundledSound("call-ended.wav", SystemSounds.Exclamation);
        }

        public void PlayQuickAlert(bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            PlayBundledSound("quick-alert.wav", SystemSounds.Beep);
        }

        public void PlayNetworkChange(bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            PlayBundledSound("network-change.wav", SystemSounds.Question);
        }

        private static bool PlayBundledSound(string fileName, SystemSound? fallback)
        {
            try
            {
                var resource = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Sounds/{fileName}", UriKind.Absolute));
                if (resource?.Stream is not null)
                {
                    using var player = new SoundPlayer(resource.Stream);
                    player.Load();
                    player.Play();
                    return true;
                }
            }
            catch
            {
                // Fall through to the Windows sound so alerts are never silent.
            }

            fallback?.Play();
            return false;
        }
    }
}
