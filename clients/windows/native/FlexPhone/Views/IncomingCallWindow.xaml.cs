using System.Windows;
using System.Windows.Input;

namespace FlexPhone.Views
{
    public partial class IncomingCallWindow : Window
    {
        private readonly Func<bool> _hasIncomingCall;
        private readonly Func<Task> _answer;
        private readonly Func<Task> _decline;
        private readonly Func<Task> _sendToVoicemail;
        private readonly Func<string, Task> _transfer;
        private string _accountName;
        private string _caller;

        public IncomingCallWindow(
            string accountName,
            string caller,
            Func<bool> hasIncomingCall,
            Func<Task> answer,
            Func<Task> decline,
            Func<Task> sendToVoicemail,
            Func<string, Task> transfer)
        {
            InitializeComponent();
            _accountName = accountName;
            _caller = caller;
            _hasIncomingCall = hasIncomingCall;
            _answer = answer;
            _decline = decline;
            _sendToVoicemail = sendToVoicemail;
            _transfer = transfer;
            UpdateText();
            Loaded += (_, _) => AnswerButton.Focus();
            PreviewKeyDown += IncomingCallWindow_PreviewKeyDown;
        }

        public void UpdateCall(string accountName, string caller, Func<bool> hasIncomingCall)
        {
            _accountName = accountName;
            _caller = caller;
            UpdateText();
            StatusText.Text = hasIncomingCall() ? "Call is still ringing." : "Incoming call is no longer waiting.";
        }

        private async void AnswerButton_Click(object sender, RoutedEventArgs e)
        {
            await RunAndCloseAsync(_answer);
        }

        private async void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            await RunAndCloseAsync(_decline);
        }

        private async void VoicemailButton_Click(object sender, RoutedEventArgs e)
        {
            await RunAndCloseAsync(_sendToVoicemail);
        }

        private async void TransferButton_Click(object sender, RoutedEventArgs e)
        {
            var destination = TransferDestinationBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(destination))
            {
                StatusText.Text = "Enter an extension, number, or SIP address before transferring.";
                TransferDestinationBox.Focus();
                return;
            }

            await RunAndCloseAsync(() => _transfer(destination));
        }

        private void IncomingCallWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private async Task RunAndCloseAsync(Func<Task> action)
        {
            try
            {
                SetButtonsEnabled(false);
                StatusText.Text = _hasIncomingCall() ? "Working on the incoming call." : "Incoming call is no longer waiting.";
                await action();
                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
                SetButtonsEnabled(true);
            }
        }

        private void UpdateText()
        {
            var account = string.IsNullOrWhiteSpace(_accountName) ? "Flex Phone" : _accountName;
            var caller = string.IsNullOrWhiteSpace(_caller) ? "Unknown caller" : _caller;
            SummaryText.Text = $"Incoming call from {caller}";
            DetailText.Text = $"Ringing on {account}.";
            StatusText.Text = "";
        }

        private void SetButtonsEnabled(bool enabled)
        {
            AnswerButton.IsEnabled = enabled;
            DeclineButton.IsEnabled = enabled;
            VoicemailButton.IsEnabled = enabled;
            TransferButton.IsEnabled = enabled;
        }
    }
}
