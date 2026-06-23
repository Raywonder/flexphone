using System.IO;
using System.Media;
using System.Windows;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

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

        public void PlayDtmfConfirmationTone(bool enabled, char digit, string? outputDevice = null)
        {
            if (!enabled || !TryDtmfFrequencies(digit, out var lowFrequency, out var highFrequency))
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    using var output = new WaveOutEvent
                    {
                        DeviceNumber = string.IsNullOrWhiteSpace(outputDevice) ||
                            outputDevice.Contains("Default communications speaker", StringComparison.OrdinalIgnoreCase)
                                ? WaveMapper
                                : DeviceNumberFor(outputDevice)
                    };
                    var tone = new DtmfToneSampleProvider(lowFrequency, highFrequency, TimeSpan.FromMilliseconds(95));
                    output.Init(tone);
                    output.Play();
                    while (output.PlaybackState == PlaybackState.Playing)
                    {
                        Thread.Sleep(8);
                    }
                }
                catch
                {
                    try { SystemSounds.Beep.Play(); } catch { }
                }
            });
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

        private static bool TryDtmfFrequencies(char digit, out double lowFrequency, out double highFrequency)
        {
            (lowFrequency, highFrequency) = char.ToUpperInvariant(digit) switch
            {
                '1' => (697, 1209),
                '2' => (697, 1336),
                '3' => (697, 1477),
                'A' => (697, 1633),
                '4' => (770, 1209),
                '5' => (770, 1336),
                '6' => (770, 1477),
                'B' => (770, 1633),
                '7' => (852, 1209),
                '8' => (852, 1336),
                '9' => (852, 1477),
                'C' => (852, 1633),
                '*' => (941, 1209),
                '0' => (941, 1336),
                '#' => (941, 1477),
                'D' => (941, 1633),
                _ => (0, 0)
            };
            return lowFrequency > 0 && highFrequency > 0;
        }

        private sealed class DtmfToneSampleProvider : ISampleProvider
        {
            private readonly double _lowStep;
            private readonly double _highStep;
            private readonly int _totalSamples;
            private int _sampleIndex;

            public DtmfToneSampleProvider(double lowFrequency, double highFrequency, TimeSpan duration)
            {
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
                _lowStep = 2 * Math.PI * lowFrequency / WaveFormat.SampleRate;
                _highStep = 2 * Math.PI * highFrequency / WaveFormat.SampleRate;
                _totalSamples = (int)(WaveFormat.SampleRate * duration.TotalSeconds);
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                var available = Math.Max(0, _totalSamples - _sampleIndex);
                var samples = Math.Min(count, available);
                for (var i = 0; i < samples; i++)
                {
                    var envelope = Envelope(_sampleIndex, _totalSamples);
                    buffer[offset + i] = (float)(((Math.Sin(_lowStep * _sampleIndex) + Math.Sin(_highStep * _sampleIndex)) * 0.14) * envelope);
                    _sampleIndex++;
                }

                return samples;
            }

            private static double Envelope(int sampleIndex, int totalSamples)
            {
                const int rampSamples = 220;
                if (sampleIndex < rampSamples)
                {
                    return sampleIndex / (double)rampSamples;
                }

                var remaining = totalSamples - sampleIndex;
                return remaining < rampSamples ? Math.Max(0, remaining / (double)rampSamples) : 1;
            }
        }

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
