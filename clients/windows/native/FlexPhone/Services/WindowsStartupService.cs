using Microsoft.Win32;

namespace FlexPhone.Services
{
    public static class WindowsStartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Flex Phone";

        public static void Apply(bool enabled)
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (runKey is null)
            {
                return;
            }

            if (!enabled)
            {
                runKey.DeleteValue(ValueName, throwOnMissingValue: false);
                return;
            }

            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return;
            }

            runKey.SetValue(ValueName, Quote(executablePath), RegistryValueKind.String);
        }

        public static bool IsEnabled()
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = runKey?.GetValue(ValueName)?.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string Quote(string path) => $"\"{path}\"";
    }
}
