using System.Windows;
using System.Windows.Controls;
using FlexPhone.Models;
using FlexPhone.Services;
using MessageBox = System.Windows.MessageBox;

namespace FlexPhone.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly FlexPbxClient _pbxClient = new();
        private readonly string _currentExtension;

        public SettingsWindow(FlexPhoneSettings settings, string currentServer, string currentExtension, string currentDisplayName)
        {
            InitializeComponent();
            Settings = new FlexPhoneSettings
            {
                DefaultPbxServer = settings.DefaultPbxServer,
                DefaultTurnServer = settings.DefaultTurnServer,
                UseCustomTurnServer = settings.UseCustomTurnServer,
                CustomTurnServer = settings.CustomTurnServer,
                MinimizeToTray = settings.MinimizeToTray,
                StartMinimizedToTray = settings.StartMinimizedToTray,
                RememberSignIn = settings.RememberSignIn,
                PlayCallSounds = settings.PlayCallSounds,
                AutoAnswer = settings.AutoAnswer,
                ProviderType = settings.ProviderType,
                SpacebarInCallAction = settings.SpacebarInCallAction,
                ClientDisplayName = settings.ClientDisplayName,
                AutoQueueSignInOutMode = settings.AutoQueueSignInOutMode,
                AllowIntercom = settings.AllowIntercom,
                CheckForUpdates = settings.CheckForUpdates,
                AutomaticallyInstallUpdates = settings.AutomaticallyInstallUpdates,
                UpdatePostponeCount = settings.UpdatePostponeCount,
                UpdatePostponedUntil = settings.UpdatePostponedUntil,
                DefaultLocalSipPort = settings.DefaultLocalSipPort,
                UserStatus = settings.UserStatus,
                BrowserLoginPath = settings.BrowserLoginPath,
                PasswordResetPath = settings.PasswordResetPath,
                AccountRecoveryPath = settings.AccountRecoveryPath,
                ClientDownloadPath = settings.ClientDownloadPath,
                UpdateManifestPath = settings.UpdateManifestPath,
                QueueToggleCode = settings.QueueToggleCode,
                QueueLoginCode = settings.QueueLoginCode,
                QueueLogoutCode = settings.QueueLogoutCode,
                QueueUsesSingleToggleCode = settings.QueueUsesSingleToggleCode,
                VoicemailCode = settings.VoicemailCode,
                DndToggleCode = settings.DndToggleCode,
                CallScreeningToggleCode = settings.CallScreeningToggleCode,
                AnnounceLineChanges = settings.AnnounceLineChanges,
                AnnounceQueueDuration = settings.AnnounceQueueDuration,
                AnnounceCallEnded = settings.AnnounceCallEnded,
                DetailedLineAnnouncements = settings.DetailedLineAnnouncements,
                ShowKeyboardHints = settings.ShowKeyboardHints,
                HasSeenGettingStarted = settings.HasSeenGettingStarted
            };
            _currentExtension = currentExtension;
            SelectProvider(Settings.ProviderType);
            DefaultServerBox.Text = string.IsNullOrWhiteSpace(currentServer) ? Settings.DefaultPbxServer : currentServer;
            LocalPortBox.Text = Settings.DefaultLocalSipPort.ToString();
            RememberSignInCheckBox.IsChecked = Settings.RememberSignIn;
            UseCustomTurnCheckBox.IsChecked = Settings.UseCustomTurnServer;
            TurnServerBox.Text = Settings.EffectiveTurnServer;
            AutoAnswerCheckBox.IsChecked = Settings.AutoAnswer;
            DisplayNameBox.Text = string.IsNullOrWhiteSpace(Settings.ClientDisplayName) ? currentDisplayName : Settings.ClientDisplayName;
            SelectSpacebarAction(Settings.SpacebarInCallAction);
            SelectAutoQueueMode(Settings.AutoQueueSignInOutMode);
            IntercomCheckBox.IsChecked = Settings.AllowIntercom;
            PlaySoundsCheckBox.IsChecked = Settings.PlayCallSounds;
            MinimizeToTrayCheckBox.IsChecked = Settings.MinimizeToTray;
            StartMinimizedCheckBox.IsChecked = Settings.StartMinimizedToTray;
            CheckUpdatesCheckBox.IsChecked = Settings.CheckForUpdates;
            AutoInstallUpdatesCheckBox.IsChecked = Settings.AutomaticallyInstallUpdates;
            UpdateManifestPathBox.Text = Settings.UpdateManifestPath;
            AnnounceLineChangesCheckBox.IsChecked = Settings.AnnounceLineChanges;
            AnnounceQueueDurationCheckBox.IsChecked = Settings.AnnounceQueueDuration;
            AnnounceCallEndedCheckBox.IsChecked = Settings.AnnounceCallEnded;
            DetailedLineAnnouncementsCheckBox.IsChecked = Settings.DetailedLineAnnouncements;
            ShowKeyboardHintsCheckBox.IsChecked = Settings.ShowKeyboardHints;
            BrowserLoginPathBox.Text = Settings.BrowserLoginPath;
            AccountRecoveryPathBox.Text = Settings.AccountRecoveryPath;
            ClientDownloadPathBox.Text = Settings.ClientDownloadPath;
            QueueLoginCodeBox.Text = Settings.QueueLoginCode;
            QueueUsesSingleToggleCodeCheckBox.IsChecked = Settings.QueueUsesSingleToggleCode;
            QueueLogoutCodeBox.Text = Settings.QueueLogoutCode;
            VoicemailCodeBox.Text = Settings.VoicemailCode;
            DndToggleCodeBox.Text = Settings.DndToggleCode;
            CallScreeningToggleCodeBox.Text = Settings.CallScreeningToggleCode;
            SelectStatus(Settings.UserStatus);
            UpdateProvisioningLink();
        }

        public FlexPhoneSettings Settings { get; private set; }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(LocalPortBox.Text, out var port) || port < 1024 || port > 65535)
            {
                MessageBox.Show("Local SIP port must be between 1024 and 65535.", "Flex Phone Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Settings.DefaultPbxServer = DefaultServerBox.Text.Trim();
            Settings.ProviderType = (ProviderTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Flex PBX";
            Settings.DefaultLocalSipPort = port;
            Settings.RememberSignIn = RememberSignInCheckBox.IsChecked == true;
            Settings.UseCustomTurnServer = UseCustomTurnCheckBox.IsChecked == true;
            Settings.CustomTurnServer = Settings.UseCustomTurnServer ? TurnServerBox.Text.Trim() : "";
            Settings.AutoAnswer = AutoAnswerCheckBox.IsChecked == true;
            Settings.ClientDisplayName = DisplayNameBox.Text.Trim();
            Settings.SpacebarInCallAction = (SpacebarActionComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Mute or unmute microphone";
            Settings.AutoQueueSignInOutMode = (AutoQueueModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Off";
            Settings.AllowIntercom = IntercomCheckBox.IsChecked == true;
            Settings.PlayCallSounds = PlaySoundsCheckBox.IsChecked == true;
            Settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
            Settings.StartMinimizedToTray = StartMinimizedCheckBox.IsChecked == true;
            Settings.CheckForUpdates = CheckUpdatesCheckBox.IsChecked == true;
            Settings.AutomaticallyInstallUpdates = AutoInstallUpdatesCheckBox.IsChecked == true;
            Settings.UserStatus = (UserStatusComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Available";
            Settings.BrowserLoginPath = NormalizePath(BrowserLoginPathBox.Text);
            Settings.AccountRecoveryPath = NormalizePath(AccountRecoveryPathBox.Text);
            Settings.ClientDownloadPath = NormalizePath(ClientDownloadPathBox.Text);
            Settings.UpdateManifestPath = NormalizePath(UpdateManifestPathBox.Text);
            Settings.QueueLoginCode = NormalizeFeatureCode(QueueLoginCodeBox.Text, "*45");
            Settings.QueueUsesSingleToggleCode = QueueUsesSingleToggleCodeCheckBox.IsChecked == true;
            Settings.QueueLogoutCode = NormalizeFeatureCode(QueueLogoutCodeBox.Text, "*46");
            Settings.QueueToggleCode = Settings.QueueLoginCode;
            if (Settings.QueueUsesSingleToggleCode)
            {
                Settings.QueueLogoutCode = Settings.QueueLoginCode;
            }
            Settings.VoicemailCode = NormalizeFeatureCode(VoicemailCodeBox.Text, "*97");
            Settings.DndToggleCode = NormalizeFeatureCode(DndToggleCodeBox.Text, "*76");
            Settings.CallScreeningToggleCode = NormalizeFeatureCode(CallScreeningToggleCodeBox.Text, "*56");
            Settings.AnnounceLineChanges = AnnounceLineChangesCheckBox.IsChecked == true;
            Settings.AnnounceQueueDuration = AnnounceQueueDurationCheckBox.IsChecked == true;
            Settings.AnnounceCallEnded = AnnounceCallEndedCheckBox.IsChecked == true;
            Settings.DetailedLineAnnouncements = DetailedLineAnnouncementsCheckBox.IsChecked == true;
            Settings.ShowKeyboardHints = ShowKeyboardHintsCheckBox.IsChecked == true;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void DownloadPageButton_Click(object sender, RoutedEventArgs e)
        {
            var uri = _pbxClient.BuildDownloadUri(DefaultServerBox.Text, NormalizePath(ClientDownloadPathBox.Text));
            _pbxClient.OpenInBrowser(uri);
        }

        private void AdvancedPathsButton_Click(object sender, RoutedEventArgs e)
        {
            if (AdvancedPathsPanel.Visibility == Visibility.Visible)
            {
                AdvancedPathsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var answer = MessageBox.Show(
                "These settings are only for custom Flex PBX installs or another provider setup. Changing them can stop sign-in, updates, voicemail, or queue features from working. Continue?",
                "Flex Phone - Advanced server paths",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Yes)
            {
                AdvancedPathsPanel.Visibility = Visibility.Visible;
            }
        }

        private void UpdateProvisioningLink()
        {
            try
            {
                ProvisioningLinkBox.Text = _pbxClient.BuildBrowserLoginUri(
                    DefaultServerBox.Text,
                    _currentExtension,
                    BrowserLoginPathBox.Text).ToString();
            }
            catch
            {
                ProvisioningLinkBox.Text = "";
            }
        }

        private void SelectStatus(string status)
        {
            foreach (var item in UserStatusComboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), status, StringComparison.OrdinalIgnoreCase))
                {
                    UserStatusComboBox.SelectedItem = item;
                    return;
                }
            }
            UserStatusComboBox.SelectedIndex = 0;
        }

        private void SelectProvider(string provider)
        {
            foreach (var item in ProviderTypeComboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), provider, StringComparison.OrdinalIgnoreCase))
                {
                    ProviderTypeComboBox.SelectedItem = item;
                    return;
                }
            }

            ProviderTypeComboBox.SelectedIndex = 0;
        }

        private void SelectAutoQueueMode(string mode)
        {
            foreach (var item in AutoQueueModeComboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), mode, StringComparison.OrdinalIgnoreCase))
                {
                    AutoQueueModeComboBox.SelectedItem = item;
                    return;
                }
            }

            AutoQueueModeComboBox.SelectedIndex = 0;
        }

        private void SelectSpacebarAction(string action)
        {
            foreach (var item in SpacebarActionComboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), action, StringComparison.OrdinalIgnoreCase))
                {
                    SpacebarActionComboBox.SelectedItem = item;
                    return;
                }
            }

            SpacebarActionComboBox.SelectedIndex = 0;
        }

        private static string NormalizePath(string path)
        {
            var value = path.Trim();
            return value.StartsWith('/') ? value : "/" + value;
        }

        private static string NormalizeFeatureCode(string code, string fallback)
        {
            var value = code.Trim();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
