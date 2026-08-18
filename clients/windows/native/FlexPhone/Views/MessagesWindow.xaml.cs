using System.Collections.ObjectModel;
using System.Windows;
using FlexPhone.Models;
using FlexPhone.Services;
using MessageBox = System.Windows.MessageBox;

namespace FlexPhone.Views
{
    public partial class MessagesWindow : Window
    {
        private readonly PbxAccountSession _account;
        private readonly FlexPbxClient _pbxClient;
        private readonly ObservableCollection<FlexPhoneMessageInfo> _messages = [];

        public MessagesWindow(PbxAccountSession account, FlexPbxClient pbxClient)
        {
            InitializeComponent();
            _account = account;
            _pbxClient = pbxClient;
            MessagesList.ItemsSource = _messages;
            StatusText.Text = $"Messages for {_account.Extension}";
            Loaded += async (_, _) => await RefreshMessagesAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshMessagesAsync();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var to = ToBox.Text.Trim();
            var body = BodyBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(body))
            {
                MessageBox.Show("Enter an extension and message before sending.", "Flex Phone - Messages", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var result = await _pbxClient.SendMessageAsync(
                    _account.Server,
                    _account.Extension,
                    _account.SessionToken,
                    to,
                    body);
                if (!result.Success)
                {
                    MessageBox.Show(FirstText(result.Error, result.Message, "Flex PBX could not send that message."), "Flex Phone - Messages", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                BodyBox.Text = "";
                StatusText.Text = FirstText(result.Message, "Message sent.");
                await RefreshMessagesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Flex Phone - Messages", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async Task RefreshMessagesAsync()
        {
            try
            {
                var result = await _pbxClient.GetMessagesAsync(
                    _account.Server,
                    _account.Extension,
                    _account.SessionToken);
                if (!result.Success)
                {
                    StatusText.Text = FirstText(result.Error, result.Message, "Messages are not available for this extension.");
                    return;
                }

                _messages.Clear();
                foreach (var message in result.Messages)
                {
                    _messages.Add(message);
                }

                StatusText.Text = _messages.Count == 0
                    ? "No messages."
                    : $"{_messages.Count} message{(_messages.Count == 1 ? "" : "s")}.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Messages failed: {ex.Message}";
            }
        }

        private static string FirstText(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }
    }
}
