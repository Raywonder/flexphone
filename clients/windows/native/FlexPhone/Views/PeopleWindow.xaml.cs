using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FlexPhone.Models;
using FlexPhone.Services;
using MessageBox = System.Windows.MessageBox;

namespace FlexPhone.Views
{
    public partial class PeopleWindow : Window
    {
        private readonly PbxAccountSession _account;
        private readonly FlexPbxClient _pbxClient;
        private readonly ObservableCollection<FlexPhonePresenceInfo> _people = [];
        private readonly DispatcherTimer _fastRefreshTimer = new() { Interval = TimeSpan.FromSeconds(30) };
        private readonly DispatcherTimer _slowRefreshTimer = new() { Interval = TimeSpan.FromSeconds(60) };
        private bool _isRefreshing;

        public PeopleWindow(PbxAccountSession account, FlexPbxClient pbxClient, IEnumerable<FlexPhonePresenceInfo> people)
        {
            InitializeComponent();
            _account = account;
            _pbxClient = pbxClient;
            PeopleList.ItemsSource = _people;
            ReplacePeople(people, announce: true);
            PeopleList.SelectionChanged += (_, _) => RefreshActionState();
            PeopleList.ContextMenuOpening += (_, _) => RefreshActionState();
            _fastRefreshTimer.Tick += async (_, _) => await RefreshPeopleAsync(announce: false);
            _slowRefreshTimer.Tick += async (_, _) => await RefreshPeopleAsync(announce: false);
            Loaded += (_, _) =>
            {
                if (_people.Count > 0)
                {
                    PeopleList.SelectedIndex = 0;
                    PeopleList.Focus();
                }

                _fastRefreshTimer.Start();
                _slowRefreshTimer.Start();
            };
            Closed += (_, _) =>
            {
                _fastRefreshTimer.Stop();
                _slowRefreshTimer.Stop();
            };
        }

        private FlexPhonePresenceInfo? SelectedPerson => PeopleList.SelectedItem as FlexPhonePresenceInfo;

        private async Task RefreshPeopleAsync(bool announce)
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            var selectedExtension = SelectedPerson?.Extension;
            try
            {
                if (announce)
                {
                    StatusText.Text = "Refreshing people.";
                }

                var result = await _pbxClient.GetPresenceAsync(_account.Server, _account.Extension, _account.SessionToken);
                if (!result.Success)
                {
                    if (announce)
                    {
                        StatusText.Text = FirstText(result.Error, result.Message, "People are not available right now.");
                    }

                    return;
                }

                ReplacePeople(result.People, announce);
                if (!string.IsNullOrWhiteSpace(selectedExtension))
                {
                    var restored = _people.FirstOrDefault(person => person.Extension == selectedExtension);
                    if (restored is not null)
                    {
                        PeopleList.SelectedItem = restored;
                    }
                }
            }
            catch (Exception ex)
            {
                if (announce)
                {
                    StatusText.Text = $"People failed: {ex.Message}";
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async void CallMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await SendPresenceActionAsync("call", "Call");
        }

        private async void IntercomMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await SendPresenceActionAsync("intercom", "Intercom");
        }

        private async void SendTextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var message = MessageTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Enter the message first, then use Send text message.", "Flex Phone - People", MessageBoxButton.OK, MessageBoxImage.Information);
                MessageTextBox.Focus();
                return;
            }

            await SendPresenceActionAsync("text", "Text message", message);
        }

        private async void SendVoicemailMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var message = MessageTextBox.Text.Trim();
            await SendPresenceActionAsync("voicemail", "Voicemail", message);
        }

        private async void PeopleList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedPerson?.IsOnline == true)
            {
                await SendPresenceActionAsync("call", "Call");
            }
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private async Task SendPresenceActionAsync(string action, string label, string message = "")
        {
            if (SelectedPerson is not { } person)
            {
                StatusText.Text = "Choose a person first.";
                return;
            }

            try
            {
                StatusText.Text = $"{label} request for {PersonName(person)}.";
                var result = await _pbxClient.SendPresenceActionAsync(
                    _account.Server,
                    _account.Extension,
                    _account.SessionToken,
                    person.Extension,
                    action,
                    message);
                StatusText.Text = result.Success
                    ? FirstText(result.Message, $"{label} request sent to {PersonName(person)}.")
                    : FirstText(result.Error, result.Message, $"{label} is not available for {PersonName(person)} right now.");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"{label} failed: {ex.Message}";
            }
        }

        private void ReplacePeople(IEnumerable<FlexPhonePresenceInfo> people, bool announce)
        {
            _people.Clear();
            foreach (var person in people
                .OrderByDescending(person => person.IsOnline)
                .ThenBy(person => string.IsNullOrWhiteSpace(person.DisplayName) ? person.Extension : person.DisplayName))
            {
                _people.Add(person);
            }

            if (announce)
            {
                StatusText.Text = _people.Count == 0
                    ? "No other Flex PBX users were found."
                    : $"{_people.Count} people. Use the context menu for actions. Press Escape to close.";
            }

            RefreshActionState();
        }

        private void RefreshActionState()
        {
            var person = SelectedPerson;
            var hasPerson = person is not null && !string.IsNullOrWhiteSpace(person.Extension);
            var isOnline = hasPerson && person!.IsOnline;
            CallMenuItem.IsEnabled = isOnline;
            IntercomMenuItem.IsEnabled = isOnline;
        }

        private static string PersonName(FlexPhonePresenceInfo person)
        {
            return string.IsNullOrWhiteSpace(person.DisplayName) ? person.Extension : person.DisplayName;
        }

        private static string FirstText(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }
    }
}
