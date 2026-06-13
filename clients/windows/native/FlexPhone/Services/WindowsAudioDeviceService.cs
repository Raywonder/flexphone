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
                using var enumerator = new MMDeviceEnumerator();
                foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
                {
                    var name = device.FriendlyName?.Trim();
                    if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        names.Add(name);
                    }
                }
            }
            catch
            {
                // Keep the default device choice available on systems where endpoint enumeration is blocked.
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
