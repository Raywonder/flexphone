using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace FlexPhone.Services
{
    public static class WindowsAudioDeviceService
    {
        public static IReadOnlyList<string> CaptureDevices()
        {
            return Devices(DataFlow.Capture, "Default communications microphone");
        }

        public static IReadOnlyList<string> RenderDevices()
        {
            return Devices(DataFlow.Render, "Default communications speaker");
        }

        public static IReadOnlyList<string> HeadsetDevices()
        {
            return Devices(DataFlow.Render, "Default headset");
        }

        public static int CaptureDeviceIndex(string? deviceName)
        {
            if (IsDefaultChoice(deviceName))
            {
                return -1;
            }

            return WaveInDeviceIndex(deviceName);
        }

        public static int RenderDeviceIndex(string? deviceName)
        {
            if (IsDefaultChoice(deviceName))
            {
                return -1;
            }

            return WaveOutDeviceIndex(deviceName);
        }

        private static IReadOnlyList<string> Devices(DataFlow flow, string defaultName)
        {
            var names = new List<string> { defaultName };
            try
            {
                // Use the same WaveIn/WaveOut names that the media endpoint uses.
                // MMDevice friendly names and NAudio Wave names are not guaranteed
                // to be identical, which previously made a selected device fall
                // back to the default endpoint.
                if (flow == DataFlow.Capture)
                {
                    for (var index = 0; index < WaveInEvent.DeviceCount; index++)
                    {
                        try
                        {
                            var name = WaveInEvent.GetCapabilities(index).ProductName?.Trim();
                            if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                            {
                                names.Add(name);
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    for (var index = 0; index < WaveOut.DeviceCount; index++)
                    {
                        try
                        {
                            var name = WaveOut.GetCapabilities(index).ProductName?.Trim();
                            if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                            {
                                names.Add(name);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch
            {
                // Keep the default device choice available on systems where enumeration is blocked.
            }

            return names;
        }

        private static bool IsDefaultChoice(string? deviceName)
        {
            return string.IsNullOrWhiteSpace(deviceName)
                || deviceName.StartsWith("Default ", StringComparison.OrdinalIgnoreCase);
        }

        private static int WaveInDeviceIndex(string? deviceName)
        {
            var wanted = NormalizeDeviceName(deviceName);
            for (var index = 0; index < WaveInEvent.DeviceCount; index++)
            {
                try
                {
                    var caps = WaveInEvent.GetCapabilities(index);
                    if (DeviceNamesMatch(wanted, caps.ProductName))
                    {
                        return index;
                    }
                }
                catch
                {
                    // Ignore inaccessible devices and keep looking.
                }
            }

            return -1;
        }

        private static int WaveOutDeviceIndex(string? deviceName)
        {
            var wanted = NormalizeDeviceName(deviceName);
            for (var index = 0; index < WaveOut.DeviceCount; index++)
            {
                try
                {
                    var caps = WaveOut.GetCapabilities(index);
                    if (DeviceNamesMatch(wanted, caps.ProductName))
                    {
                        return index;
                    }
                }
                catch
                {
                    // Ignore inaccessible devices and keep looking.
                }
            }

            return -1;
        }

        private static bool DeviceNamesMatch(string wanted, string? candidate)
        {
            var actual = NormalizeDeviceName(candidate);
            return actual.Length > 0
                && (wanted.Contains(actual, StringComparison.OrdinalIgnoreCase)
                    || actual.Contains(wanted, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeDeviceName(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
