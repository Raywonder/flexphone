using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlexPhone.Views
{
    internal partial class TransferCallWindow : Window
    {
        private readonly string _currentServer;

        internal TransferCallWindow(string initialDestination, string currentServer, IEnumerable<string> knownServers)
        {
            InitializeComponent();
            _currentServer = currentServer;
            DestinationBox.Text = initialDestination;
            foreach (var server in knownServers.Where(server => !string.IsNullOrWhiteSpace(server)))
            {
                ServerBox.Items.Add(server);
            }

            ServerBox.Text = currentServer;
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
                _ => "Extension"
            };
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var destination = BuildDestination();
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
