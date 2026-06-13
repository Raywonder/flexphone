using System.IO;
using System.Media;
using System.Windows;
using NAudio.Wave;

namespace FlexPhone.Services
{
    public sealed class FlexPhoneSoundService
    {
        private static readonly object CacheSync = new();
        private readonly object _previewSync = new();
        private IWavePlayer? _previewOutput;
        private AudioFileReader? _previewReader;
        private static readonly Dictionary<string, string> RingtoneFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Incoming call"] = "Ringtones/ringtone-incoming-call.wav",
            ["Incoming call alternate"] = "Ringtones/ringtone-incoming-call-alt.wav",
            ["Ring Ring Flitch"] = "Ringtones/ringtone-ring-ring-flitch.wav",
            ["Are you gonna answer"] = "Ringtones/ringtone-are-you-gonna-answer.wav"
        };

        public static IReadOnlyList<string> AvailableRingtones { get; } =
            RingtoneFiles.Keys.OrderBy(name => name).ToArray();

        public void PlayIncomingRing(bool enabled, string? ringtone = null, string? outputDevice = null)
        {
            if (!enabled)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(ringtone) &&
                RingtoneFiles.TryGetValue(ringtone.Trim(), out var selectedFile) &&
                PlayBundledSound(selectedFile, null, outputDevice))
            {
                return;
            }

            PlayBundledSound("incoming-ring.wav", SystemSounds.Asterisk, outputDevice);
        }

        public void PreviewRingtone(string? ringtone, string? outputDevice = null)
        {
            if (!string.IsNullOrWhiteSpace(ringtone) &&
                RingtoneFiles.TryGetValue(ringtone.Trim(), out var selectedFile) &&
                PlayBundledSound(selectedFile, null, outputDevice, stopPrevious: true))
            {
                return;
            }

            PlayBundledSound("incoming-ring.wav", SystemSounds.Asterisk, outputDevice, stopPrevious: true);
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

        private bool PlayBundledSound(string fileName, SystemSound? fallback, string? outputDevice = null, bool stopPrevious = false)
        {
            try
            {
                var cachedPath = CachedSoundPath(fileName);
                if (!string.IsNullOrWhiteSpace(cachedPath))
                {
                    if (TryPlayWithNAudio(cachedPath, outputDevice, stopPrevious))
                    {
                        return true;
                    }

                    var player = new SoundPlayer(cachedPath);
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

        private bool TryPlayWithNAudio(string path, string? outputDevice, bool stopPrevious)
        {
            lock (_previewSync)
            {
                if (stopPrevious)
                {
                    StopPreviewLocked();
                }

                var deviceNumber = WaveMapper;
                if (!string.IsNullOrWhiteSpace(outputDevice) &&
                    !outputDevice.Contains("Default communications speaker", StringComparison.OrdinalIgnoreCase))
                {
                    deviceNumber = DeviceNumberFor(outputDevice);
                }

                var reader = new AudioFileReader(path);
                var output = new WaveOutEvent { DeviceNumber = deviceNumber };
                output.Init(reader);
                output.PlaybackStopped += (_, _) =>
                {
                    lock (_previewSync)
                    {
                        if (ReferenceEquals(_previewOutput, output))
                        {
                            _previewOutput = null;
                            _previewReader = null;
                        }
                    }
                    output.Dispose();
                    reader.Dispose();
                };
                _previewOutput = output;
                _previewReader = reader;
                output.Play();
                return true;
            }
        }

        private void StopPreviewLocked()
        {
            try { _previewOutput?.Stop(); } catch { }
            try { _previewOutput?.Dispose(); } catch { }
            try { _previewReader?.Dispose(); } catch { }
            _previewOutput = null;
            _previewReader = null;
        }

        private static int DeviceNumberFor(string outputDevice)
        {
            for (var index = 0; index < WaveOut.DeviceCount; index++)
            {
                try
                {
                    var caps = WaveOut.GetCapabilities(index);
                    if (outputDevice.Contains(caps.ProductName, StringComparison.OrdinalIgnoreCase) ||
                        caps.ProductName.Contains(outputDevice, StringComparison.OrdinalIgnoreCase))
                    {
                        return index;
                    }
                }
                catch
                {
                    // Skip devices that disappear while settings are open.
                }
            }

            return WaveMapper;
        }

        private const int WaveMapper = -1;

        private static string CachedSoundPath(string fileName)
        {
            lock (CacheSync)
            {
                var safeRelativePath = fileName.Replace('/', Path.DirectorySeparatorChar);
                var cachePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FlexPhone",
                    "SoundCache",
                    safeRelativePath);
                if (File.Exists(cachePath))
                {
                    return cachePath;
                }

                var resource = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Sounds/{fileName}", UriKind.Absolute));
                if (resource?.Stream is null)
                {
                    return "";
                }

                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                using var output = File.Create(cachePath);
                resource.Stream.CopyTo(output);
                return cachePath;
            }
        }
    }
}
