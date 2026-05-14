using System.Windows;
using FlexPhone.Models;
using FlexPhone.Services;
using MessageBox = System.Windows.MessageBox;

namespace FlexPhone.Views
{
    public partial class PairingWindow : Window
    {
        private readonly PbxAccountSession _account;
        private readonly FlexPbxClient _pbxClient;

        public PairingWindow(PbxAccountSession account, FlexPbxClient pbxClient)
        {
            InitializeComponent();
            _account = account;
            _pbxClient = pbxClient;
            Loaded += async (_, _) => await RefreshPairingCodeAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshPairingCodeAsync();
        }

        private async void CheckCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var code = PairingCodeBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show("Refresh the pairing code first.", "Flex Phone - Pairing", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var result = await _pbxClient.ValidatePairingCodeAsync(_account.Server, _account.Extension, _account.SessionToken, code);
                StatusText.Text = FirstText(result.Message, result.Error, result.Success ? "Pairing code is valid." : "Pairing code could not be verified.");
                _account.IsPaired = result.Success || _account.IsPaired;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Pairing code check failed: {ex.Message}";
            }
        }

        private void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var code = PairingCodeBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            System.Windows.Clipboard.SetText(code);
            StatusText.Text = "Pairing code copied.";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async Task RefreshPairingCodeAsync()
        {
            try
            {
                var result = await _pbxClient.CreatePairingCodeAsync(_account.Server, _account.Extension, _account.SessionToken);
                if (!result.Success)
                {
                    StatusText.Text = FirstText(result.Error, result.Message, "Flex PBX could not create a pairing code.");
                    return;
                }

                PairingCodeBox.Text = result.PairingCode;
                PairingUrlBox.Text = result.PairingUrl;
                StatusText.Text = "Pairing code ready. Copy it to link another trusted device to this extension.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Pairing failed: {ex.Message}";
            }
        }

        private static string FirstText(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }
    }
}
