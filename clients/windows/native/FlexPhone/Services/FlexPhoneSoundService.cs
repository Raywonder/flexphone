using System.Media;
using System.Windows;

namespace FlexPhone.Services
{
    public sealed class FlexPhoneSoundService
    {
        public void PlayIncomingRing(bool enabled)
        {
            if (!enabled)
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

        private static void PlayBundledSound(string fileName, SystemSound fallback)
        {
            try
            {
                var resource = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Sounds/{fileName}", UriKind.Absolute));
                if (resource?.Stream is not null)
                {
                    using var player = new SoundPlayer(resource.Stream);
                    player.Load();
                    player.Play();
                    return;
                }
            }
            catch
            {
                // Fall through to the Windows sound so alerts are never silent.
            }

            fallback.Play();
        }
    }
}
