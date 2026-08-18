using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlexPhone.Services;

namespace FlexPhone.Views
{
    internal partial class TransferCallWindow : Window
    {
        private readonly string _currentServer;

        internal TransferCallWindow(string initialDestination, string currentServer, IEnumerable<string> knownServers, IEnumerable<FlexPhoneDeviceInfo> devices)
        {
            InitializeComponent();
            _currentServer = currentServer;
            DestinationBox.Text = initialDestination;
            foreach (var server in knownServers.Where(server => !string.IsNullOrWhiteSpace(server)))
            {
                ServerBox.Items.Add(server);
            }

            ServerBox.Text = currentServer;
            foreach (var device in devices.Where(device => device.CanReceiveNamedTransfer))
            {
                DeviceBox.Items.Add(device);
            }
            if (DeviceBox.Items.Count > 0)
            {
                TransferTypeBox.Items.Add(new ComboBoxItem { Content = "Another signed-in FlexPhone device", Tag = "device" });
            }
            Loaded += (_, _) =>
            {
                DestinationBox.Focus();
                DestinationBox.SelectAll();
                UpdateMode();
            };
            DestinationBox.TextChanged += (_, _) => UpdatePreview();
            ServerBox.SelectionChanged += (_, _) => UpdatePreview();
            ServerBox.KeyUp += (_, _) => UpdatePreview();
        }

        internal string TransferDestination { get; private set; } = "";
        internal string TransferDeviceId { get; private set; } = "";

        private string TransferMode => (TransferTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "internal";

        private void TransferTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateMode();
        }

        private void UpdateMode()
        {
            var mode = TransferMode;
            ServerLabel.Visibility = mode == "pbx" ? Visibility.Visible : Visibility.Collapsed;
            ServerBox.Visibility = mode == "pbx" ? Visibility.Visible : Visibility.Collapsed;
            DestinationLabel.Text = mode switch
            {
                "external" => "Phone number",
                "pbx" => "Extension, number, or SIP user",
                "device" => "Signed-in device",
                _ => "Extension"
            };
            DeviceLabel.Visibility = mode == "device" ? Visibility.Visible : Visibility.Collapsed;
            DeviceBox.Visibility = mode == "device" ? Visibility.Visible : Visibility.Collapsed;
            DestinationBox.Visibility = mode == "device" ? Visibility.Collapsed : Visibility.Visible;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var destination = BuildDestination();
            if (TransferMode == "device")
            {
                var device = DeviceBox.SelectedItem as FlexPhoneDeviceInfo;
                PreviewText.Text = device is null
                    ? "Choose an online signed-in FlexPhone device."
                    : $"Transfer target: {device.AccessibleSummary}";
                return;
            }
            PreviewText.Text = string.IsNullOrWhiteSpace(destination)
                ? "Enter a transfer destination."
                : $"Transfer target: {destination}";
        }

        private string BuildDestination()
        {
            var value = DestinationBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            if (TransferMode != "pbx")
            {
                return value;
            }

            if (value.Contains("@", StringComparison.Ordinal))
            {
                return value;
            }

            var server = ServerBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(server))
            {
                server = _currentServer;
            }

            return $"{value}@{server}";
        }

        private void TransferButton_Click(object sender, RoutedEventArgs e)
        {
            var destination = BuildDestination();
            if (TransferMode == "device")
            {
                if (DeviceBox.SelectedItem is not FlexPhoneDeviceInfo device)
                {
                    PreviewText.Text = "Choose an online signed-in FlexPhone device before continuing.";
                    DeviceBox.Focus();
                    return;
                }

                TransferDeviceId = device.DeviceId;
                DialogResult = true;
                Close();
                return;
            }
            if (string.IsNullOrWhiteSpace(destination))
            {
                PreviewText.Text = "Enter a transfer destination before continuing.";
                DestinationBox.Focus();
                return;
            }

            TransferDestination = destination;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
                Close();
            }
        }
    }
}
