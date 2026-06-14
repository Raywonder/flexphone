using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using FlexPhone.Models;
using FlexPhone.Services;
using MessageBox = System.Windows.MessageBox;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolTipIcon = System.Windows.Forms.ToolTipIcon;

namespace FlexPhone.Views
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<PbxAccountSession> _accounts = [];
        private readonly ObservableCollection<LineViewItem> _lineItems = [];
        private readonly ObservableCollection<CallLogEntry> _callLogEntries = [];
        private readonly FlexPhoneSettingsService _settingsService = new();
        private readonly FlexPhoneCredentialStore _credentialStore = new();
        private readonly FlexPbxClient _pbxClient = new();
        private readonly FlexPhoneSoundService _sounds = new();
        private readonly ScreenReaderAnnouncementService _announcements = new();
        private readonly NotifyIcon _trayIcon;
        private readonly DispatcherTimer _heartbeatTimer = new() { Interval = TimeSpan.FromSeconds(30) };
        private readonly DispatcherTimer _queueDurationTimer = new() { Interval = TimeSpan.FromMinutes(1) };
        private static DateTime _lastLogCleanup = DateTime.MinValue;
        private FlexPhoneSettings _settings;
        private bool _isExiting;
        private bool _dndEnabled;
        private bool _refreshingLines;
        private bool _triedRememberedSignIn;
        private bool _updateInstallInProgress;
        private bool _hadActiveCall;
        private FlexPhoneUpdateManifest? _pendingUpdateManifest;
        private CallLogWindow? _callLogWindow;
        private IncomingCallWindow? _incomingCallWindow;
        private string _pendingAuthorizationEmail = "";
        private string _pendingAuthorizationUrl = "";
        private DateTime _lastEscapeAt = DateTime.MinValue;
        private const int MaxUpdatePostpones = 3;
        private const string TappedInPbxServer = "pbx.tappedin.fm";
        private const string DevineCreationsPbxServer = "pbx.devinecreations.net";
        private const int HotKeyAnswer = 0x4650;
        private const int HotKeyDecline = 0x4651;
        private const int HotKeyHold = 0x4652;
        private HwndSource? _hotKeySource;

        private sealed class SipRegistrationRoute
        {
            public required string Label { get; init; }
            public required string Server { get; init; }
            public int Port { get; init; } = 5060;
            public string Transport { get; init; } = "UDP";
            public string RouteType { get; init; } = "";
            public bool Preferred { get; init; }

            public string Target => BuildTarget(Server, Port);
            public bool IsHeadscale => RouteType.Equals("headscale", StringComparison.OrdinalIgnoreCase)
                || Server.StartsWith("100.64.", StringComparison.OrdinalIgnoreCase);
            public string Description => $"{Label} ({Target}, {Transport.ToUpperInvariant()})";
        }

        private PbxAccountSession? SelectedAccount => AccountsListBox.SelectedItem as PbxAccountSession
            ?? _accounts.FirstOrDefault();

        private bool RememberCurrentLoginChoice =>
            ExistingSignInPanel.Visibility == Visibility.Visible
                ? RememberExistingSignInCheckBox.IsChecked == true
                : RememberProvisioningCheckBox.IsChecked == true;

        public MainWindow()
        {
            InitializeComponent();
            _settings = _settingsService.Load();
            AccountsListBox.ItemsSource = _accounts;
            LinesList.ItemsSource = _lineItems;
            _trayIcon = CreateTrayIcon();
            _heartbeatTimer.Tick += async (_, _) => await ReportDeviceStatusAsync();
            _queueDurationTimer.Tick += (_, _) => RefreshState();
            ApplySettingsToUi();
            NotifyPendingUpdateSuccess();
            RefreshState();
            Loaded += MainWindow_Loaded;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            _ = CheckForUpdatesInBackgroundAsync(interactive: false);

            if (_settings.StartMinimizedToTray && _settings.MinimizeToTray)
            {
                Loaded += (_, _) => HideToTray();
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
            {
                HideToTray();
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            UnregisterGlobalCallHotKeys();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            if (!_isExiting && _settings.MinimizeToTray)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (var account in _accounts)
            {
                account.Softphone.Dispose();
            }

            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            UnregisterGlobalCallHotKeys();
            _heartbeatTimer.Stop();
            _queueDurationTimer.Stop();
            base.OnClosed(e);
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            await RegisterCurrentFieldsAsync(replaceSelected: true);
        }

        private void ShowRequestExtensionButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateCurrentAccountEmailText();
            ShowLoginView(RequestExtensionPanel);
            EmailBox.Focus();
        }

        private void ShowExistingSignInButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLoginView(ExistingSignInPanel);
            ExtensionBox.Focus();
        }

        private void ShowLoginChoiceButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLoginView(LoginChoicePanel);
            ShowRequestExtensionButton.Focus();
        }

        private void ShowLoginView(FrameworkElement visiblePanel)
        {
            LoginChoicePanel.Visibility = ReferenceEquals(visiblePanel, LoginChoicePanel) ? Visibility.Visible : Visibility.Collapsed;
            RequestExtensionPanel.Visibility = ReferenceEquals(visiblePanel, RequestExtensionPanel) ? Visibility.Visible : Visibility.Collapsed;
            ExistingSignInPanel.Visibility = ReferenceEquals(visiblePanel, ExistingSignInPanel) ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void RequestExtensionButton_Click(object sender, RoutedEventArgs e)
        {
            var server = ServerBox.Text.Trim();
            var email = EmailBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                MessageBox.Show("Enter the Flex PBX server and your email address first.", "Flex Phone - Get extension", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var result = await _pbxClient.RequestExtensionAsync(server, email, GetOrCreateDeviceId());
                if (!result.Success)
                {
                    MessageBox.Show(FirstText(result.Error, result.Message, "The phone system could not start device authorization."), "Flex Phone - Get extension", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _sounds.PlayQuickAlert(_settings.PlayCallSounds);
                    return;
                }

                _pendingAuthorizationEmail = email;
                _pendingAuthorizationUrl = result.DeviceAuthorizationUrl;
                TokenUrlBox.Text = _pendingAuthorizationUrl;

                if (!string.IsNullOrWhiteSpace(_pendingAuthorizationUrl))
                {
                    Log("Confirmation email sent. Open the email link, then choose Finish sign in.");
                }

                if (CanRegisterFromProvision(result))
                {
                    await RegisterProvisionedAccountAsync(result, replaceSelected: true);
                }
                else
                {
                    var message = FirstText(
                        result.Message,
                        "Confirmation email sent. Open the email link to authorize this device, then return to Flex Phone and choose Finish sign in.");
                    MessageBox.Show(
                        message + "\n\nIf this email is not assigned to an extension yet, the confirmation page will offer the extension request form.",
                        "Flex Phone - Check your email",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    FinishAuthorizationButton.Focus();
                    ShowFlexPhoneNotification("Authorize Flex Phone", "Check your email to approve this device.", ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(FriendlyNetworkError(ex, ServerBox.Text), "Flex Phone - Get extension", MessageBoxButton.OK, MessageBoxImage.Warning);
                _sounds.PlayQuickAlert(_settings.PlayCallSounds);
            }
        }

        private void OpenTokenUrlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Uri.TryCreate(TokenUrlBox.Text.Trim(), UriKind.Absolute, out var uri))
            {
                MessageBox.Show("There is no authorization link to open yet.", "Flex Phone - Authorization", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _pbxClient.OpenInBrowser(uri);
        }

        private async void FinishAuthorizationButton_Click(object sender, RoutedEventArgs e)
        {
            var email = FirstText(_pendingAuthorizationEmail, EmailBox.Text).Trim();
            var tokenUrl = FirstText(_pendingAuthorizationUrl, TokenUrlBox.Text).Trim();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(tokenUrl))
            {
                MessageBox.Show("Get an extension first, then authorize this device.", "Flex Phone - Finish sign in", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var result = await _pbxClient.CompleteDeviceAuthorizationAsync(ServerBox.Text, email, GetOrCreateDeviceId(), tokenUrl);
                if (!result.Success || !CanRegisterFromProvision(result))
                {
                    MessageBox.Show(FirstText(result.Error, result.Message, "This device is not authorized yet. Open the authorization link and try again."), "Flex Phone - Finish sign in", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _sounds.PlayQuickAlert(_settings.PlayCallSounds);
                    return;
                }

                await RegisterProvisionedAccountAsync(result, replaceSelected: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(FriendlyNetworkError(ex, ServerBox.Text), "Flex Phone - Finish sign in", MessageBoxButton.OK, MessageBoxImage.Warning);
                _sounds.PlayQuickAlert(_settings.PlayCallSounds);
            }
        }

        private async Task RegisterCurrentFieldsAsync(bool replaceSelected)
        {
            var server = ServerBox.Text.Trim();
            var identifier = ExtensionBox.Text.Trim();
            var password = CurrentPasswordText();

            FlexPhoneLoginResponse login;
            RegisterButton.IsEnabled = false;
            try
            {
                Log("Signing in to Flex PBX.");
                login = await _pbxClient.LoginAsync(server, identifier, password);
            }
            catch (Exception ex)
            {
                MessageBox.Show(FriendlyNetworkError(ex, server), "Flex Phone - Login", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            finally
            {
                RegisterButton.IsEnabled = true;
            }

            if (!login.Success || string.IsNullOrWhiteSpace(login.Extension))
            {
                var message = FirstText(login.Error, login.Message, "Flex PBX did not accept that login.");
                MessageBox.Show(message, "Flex Phone - Login", MessageBoxButton.OK, MessageBoxImage.Warning);
                _sounds.PlayQuickAlert(_settings.PlayCallSounds);
                return;
            }

            var extension = login.Extension.Trim();
            var sipPassword = FirstText(login.SipPassword);
            if (string.IsNullOrWhiteSpace(sipPassword))
            {
                MessageBox.Show("Signed in, but the phone system did not return phone registration credentials. Please refresh account credentials or contact support.", "Flex Phone - Login", MessageBoxButton.OK, MessageBoxImage.Warning);
                _sounds.PlayQuickAlert(_settings.PlayCallSounds);
                return;
            }
            ApplyFeatureCodes(login.FeatureCodes);

            var sipServer = FirstText(login.SipSettings.Server, server);
            Log($"Signed in as extension {extension}. Registering phone audio.");
            await RegisterAccountAsync(
                server,
                sipServer,
                extension,
                sipPassword,
                login,
                login.SipSettings,
                replaceSelected,
                rememberSignIn: RememberExistingSignInCheckBox.IsChecked == true);
        }

        private async Task RegisterAccountAsync(
            string server,
            string sipServer,
            string extension,
            string password,
            FlexPhoneLoginResponse login,
            FlexPhoneSipSettings? sipSettings,
            bool replaceSelected,
            bool rememberSignIn)
        {
            var localPort = replaceSelected ? _settings.DefaultLocalSipPort : _settings.DefaultLocalSipPort + _accounts.Count;

            if (replaceSelected && SelectedAccount is { } selected)
            {
                await selected.Softphone.UnregisterAsync();
                selected.Softphone.Dispose();
                _accounts.Remove(selected);
            }

            var softphone = new PbxSoftphoneService(_settings.InputAudioDevice, _settings.OutputAudioDevice);
            var displayName = FirstText(_settings.ClientDisplayName, login.FullName);
            var account = new PbxAccountSession
            {
                Server = server,
                SipServer = sipServer,
                Username = login.Username,
                Extension = extension,
                Password = password,
                SessionToken = login.SessionToken,
                FullName = displayName,
                Email = login.Email,
                Role = login.Role,
                Group = login.Group,
                Team = login.Team,
                AutoQueuePolicy = login.AutoQueueSignInOut,
                Softphone = softphone,
                LocalPort = localPort,
                DeviceId = GetOrCreateDeviceId()
            };

            softphone.StateChanged += (_, _) => Dispatcher.Invoke(() =>
            {
                RefreshState();
                _ = TryRunPendingUpdateAfterCallAsync();
            });
            softphone.Diagnostic += (_, message) => Dispatcher.Invoke(() => Log(message, ClassifyLogMessage(message)));
            softphone.ActiveLineChanged += (_, message) => Dispatcher.Invoke(() =>
            {
                if (_settings.AnnounceLineChanges)
                {
                    Log(message);
                }
            });
            softphone.LineFreed += (_, message) => Dispatcher.Invoke(() =>
            {
                if (_settings.AnnounceCallEnded)
                {
                    Log(message);
                    ShowFlexPhoneNotification("Line free", message, ToolTipIcon.Info);
                }
                _ = TryRunPendingUpdateAfterCallAsync();
            });
            softphone.RegistrationSucceeded += (_, message) => Dispatcher.Invoke(() =>
            {
                Log(message);
                ShowFlexPhoneNotification("Flex Phone Registered", message, ToolTipIcon.Info, playNetworkTone: true);
            });
            softphone.IncomingCall += async (_, caller) => await Dispatcher.InvokeAsync(async () =>
            {
                Log($"Incoming call for {account.DisplayName} from {caller}");
                _sounds.PlayIncomingRing(_settings.PlayCallSounds, _settings.IncomingRingtone, _settings.OutputAudioDevice);
                ShowFlexPhoneNotification("Incoming call", $"Call from {caller}", ToolTipIcon.Info);
                if (_settings.AutoAnswer && !_dndEnabled)
                {
                    await RunActionAsync("Auto-answer", () => softphone.AnswerAsync(), softphone);
                    CloseIncomingCallWindow();
                }
                else
                {
                    ShowIncomingCallWindow(account, caller);
                }
            });

            _accounts.Add(account);
            AccountsListBox.SelectedItem = account;
            var routes = BuildSipRegistrationRoutes(sipSettings, sipServer, server);
            var registered = await RegisterWithApprovedRoutesAsync(account, routes, extension, password, localPort);
            if (!registered || !softphone.IsRegistered)
            {
                _accounts.Remove(account);
                softphone.Dispose();
                RefreshState();
                return;
            }

            if (softphone.IsRegistered)
            {
                PasswordBox.Password = "";
                ExtensionBox.Text = extension;
                if (!string.IsNullOrWhiteSpace(_settings.ClientDisplayName)
                    && !string.Equals(_settings.ClientDisplayName, login.FullName, StringComparison.Ordinal))
                {
                    _ = _pbxClient.UpdateDisplayNameAsync(account.Server, account.Extension, account.SessionToken, _settings.ClientDisplayName);
                }
                if (rememberSignIn)
                {
                    SaveRememberedSignIn(account);
                }
                UpdateAccountMenuState();
                _heartbeatTimer.Start();
                _queueDurationTimer.Start();
                await ReportDeviceStatusAsync();
                _ = EnsureCurrentDevicePairedAsync(account);
                PromptForPortableCleanupAfterMigration();
            }
            UpdateProvisioningLink();
            RefreshState();
        }

        private async Task<bool> RegisterWithApprovedRoutesAsync(
            PbxAccountSession account,
            IReadOnlyList<SipRegistrationRoute> routes,
            string extension,
            string password,
            int localPort)
        {
            if (routes.Count == 0)
            {
                MessageBox.Show("Flex Phone did not receive any approved SIP routes for this PBX.", "Flex Phone - Register phone", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            Exception? lastTimeout = null;
            for (var index = 0; index < routes.Count; index++)
            {
                var route = routes[index];
                account.SipServer = route.Target;
                Log($"Trying SIP route {route.Description}.");
                try
                {
                    await account.Softphone.RegisterAsync(route.Target, extension, password, localPort);
                    Log("Register phone");
                    return true;
                }
                catch (TimeoutException ex)
                {
                    lastTimeout = ex;
                    Log($"SIP route timed out on {route.Description}: {ex.Message}");
                    var nextRoute = index + 1 < routes.Count ? routes[index + 1] : null;
                    if (nextRoute is not null)
                    {
                        Log(route.RouteType.Equals("public", StringComparison.OrdinalIgnoreCase)
                            ? "Public SIP did not answer; trying secure Headscale route."
                            : $"Trying next approved SIP route {nextRoute.Description}.");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Register phone failed on {route.Description}: {ex.Message}");
                    var nextRoute = index + 1 < routes.Count ? routes[index + 1] : null;
                    if (nextRoute is not null && IsRecoverableSipRouteFailure(ex))
                    {
                        Log(route.RouteType.Equals("public", StringComparison.OrdinalIgnoreCase)
                            ? "Public SIP rejected registration; trying secure Headscale route."
                            : $"Trying next approved SIP route {nextRoute.Description}.");
                        continue;
                    }

                    _sounds.PlayQuickAlert(_settings.PlayCallSounds);
                    MessageBox.Show(FriendlySipRegistrationError(ex), "Flex Phone - Register phone", MessageBoxButton.OK, MessageBoxImage.Warning);
                    RefreshState();
                    return false;
                }
                finally
                {
                    RefreshState();
                }
            }

            var message = lastTimeout?.Message ?? "Flex Phone could not register on any approved SIP route.";
            Log($"Register phone failed: {message}");
            _sounds.PlayQuickAlert(_settings.PlayCallSounds);
            MessageBox.Show(message, "Flex Phone - Register phone", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private static IReadOnlyList<SipRegistrationRoute> BuildSipRegistrationRoutes(
            FlexPhoneSipSettings? sipSettings,
            string sipServer,
            string pbxServer)
        {
            var routes = new List<SipRegistrationRoute>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string label, string server, int port, string transport, string routeType, bool preferred)
            {
                var normalizedServer = NormalizeSipRouteHost(server);
                if (string.IsNullOrWhiteSpace(normalizedServer))
                {
                    return;
                }

                var normalizedTransport = string.IsNullOrWhiteSpace(transport)
                    ? "UDP"
                    : transport.Trim().ToUpperInvariant();
                var route = new SipRegistrationRoute
                {
                    Label = string.IsNullOrWhiteSpace(label) ? "SIP route" : label.Trim(),
                    Server = normalizedServer,
                    Port = port > 0 ? port : 5060,
                    Transport = normalizedTransport,
                    RouteType = routeType.Trim(),
                    Preferred = preferred
                };
                if (seen.Add($"{route.Target}|{route.Transport}"))
                {
                    routes.Add(route);
                }
            }

            if (sipSettings?.Routes is { Count: > 0 } configuredRoutes)
            {
                foreach (var route in configuredRoutes)
                {
                    Add(
                        FirstText(route.Label ?? "", route.RouteType ?? "", "SIP route"),
                        FirstText(route.Host, route.Server),
                        route.Port,
                        FirstText(route.Transport ?? "", sipSettings.Transport, "UDP"),
                        route.RouteType ?? "",
                        route.Preferred);
                }
            }

            Add(
                "Public SIP",
                FirstText(sipSettings?.Host ?? "", sipSettings?.Server ?? "", sipServer, pbxServer),
                sipSettings?.Port ?? 5060,
                FirstText(sipSettings?.Transport ?? "", "UDP"),
                "public",
                true);

            if (sipSettings?.Fallbacks is { Count: > 0 } fallbackRoutes)
            {
                foreach (var route in fallbackRoutes)
                {
                    Add(
                        FirstText(route.Label ?? "", route.RouteType ?? "", "SIP fallback"),
                        FirstText(route.Host, route.Server),
                        route.Port,
                        FirstText(route.Transport ?? "", sipSettings.Transport, "UDP"),
                        route.RouteType ?? "",
                        route.Preferred);
                }
            }

            if (routes.Count == 0)
            {
                Add("Configured SIP server", FirstText(sipServer, pbxServer), 5060, "UDP", "public", true);
            }

            return routes;
        }

        private static bool IsRecoverableSipRouteFailure(Exception ex)
        {
            var message = ex.Message;
            return message.Contains("401 Unauthorized", StringComparison.OrdinalIgnoreCase)
                || message.Contains("403 Forbidden", StringComparison.OrdinalIgnoreCase)
                || message.Contains("404 Not Found", StringComparison.OrdinalIgnoreCase)
                || message.Contains("408 Request Timeout", StringComparison.OrdinalIgnoreCase)
                || message.Contains("503 Service Unavailable", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKnownFlexPbxHost(string value)
        {
            var host = NormalizeSipRouteHost(value);
            return host.Equals(TappedInPbxServer, StringComparison.OrdinalIgnoreCase)
                || host.Equals(DevineCreationsPbxServer, StringComparison.OrdinalIgnoreCase)
                || host.Equals("flexpbx.devinecreations.net", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildTarget(string server, int port)
        {
            var host = NormalizeSipRouteHost(server);
            if (port > 0 && port != 5060 && !host.Contains(':'))
            {
                return $"{host}:{port}";
            }

            return host;
        }

        private static string NormalizeSipRouteHost(string value)
        {
            var host = value.Trim();
            if (host.StartsWith("sip:", StringComparison.OrdinalIgnoreCase))
            {
                host = host[4..];
            }
            else if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                host = host[8..];
            }
            else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                host = host[7..];
            }

            var slash = host.IndexOf('/');
            if (slash >= 0)
            {
                host = host[..slash];
            }

            return host.Trim();
        }

        private async Task RegisterProvisionedAccountAsync(FlexPhoneProvisionResponse result, bool replaceSelected)
        {
            var password = FirstText(result.SipPassword);
            if (string.IsNullOrWhiteSpace(result.Extension) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Flex PBX has not sent phone registration credentials yet. Finish authorizing this device and try again.", "Flex Phone - Sign in", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ExtensionBox.Text = result.Extension;
            PasswordBox.Password = "";
            EmailBox.Text = FirstText(result.Email, EmailBox.Text);
            ApplyFeatureCodes(result.FeatureCodes);
            await RegisterAccountAsync(
                ServerBox.Text.Trim(),
                FirstText(result.SipSettings.Server, ServerBox.Text.Trim()),
                result.Extension.Trim(),
                password,
                result,
                result.SipSettings,
                replaceSelected,
                rememberSignIn: RememberProvisioningCheckBox.IsChecked == true);
        }

        private static bool CanRegisterFromProvision(FlexPhoneProvisionResponse result)
        {
            return result.Success
                && !string.IsNullOrWhiteSpace(result.Extension)
                && !string.IsNullOrWhiteSpace(result.SipPassword);
        }

        private async void UnregisterButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is not { } account)
            {
                return;
            }

            await RunActionAsync("Log out", () => account.Softphone.UnregisterAsync(), account.Softphone);
            _accounts.Remove(account);
            account.Softphone.Dispose();
            RefreshState();
        }

        private async void DialButton_Click(object sender, RoutedEventArgs e)
        {
            await DialSelectedDestinationAsync();
        }

        private void CallLogMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_callLogWindow is { IsVisible: true })
            {
                _callLogWindow.Activate();
                return;
            }

            _callLogWindow = new CallLogWindow(_callLogEntries)
            {
                Owner = this
            };
            _callLogWindow.Closed += (_, _) => _callLogWindow = null;
            _callLogWindow.Show();
        }

        private async void AnswerButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                await RunActionAsync("Pick up", () => account.Softphone.AnswerAsync(), account.Softphone);
            }
        }

        private async void HangupButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                await RunActionAsync("End", () => account.Softphone.HangupAsync(), account.Softphone);
                _sounds.PlayCallEnded(_settings.PlayCallSounds);
                ClearDialBoxAfterCall();
            }
        }

        private void ToggleDialPadButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleDialPad();
        }

        private async void HoldButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                await RunActionAsync("Hold", () => account.Softphone.HoldAsync(), account.Softphone);
            }
        }

        private async void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                await RunActionAsync("Resume", () => account.Softphone.ResumeAsync(), account.Softphone);
            }
        }

        private async void TransferButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                await RunActionAsync("Transfer", () => account.Softphone.TransferAsync(account.Server, TransferDestinationBox.Text), account.Softphone);
            }
        }

        private async void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                await RunActionAsync("Mute", () => account.Softphone.SetMutedAsync(true), account.Softphone);
            }
        }

        private async void UnmuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                await RunActionAsync("Unmute", () => account.Softphone.SetMutedAsync(false), account.Softphone);
            }
        }

        private async void IntercomButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                var destination = DestinationBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(destination))
                {
                    MessageBox.Show("Enter an extension before starting intercom.", "Flex Phone - Intercom", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await RunActionAsync("Intercom", () => account.Softphone.DialAsync(
                    account.Server,
                    account.Extension,
                    account.Password,
                    $"*80{destination}"),
                    account.Softphone);
            }
        }

        private async void DndButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                var completed = await DialFeatureCodeAsync("Do not disturb", _settings.DndToggleCode);
                if (!completed)
                {
                    Log("Do not disturb was not changed because the request failed.");
                    return;
                }
                _dndEnabled = !_dndEnabled;
                var status = _dndEnabled ? "Do not disturb" : "Available";
                var sent = await _pbxClient.PostUserStatusAsync(account.Server, account.Extension, status);
                Log(sent ? $"Status updated to {status}" : $"Status saved locally as {status}");
            }
            else
            {
                _dndEnabled = !_dndEnabled;
                Log(_dndEnabled ? "Do not disturb is on." : "Do not disturb is off.");
            }
            RefreshState();
        }

        private async void QueueToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is not { } account)
            {
                return;
            }

            var wasLoggedIn = account.QueueState == QueueState.LoggedIn;
            var code = QueueActionCode(wasLoggedIn);
            var action = wasLoggedIn ? "Queue out" : "Queue in";
            Log($"Queue button. {QueueStatusText(account)}");
            var completed = await DialFeatureCodeAsync(action, code);
            if (!completed)
            {
                Log("Queue status was not changed because the queue request failed.");
                return;
            }
            account.QueueState = wasLoggedIn ? QueueState.LoggedOut : QueueState.LoggedIn;
            account.QueueStateChangedAt = DateTime.Now;

            Log(QueueStatusText(account));
            await ReportDeviceStatusAsync();
        }

        private void QueueToggleButton_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                Log($"Queue button. {QueueStatusText(account)}");
            }
        }

        private async void WaitingCallButton_Click(object sender, RoutedEventArgs e)
        {
            await RunControlActionAsync("Waiting calls", async account =>
            {
                var result = await _pbxClient.GetWaitingCallsAsync(account.Server, account.Extension, account.SessionToken);
                var message = result.Calls.Count == 0
                    ? "No calls are waiting right now."
                    : string.Join(Environment.NewLine, result.Calls.Select(call =>
                        $"{FirstText(call.DisplayName, "Caller")} {FirstText(call.Last4, call.Number)} {call.State}".Trim()));
                MessageBox.Show(message, "Flex Phone - Waiting", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private async void VoicemailButton_Click(object sender, RoutedEventArgs e)
        {
            await DialFeatureCodeAsync("Voicemail", _settings.VoicemailCode);
        }

        private async Task SendIncomingCallToVoicemailAsync(PbxAccountSession account)
        {
            await RunActionAsync("Send to voicemail", () => account.Softphone.HangupAsync(), account.Softphone);
            _sounds.PlayCallEnded(_settings.PlayCallSounds);
            CloseIncomingCallWindow();
        }

        private void MessagesButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is not { } account)
            {
                return;
            }

            var window = new MessagesWindow(account, _pbxClient)
            {
                Owner = this
            };
            window.Show();
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            await RunControlActionAsync("Record", async account =>
            {
                var result = await _pbxClient.ToggleServerRecordingAsync(account.Server, account.Extension, account.SessionToken);
                MessageBox.Show(FirstText(result.Message, result.Error, "Recording request completed."), "Flex Phone - Record", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            });
        }

        private async void RecordingsButton_Click(object sender, RoutedEventArgs e)
        {
            await RunControlActionAsync("Recordings", async account =>
            {
                var result = await _pbxClient.GetRecordingsAsync(account.Server, account.Extension, account.SessionToken);
                var message = result.Recordings.Count == 0
                    ? "No server recordings were found for this extension."
                    : string.Join(Environment.NewLine, result.Recordings.Select(recording =>
                        $"{recording.Date} {recording.Name}".Trim()));
                MessageBox.Show(message, "Flex Phone - Recordings", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private async void PeopleButton_Click(object sender, RoutedEventArgs e)
        {
            await RunControlActionAsync("Contacts", async account =>
            {
                var result = await _pbxClient.GetPresenceAsync(account.Server, account.Extension, account.SessionToken);
                var window = new PeopleWindow(account, _pbxClient, result.People)
                {
                    Owner = this
                };
                window.Show();
            });
        }

        private async void DirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            await RunControlActionAsync("Directory", async account =>
            {
                var result = await _pbxClient.GetPresenceAsync(account.Server, account.Extension, account.SessionToken);
                var window = new PeopleWindow(account, _pbxClient, result.People, directoryMode: true)
                {
                    Owner = this
                };
                window.Show();
            });
        }

        private void PairButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is not { } account)
            {
                return;
            }

            var window = new PairingWindow(account, _pbxClient)
            {
                Owner = this
            };
            window.Show();
        }

        private async void DtmfButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button
                || button.Content?.ToString() is not { Length: > 0 } label)
            {
                return;
            }

            await PressDtmfOrAppendAsync(label[0]);
        }

        private async Task PressDtmfOrAppendAsync(char digit)
        {
            if (SelectedAccount?.Softphone.IsInCall == true)
            {
                await RunActionAsync($"DTMF {digit}", () => SelectedAccount.Softphone.SendDtmfAsync(digit), SelectedAccount.Softphone);
                return;
            }

            DestinationBox.Text += digit;
        }

        private async Task<bool> DialFeatureCodeAsync(string label, string code)
        {
            if (SelectedAccount is not { } account)
            {
                return false;
            }

            return await RunActionAsync(label, () => account.Softphone.DialAsync(
                account.Server,
                account.Extension,
                account.Password,
                code,
                SelectedLineNumber()),
                account.Softphone);
        }

        private async Task DialSelectedDestinationAsync()
        {
            if (SelectedAccount is not { } account)
            {
                return;
            }

            var lineNumber = SelectedLineNumber();
            Log($"Using line {lineNumber}.");
            await RunActionAsync("Dial", () => account.Softphone.DialAsync(
                account.Server,
                account.Extension,
                account.Password,
                DestinationBox.Text,
                lineNumber),
                account.Softphone);
        }

        private async Task RunControlActionAsync(string label, Func<PbxAccountSession, Task> action)
        {
            if (SelectedAccount is not { } account)
            {
                return;
            }

            try
            {
                await action(account);
                Log(label);
            }
            catch (Exception ex)
            {
                Log($"{label} failed: {ex.Message}");
                _sounds.PlayQuickAlert(_settings.PlayCallSounds);
                MessageBox.Show(ex.Message, $"Flex Phone - {label}", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                RefreshState();
            }
        }

        private void AccountsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                ServerBox.Text = account.Server;
                SelectServerPreset(account.Server);
                ExtensionBox.Text = account.Extension;
                AutomationProperties.SetName(AccountsListBox, $"Account, {account.DisplayName}");
                CurrentAccountText.Text = $"SIP account: {account.DisplayName}";
            }
            else
            {
                CurrentAccountText.Text = "No SIP account selected";
            }

            UpdateAccountMenuState();
            UpdateProvisioningLink();
            RefreshState();
        }

        private async void LinesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshingLines)
            {
                return;
            }

            if (SelectedAccount is not { } account || LinesList.SelectedItem is not LineViewItem line)
            {
                return;
            }

            await RunActionAsync($"Line {line.LineNumber}", () => account.Softphone.SelectLineAsync(line.LineNumber), account.Softphone);
        }

        private async void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (IsMenuInteractionFocused())
            {
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemComma)
            {
                e.Handled = true;
                SettingsButton_Click(SettingsButton, new RoutedEventArgs());
                return;
            }

            if (e.Key is >= Key.F1 and <= Key.F12
                && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift))
            {
                e.Handled = true;
                await RunFunctionKeyActionAsync(e.Key);
                return;
            }

            if (e.Key == Key.Tab && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift))
            {
                e.Handled = true;
                MoveFocusWithinActiveView(backwards: Keyboard.Modifiers == ModifierKeys.Shift);
                return;
            }

            if (LoginPanel.Visibility == Visibility.Visible)
            {
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key >= Key.D1 && e.Key <= Key.D8)
            {
                e.Handled = true;
                var line = (int)e.Key - (int)Key.D1 + 1;
                SelectLineInList(line);
                if (SelectedAccount is { } account)
                {
                    await RunActionAsync($"Line {line}", () => account.Softphone.SelectLineAsync(line), account.Softphone);
                }
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D9)
            {
                e.Handled = true;
                Log("Toggling call screening.", "Calls");
                await DialFeatureCodeAsync("Call screening", _settings.CallScreeningToggleCode);
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D)
            {
                e.Handled = true;
                ToggleDialPad();
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.M)
            {
                e.Handled = true;
                MessagesButton_Click(MessagesButton, new RoutedEventArgs());
                return;
            }

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.D)
            {
                e.Handled = true;
                DndButton_Click(DndButton, new RoutedEventArgs());
                return;
            }

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Q)
            {
                e.Handled = true;
                QueueToggleButton_Click(QueueToggleButton, new RoutedEventArgs());
                return;
            }

            if ((Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift) && TryMapKeyboardDigit(e.Key, out var digit))
            {
                e.Handled = true;
                await PressDtmfOrAppendAsync(digit);
                return;
            }

            if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None && !IsTextInputFocused())
            {
                if (TryActivateFocusedButton())
                {
                    e.Handled = true;
                    return;
                }

                if (SelectedAccount?.Softphone.IsInCall == true)
                {
                    e.Handled = true;
                    await RunSpacebarInCallActionAsync();
                }
                else if (IsKeyboardFocusInKeypad())
                {
                    e.Handled = true;
                    Log("Spacebar mute, hold, or conference actions only work while you are in a call.");
                }

                return;
            }

            if (e.Key == Key.Enter && !IsTextInputFocused())
            {
                e.Handled = true;
                if (TryActivateFocusedButton())
                {
                    return;
                }

                if (IsKeyboardFocusInKeypad()
                    && Keyboard.FocusedElement is System.Windows.Controls.Button button
                    && button.Content?.ToString() is { Length: > 0 } label)
                {
                    await PressDtmfOrAppendAsync(label[0]);
                }
                else if (SelectedAccount?.Softphone.IsInCall != true)
                {
                    await RunEnterDefaultActionAsync();
                }
                return;
            }

            if (e.Key == Key.Escape)
            {
                var now = DateTime.Now;
                if ((now - _lastEscapeAt) <= TimeSpan.FromSeconds(1.5))
                {
                    e.Handled = true;
                    if (SelectedAccount is { Softphone.ActiveCallCount: > 0 } account)
                    {
                        await HangupActiveOrFirstCallAsync(account);
                        _sounds.PlayCallEnded(_settings.PlayCallSounds);
                        ClearDialBoxAfterCall();
                    }
                    else
                    {
                        MinimizeFromEscape();
                    }
                }
                _lastEscapeAt = now;
            }
        }

        private async void DtmfKeypad_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is not System.Windows.Controls.Button button)
            {
                FocusKeypadButton(0);
                Log("Keypad. Use arrow keys to choose a number and Enter to press it.");
                e.Handled = true;
                return;
            }

            var index = DtmfKeypad.Children.IndexOf(button);
            var next = e.Key switch
            {
                Key.Left => Math.Max(0, index - 1),
                Key.Right => Math.Min(DtmfKeypad.Children.Count - 1, index + 1),
                Key.Up => Math.Max(0, index - 3),
                Key.Down => Math.Min(DtmfKeypad.Children.Count - 1, index + 3),
                _ => index
            };

            if (next != index)
            {
                FocusKeypadButton(next);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter && button.Content?.ToString() is { Length: > 0 } label)
            {
                e.Handled = true;
                await PressDtmfOrAppendAsync(label[0]);
            }
        }

        private void DtmfKeypad_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (Keyboard.FocusedElement is System.Windows.Controls.Button button
                && IsDescendantOf(button, DtmfKeypad))
            {
                return;
            }

            FocusKeypadButton(0);
            Log("Keypad. Use arrow keys to choose a number and Enter to press it.");
        }

        private async Task RunSpacebarInCallActionAsync()
        {
            if (SelectedAccount is not { } account)
            {
                return;
            }

            switch (_settings.SpacebarInCallAction)
            {
                case "End current call":
                    await HangupActiveOrFirstCallAsync(account);
                    _sounds.PlayCallEnded(_settings.PlayCallSounds);
                    break;
                case "Hold or resume current call":
                    if (account.Softphone.ActiveLineState == PbxLineState.Holding)
                    {
                        await RunActionAsync("Resume", () => account.Softphone.ResumeAsync(), account.Softphone);
                    }
                    else
                    {
                        await RunActionAsync("Hold", () => account.Softphone.HoldAsync(), account.Softphone);
                    }
                    break;
                case "Transfer current call":
                    await RunActionAsync("Transfer", () => account.Softphone.TransferAsync(account.Server, TransferDestinationBox.Text.Trim()), account.Softphone);
                    break;
                case "Conference open calls":
                    Log("Conference open calls is selected. Use Lines to choose each open call; server-side conferencing must be enabled by Flex PBX.");
                    break;
                case "No action":
                    Log("Spacebar has no in-call action.");
                    break;
                default:
                    await RunActionAsync(account.Softphone.IsMuted ? "Unmute" : "Mute", () => account.Softphone.SetMutedAsync(!account.Softphone.IsMuted), account.Softphone);
                    break;
            }
        }

        private async Task RunEnterDefaultActionAsync()
        {
            switch (_settings.EnterDefaultAction)
            {
                case "Dial as intercom":
                    IntercomButton_Click(IntercomButton, new RoutedEventArgs());
                    break;
                case "Open messages":
                    MessagesButton_Click(MessagesButton, new RoutedEventArgs());
                    break;
                default:
                    Log("Enter activates the focused control. Focus Dial to place a normal call, or type a number and use the Dial button.");
                    break;
            }

            await Task.CompletedTask;
        }

        private async Task RunFunctionKeyActionAsync(Key key)
        {
            switch (key)
            {
                case Key.F1:
                    AnswerButton_Click(AnswerButton, new RoutedEventArgs());
                    break;
                case Key.F2:
                    HangupButton_Click(HangupButton, new RoutedEventArgs());
                    break;
                case Key.F3:
                    if (SelectedAccount?.Softphone.ActiveLineState == PbxLineState.Holding)
                    {
                        ResumeButton_Click(ResumeButton, new RoutedEventArgs());
                    }
                    else
                    {
                        HoldButton_Click(HoldButton, new RoutedEventArgs());
                    }
                    break;
                case Key.F4:
                    TransferButton_Click(TransferButton, new RoutedEventArgs());
                    break;
                case Key.F5:
                    RecordButton_Click(RecordButton, new RoutedEventArgs());
                    break;
                case Key.F6:
                    if (SelectedAccount?.Softphone.IsMuted == true)
                    {
                        UnmuteButton_Click(UnmuteButton, new RoutedEventArgs());
                    }
                    else
                    {
                        MuteButton_Click(MuteButton, new RoutedEventArgs());
                    }
                    break;
                case Key.F7:
                    QueueToggleButton_Click(QueueToggleButton, new RoutedEventArgs());
                    break;
                case Key.F8:
                    VoicemailButton_Click(VoicemailButton, new RoutedEventArgs());
                    break;
                case Key.F9:
                    MessagesButton_Click(MessagesButton, new RoutedEventArgs());
                    break;
                case Key.F10:
                    PeopleButton_Click(PeopleButton, new RoutedEventArgs());
                    break;
                case Key.F11:
                    DndButton_Click(DndButton, new RoutedEventArgs());
                    break;
                case Key.F12:
                    Log("Toggling call screening.");
                    await DialFeatureCodeAsync("Call screening", _settings.CallScreeningToggleCode);
                    break;
            }
        }

        private async Task HangupActiveOrFirstCallAsync(PbxAccountSession account)
        {
            var line = account.Softphone.Lines.FirstOrDefault(item => item.IsActive && !item.IsFree)
                ?? account.Softphone.Lines.FirstOrDefault(item => !item.IsFree);
            if (line is not null && line.LineNumber != account.Softphone.ActiveLineNumber)
            {
                await account.Softphone.SelectLineAsync(line.LineNumber);
            }

            await RunActionAsync("End", () => account.Softphone.HangupAsync(), account.Softphone);
        }

        private async void ResetUsernameButton_Click(object sender, RoutedEventArgs e)
        {
            await RequestAccountRecoveryAsync("Reset username", "Send the login username for this extension to its recovery email?", "reset_username");
        }

        private async void ResetPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            await RequestAccountRecoveryAsync("Reset password", "Create a new password for this extension and send it to the recovery email?", "reset_password");
        }

        private async void GetPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            await RequestAccountRecoveryAsync("Get password", "Send the current password for this extension to its recovery email? If it cannot be safely read, a new password will be created and emailed instead.", "get_current_password");
        }

        private void LinkEmailMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is { } account)
            {
                EmailBox.Text = account.Email;
            }

            UpdateCurrentAccountEmailText();
            ShowLoginView(RequestExtensionPanel);
            EmailBox.Focus();
            Log("Enter the email address to link. Flex PBX will confirm the new email before changing the account.");
        }

        private void OpenUserLoginMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var extension = SelectedAccount?.Extension ?? ExtensionBox.Text;
            _pbxClient.OpenInBrowser(_pbxClient.BuildBrowserLoginUri(ServerBox.Text, extension, _settings.BrowserLoginPath));
        }

        private void OpenAdminLoginMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _pbxClient.OpenInBrowser(_pbxClient.BuildDownloadUri(ServerBox.Text, "/admin/"));
        }

        private async Task RequestAccountRecoveryAsync(string title, string confirmationMessage, string action)
        {
            var extension = ExtensionBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(extension))
            {
                MessageBox.Show("Enter an extension, username, or email first.", $"Flex Phone - {title}", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmation = MessageBox.Show(
                confirmationMessage,
                $"Flex Phone - {title}",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
            {
                Log($"{title} cancelled.");
                return;
            }

            try
            {
                var result = await _pbxClient.RequestAccountRecoveryAsync(
                    ServerBox.Text,
                    _settings.AccountRecoveryPath,
                    extension,
                    action,
                    confirmed: true);

                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? (result.Success ? "Request sent. Check the recovery email for the next step." : "The request could not be completed.")
                    : result.Message;

                Log(message);
                ShowFlexPhoneNotification(title, message, result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
            }
            catch (Exception ex)
            {
                var message = FriendlyNetworkError(ex, ServerBox.Text);
                Log($"{title} failed: {message}");
                _sounds.PlayQuickAlert(_settings.PlayCallSounds);
                MessageBox.Show(message, $"Flex Phone - {title}", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var previousDisplayName = _settings.ClientDisplayName;
            var window = new SettingsWindow(_settings, ServerBox.Text, SelectedAccount?.Extension ?? ExtensionBox.Text, SelectedAccount?.FullName ?? "")
            {
                Owner = this
            };

            if (window.ShowDialog() == true)
            {
                _settings = window.Settings;
                ServerBox.Text = _settings.DefaultPbxServer;
                SaveSettings();
                UnregisterGlobalCallHotKeys();
                if (SelectedAccount is { } account
                    && !string.IsNullOrWhiteSpace(_settings.ClientDisplayName)
                    && !string.Equals(previousDisplayName, _settings.ClientDisplayName, StringComparison.Ordinal))
                {
                    account.FullName = _settings.ClientDisplayName;
                    _ = _pbxClient.UpdateDisplayNameAsync(account.Server, account.Extension, account.SessionToken, _settings.ClientDisplayName);
                    AutomationProperties.SetName(AccountsListBox, $"Account, {account.DisplayName}");
                    Log($"Display name updated to {_settings.ClientDisplayName}.");
                }
                UpdateProvisioningLink();
                RefreshState();
                UpdateAccountMenuState();
            }
        }

        private async void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!IsActive || e.IsRepeat)
            {
                return;
            }

            var hotKey = HotKeyText(e);
            if (string.IsNullOrWhiteSpace(hotKey))
            {
                return;
            }

            if (HotKeyMatches(hotKey, _settings.AnswerHotKey, "Ctrl+Shift+A"))
            {
                e.Handled = true;
                await RunIncomingHotKeyActionAsync("Answer", answer: true, holdAfterAnswer: false);
                return;
            }

            if (HotKeyMatches(hotKey, _settings.HangupHotKey, "Ctrl+Shift+H"))
            {
                e.Handled = true;
                await RunIncomingHotKeyActionAsync("Hang up", answer: false, holdAfterAnswer: false);
                return;
            }

            if (HotKeyMatches(hotKey, _settings.HoldHotKey, "Ctrl+Shift+O"))
            {
                e.Handled = true;
                await RunIncomingHotKeyActionAsync("Hold incoming", answer: true, holdAfterAnswer: true);
            }
        }

        private async void InstallFullVersionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (IsInstalledProgramFilesRun())
            {
                MessageBox.Show("This is already the installed Flex Phone.", "Flex Phone", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SelectedAccount is { } account && RememberCurrentLoginChoice)
            {
                SaveRememberedSignIn(account);
            }

            var answer = MessageBox.Show(
                "Install the full Flex Phone app on this device? Your remembered sign-in stays with this Windows account and will be used by the installed app.",
                "Flex Phone - Install full version",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var manifest = await _pbxClient.GetUpdateManifestAsync(ServerBox.Text, _settings.UpdateManifestPath);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.ResolvedDownloadUrl))
                {
                    MessageBox.Show("Flex Phone could not find installer information from Flex PBX.", "Flex Phone - Install full version", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await DownloadAndInstallUpdateAsync(manifest, launchInstalledAfterInstall: true);
            }
            catch (Exception ex)
            {
                var message = FriendlyNetworkError(ex, ServerBox.Text);
                Log($"Full install failed: {message}");
                MessageBox.Show(message, "Flex Phone - Install full version", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _ = TryRememberedSignInAsync();

            if (_settings.HasSeenGettingStarted)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (LoginPanel.Visibility == Visibility.Visible)
                    {
                        ShowRequestExtensionButton.Focus();
                    }
                    else
                    {
                        DestinationBox.Focus();
                    }
                }), DispatcherPriority.ContextIdle);
                return;
            }

            _settings.HasSeenGettingStarted = true;
            SaveSettings();
            Dispatcher.BeginInvoke(new Action(() => ShowHelp(gettingStarted: true)), DispatcherPriority.ContextIdle);
        }

        private void GettingStartedMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowHelp(gettingStarted: true);
        }

        private void ManualMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowHelp(gettingStarted: false);
        }

        private async void CheckForUpdatesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesInBackgroundAsync(interactive: true, force: true);
        }

        private void AutoCheckUpdatesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _settings.CheckForUpdates = AutoCheckUpdatesMenuItem.IsChecked;
            SaveSettings();
            Log(_settings.CheckForUpdates ? "Automatic update checks are on." : "Automatic update checks are off.");
        }

        private void ForgetRememberedSignInMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _credentialStore.Delete();
            Log("Remembered sign-in removed.");
            ShowFlexPhoneNotification("Flex Phone", "Remembered sign-in removed.", ToolTipIcon.Info);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _isExiting = true;
            Close();
        }

        private async void CallScreeningMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Log("Toggling call screening.");
            await DialFeatureCodeAsync("Call screening", _settings.CallScreeningToggleCode);
        }

        private async void LineMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: string tag }
                || !int.TryParse(tag, out var line)
                || SelectedAccount is not { } account)
            {
                return;
            }

            SelectLineInList(line);
            await RunActionAsync($"Line {line}", () => account.Softphone.SelectLineAsync(line), account.Softphone);
        }

        private void ShowHelp(bool gettingStarted)
        {
            var window = new HelpWindow(gettingStarted)
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private async Task<bool> RunActionAsync(string label, Func<Task> action, PbxSoftphoneService softphone)
        {
            try
            {
                await action();
                if (label is "Dial" or "Pick up" or "Resume" or "Intercom")
                {
                    _sounds.PlayCallConnected(_settings.PlayCallSounds);
                }

                Log(label);
                return true;
            }
            catch (Exception ex)
            {
                Log($"{label} failed: {ex.Message}");
                _sounds.PlayQuickAlert(_settings.PlayCallSounds);
                MessageBox.Show(ex.Message, $"Flex Phone - {label}", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            finally
            {
                RefreshState();
            }
        }

        private void RefreshState()
        {
            var account = SelectedAccount;
            var isLoggedIn = _accounts.Any(a => a.Softphone.IsRegistered);
            var inCall = account?.Softphone.IsInCall == true;
            var hasIncoming = account?.Softphone.HasIncomingCall == true;
            var onHold = account?.Softphone.ActiveLineState == PbxLineState.Holding;
            var muted = account?.Softphone.IsMuted == true;
            var hasActiveCall = inCall || hasIncoming || onHold;

            if (_hadActiveCall && !hasActiveCall)
            {
                ClearDialBoxAfterCall();
            }
            _hadActiveCall = hasActiveCall;

            LoginPanel.Visibility = isLoggedIn ? Visibility.Collapsed : Visibility.Visible;
            PhonePanel.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
            SettingsButton.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = account is null
                ? "Not registered"
                : $"{account.Softphone.RegistrationStatus}. {QueueStatusText(account)}";
            InstallFullVersionMenuItem.Visibility = IsInstalledProgramFilesRun() ? Visibility.Collapsed : Visibility.Visible;
            AutoCheckUpdatesMenuItem.IsChecked = _settings.CheckForUpdates;

            DialButton.IsEnabled = account?.Softphone.IsRegistered == true && !inCall && !_dndEnabled;
            AnswerButton.Visibility = hasIncoming ? Visibility.Visible : Visibility.Collapsed;
            HangupButton.Visibility = (inCall || hasIncoming || onHold) ? Visibility.Visible : Visibility.Collapsed;
            HoldButton.Visibility = inCall && !onHold ? Visibility.Visible : Visibility.Collapsed;
            ResumeButton.Visibility = onHold ? Visibility.Visible : Visibility.Collapsed;
            TransferButton.Visibility = inCall ? Visibility.Visible : Visibility.Collapsed;
            MuteButton.Visibility = inCall && !muted ? Visibility.Visible : Visibility.Collapsed;
            UnmuteButton.Visibility = inCall && muted ? Visibility.Visible : Visibility.Collapsed;
            TransferLabel.Visibility = inCall ? Visibility.Visible : Visibility.Collapsed;
            TransferDestinationBox.Visibility = inCall ? Visibility.Visible : Visibility.Collapsed;
            IntercomButton.Visibility = !inCall && !hasIncoming && _settings.AllowIntercom ? Visibility.Visible : Visibility.Collapsed;
            QueueToggleButton.Visibility = !inCall && !hasIncoming ? Visibility.Visible : Visibility.Collapsed;
            var queueIsIn = account?.QueueState == QueueState.LoggedIn;
            var queueLabel = queueIsIn ? "Queue out" : "Queue in";
            var queueCode = QueueActionCode(queueIsIn);
            QueueToggleButton.Content = $"{queueLabel} {queueCode}";
            AutomationProperties.SetName(QueueToggleButton, account is null
                ? $"Queue toggle {queueCode}"
                : $"{queueLabel} {queueCode}. {QueueStatusText(account)}");
            DndButton.Content = _dndEnabled ? "DND on" : "DND off";
            AutomationProperties.SetName(DndButton, _dndEnabled
                ? $"Do not disturb on. Toggle {_settings.DndToggleCode}"
                : $"Do not disturb off. Toggle {_settings.DndToggleCode}");
            WaitingCallButton.Visibility = !inCall ? Visibility.Visible : Visibility.Collapsed;
            VoicemailButton.Visibility = Visibility.Visible;
            VoicemailButton.Content = $"Voicemail {_settings.VoicemailCode}";
            AutomationProperties.SetName(VoicemailButton, $"Voicemail {_settings.VoicemailCode}");
            RecordButton.Visibility = inCall ? Visibility.Visible : Visibility.Collapsed;
            RecordingsButton.Visibility = Visibility.Visible;
            PeopleButton.Visibility = Visibility.Visible;
            PairButton.Visibility = Visibility.Visible;
            MessagesButton.Visibility = Visibility.Visible;
            ApplyDialPadVisibility();

            RefreshLineItems(account);
        }

        private void RefreshLineItems(PbxAccountSession? account)
        {
            var selectedLine = SelectedLineNumber();
            _refreshingLines = true;
            _lineItems.Clear();
            if (account is null)
            {
                _refreshingLines = false;
                return;
            }

            foreach (var line in account.Softphone.Lines)
            {
                _lineItems.Add(new LineViewItem
                {
                    Snapshot = new PbxLineStateSnapshot
                    {
                        AccountName = account.DisplayName,
                        LineNumber = line.LineNumber,
                        State = line.State,
                        RemoteParty = line.RemoteParty,
                        Status = line.Status,
                        IsActive = line.IsActive,
                        IsMuted = line.IsMuted
                    }
                });
            }

            SelectLineInList(selectedLine == 0 ? account.Softphone.ActiveLineNumber : selectedLine);
            _refreshingLines = false;
        }

        private void Log(string message, string? category = null)
        {
            var entry = new CallLogEntry
            {
                Timestamp = DateTime.Now,
                Category = category ?? ClassifyLogMessage(message),
                Message = message
            };
            _callLogEntries.Insert(0, entry);
            _announcements.Announce(AnnouncementText, message);
            while (_callLogEntries.Count > 200)
            {
                _callLogEntries.RemoveAt(_callLogEntries.Count - 1);
            }
            WritePersistentLog(entry);
        }

        private static void WritePersistentLog(CallLogEntry entry)
        {
            try
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FlexPhone",
                    "logs");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"flexphone-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(
                    path,
                    $"{entry.Timestamp:O}\t{entry.Category}\t{entry.Message}{Environment.NewLine}");

                if ((DateTime.Now - _lastLogCleanup).TotalHours < 12)
                {
                    return;
                }

                _lastLogCleanup = DateTime.Now;
                foreach (var file in Directory.EnumerateFiles(directory, "flexphone-*.log")
                             .Select(name => new FileInfo(name))
                             .Where(info => info.Exists && info.CreationTimeUtc < DateTime.UtcNow.AddDays(-14)))
                {
                    file.Delete();
                }
            }
            catch
            {
                // UI logging must continue even if the local diagnostics file is temporarily unavailable.
            }
        }

        private static string ClassifyLogMessage(string message)
        {
            if (message.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("error", StringComparison.OrdinalIgnoreCase)
                || message.Contains("could not", StringComparison.OrdinalIgnoreCase)
                || message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            {
                return "Errors";
            }

            if (message.Contains("SIP", StringComparison.OrdinalIgnoreCase)
                || message.Contains("registered", StringComparison.OrdinalIgnoreCase)
                || message.Contains("registration", StringComparison.OrdinalIgnoreCase))
            {
                return "SIP";
            }

            if (message.Contains("audio", StringComparison.OrdinalIgnoreCase)
                || message.Contains("microphone", StringComparison.OrdinalIgnoreCase)
                || message.Contains("speaker", StringComparison.OrdinalIgnoreCase)
                || message.Contains("ringtone", StringComparison.OrdinalIgnoreCase))
            {
                return "Audio";
            }

            if (message.Contains("call", StringComparison.OrdinalIgnoreCase)
                || message.Contains("line", StringComparison.OrdinalIgnoreCase)
                || message.Contains("dial", StringComparison.OrdinalIgnoreCase)
                || message.Contains("hold", StringComparison.OrdinalIgnoreCase)
                || message.Contains("transfer", StringComparison.OrdinalIgnoreCase)
                || message.Contains("queue", StringComparison.OrdinalIgnoreCase))
            {
                return "Calls";
            }

            if (message.Contains("account", StringComparison.OrdinalIgnoreCase)
                || message.Contains("sign", StringComparison.OrdinalIgnoreCase)
                || message.Contains("extension", StringComparison.OrdinalIgnoreCase))
            {
                return "Accounts";
            }

            if (message.Contains("update", StringComparison.OrdinalIgnoreCase)
                || message.Contains("install", StringComparison.OrdinalIgnoreCase))
            {
                return "Updates";
            }

            return "System";
        }

        private void ApplySettingsToUi()
        {
            ServerBox.Text = _settings.DefaultPbxServer;
            SelectServerPreset(ServerBox.Text);
            RememberProvisioningCheckBox.IsChecked = _settings.RememberSignIn;
            RememberExistingSignInCheckBox.IsChecked = _settings.RememberSignIn;
            ApplyDialPadVisibility();
            UpdateAccountMenuState();
            UpdateProvisioningLink();
        }

        private void ToggleDialPad()
        {
            _settings.ShowDialPad = !_settings.ShowDialPad;
            ApplyDialPadVisibility();
            SaveSettings();
            Log(_settings.ShowDialPad ? "Dialpad shown." : "Dialpad hidden. The number box can still dial numbers.");
        }

        private void ApplyDialPadVisibility()
        {
            if (!IsInitialized)
            {
                return;
            }

            DtmfKeypad.Visibility = _settings.ShowDialPad ? Visibility.Visible : Visibility.Collapsed;
            ToggleDialPadButton.Content = _settings.ShowDialPad ? "Hide dialpad" : "Show dialpad";
            AutomationProperties.SetName(ToggleDialPadButton, _settings.ShowDialPad ? "Hide dialpad" : "Show dialpad");
            AutomationProperties.SetHelpText(ToggleDialPadButton, "Control D also shows or hides the dialpad while this window is focused. The number box remains available when the dialpad is hidden.");
        }

        private void ClearDialBoxAfterCall()
        {
            if (!string.IsNullOrWhiteSpace(DestinationBox.Text))
            {
                DestinationBox.Clear();
                Log("Dial box cleared after call ended.", "Calls");
            }
        }

        private void ServerPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerPresetComboBox.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            var selectedServer = item.Tag?.ToString() ?? "";
            var isManual = string.IsNullOrWhiteSpace(selectedServer);
            ServerLabel.Visibility = isManual ? Visibility.Visible : Visibility.Collapsed;
            ServerBox.Visibility = isManual ? Visibility.Visible : Visibility.Collapsed;
            if (!isManual)
            {
                ServerBox.Text = selectedServer;
                _settings.DefaultPbxServer = selectedServer;
            }
            UpdateProvisioningLink();
        }

        private void SelectServerPreset(string server)
        {
            var normalized = server.Trim();
            var preset = normalized.Equals(TappedInPbxServer, StringComparison.OrdinalIgnoreCase)
                ? TappedInPbxServer
                : normalized.Equals(DevineCreationsPbxServer, StringComparison.OrdinalIgnoreCase)
                    ? DevineCreationsPbxServer
                    : "";

            foreach (var item in ServerPresetComboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString() ?? "", preset, StringComparison.OrdinalIgnoreCase))
                {
                    ServerPresetComboBox.SelectedItem = item;
                    ServerLabel.Visibility = string.IsNullOrWhiteSpace(preset) ? Visibility.Visible : Visibility.Collapsed;
                    ServerBox.Visibility = string.IsNullOrWhiteSpace(preset) ? Visibility.Visible : Visibility.Collapsed;
                    return;
                }
            }

            ServerPresetComboBox.SelectedIndex = 0;
        }

        private string CurrentPasswordText()
        {
            return ShowPasswordCheckBox.IsChecked == true
                ? VisiblePasswordBox.Text
                : PasswordBox.Password;
        }

        private void ShowPasswordCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowPasswordCheckBox.IsChecked == true)
            {
                VisiblePasswordBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                VisiblePasswordBox.Visibility = Visibility.Visible;
                VisiblePasswordBox.Focus();
                VisiblePasswordBox.CaretIndex = VisiblePasswordBox.Text.Length;
                return;
            }

            PasswordBox.Password = VisiblePasswordBox.Text;
            VisiblePasswordBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            PasswordBox.Focus();
        }

        private void UpdateCurrentAccountEmailText()
        {
            if (SelectedAccount is { } account && !string.IsNullOrWhiteSpace(account.Email))
            {
                CurrentAccountEmailText.Text = $"Current linked email: {account.Email}. A new email must be confirmed before Flex PBX changes the link.";
                return;
            }

            CurrentAccountEmailText.Text = "No existing linked email is shown for this account. A new email must be confirmed before Flex PBX links it.";
        }

        private void UpdateAccountMenuState()
        {
            var role = SelectedAccount?.Role ?? "";
            AdminLoginMenuItem.Visibility = IsAdminRole(role) ? Visibility.Visible : Visibility.Collapsed;
            SipAccountsMenuItem.Items.Clear();
            if (_accounts.Count == 0)
            {
                SipAccountsMenuItem.Items.Add(new MenuItem
                {
                    Header = "_No SIP accounts signed in",
                    IsEnabled = false
                });
                return;
            }

            foreach (var account in _accounts)
            {
                var item = new MenuItem
                {
                    Header = account.DisplayName.Replace("_", "__"),
                    IsCheckable = true,
                    IsChecked = ReferenceEquals(account, SelectedAccount),
                    Tag = account
                };
                AutomationProperties.SetName(item, $"Use SIP account {account.DisplayName}");
                item.Click += SipAccountMenuItem_Click;
                SipAccountsMenuItem.Items.Add(item);
            }
        }

        private void SipAccountMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: PbxAccountSession account })
            {
                AccountsListBox.SelectedItem = account;
                Log($"Using SIP account {account.DisplayName}.", "Accounts");
                DestinationBox.Focus();
            }
        }

        private static bool IsAdminRole(string role)
        {
            return role.Equals("admin", StringComparison.OrdinalIgnoreCase)
                || role.Equals("super_admin", StringComparison.OrdinalIgnoreCase)
                || role.Equals("administrator", StringComparison.OrdinalIgnoreCase)
                || role.Equals("head_admin", StringComparison.OrdinalIgnoreCase);
        }

        private string QueueActionCode(bool currentlyLoggedIn)
        {
            if (_settings.QueueUsesSingleToggleCode)
            {
                return FirstText(_settings.QueueToggleCode, _settings.QueueLoginCode, "*45");
            }

            return currentlyLoggedIn
                ? FirstText(_settings.QueueLogoutCode, _settings.QueueToggleCode, "*45")
                : FirstText(_settings.QueueLoginCode, _settings.QueueToggleCode, "*45");
        }

        private void ApplyFeatureCodes(IReadOnlyDictionary<string, string>? featureCodes)
        {
            if (featureCodes is null || featureCodes.Count == 0)
            {
                return;
            }

            var queueToggle = FeatureCode(featureCodes, "queue_toggle");
            var queueLogin = FeatureCode(featureCodes, "queue_login");
            var queueLogout = FeatureCode(featureCodes, "queue_logout");
            if (!string.IsNullOrWhiteSpace(queueToggle))
            {
                _settings.QueueToggleCode = queueToggle;
                _settings.QueueLoginCode = queueToggle;
            }
            else if (!string.IsNullOrWhiteSpace(queueLogin))
            {
                _settings.QueueLoginCode = queueLogin;
                _settings.QueueToggleCode = queueLogin;
            }

            if (!string.IsNullOrWhiteSpace(queueLogout))
            {
                _settings.QueueLogoutCode = queueLogout;
                _settings.QueueUsesSingleToggleCode = string.Equals(_settings.QueueLoginCode, queueLogout, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(_settings.QueueToggleCode, queueLogout, StringComparison.OrdinalIgnoreCase);
            }
            else if (!string.IsNullOrWhiteSpace(queueToggle))
            {
                _settings.QueueLogoutCode = queueToggle;
                _settings.QueueUsesSingleToggleCode = true;
            }

            _settings.VoicemailCode = FirstText(FeatureCode(featureCodes, "voicemail"), _settings.VoicemailCode);
            _settings.DndToggleCode = FirstText(FeatureCode(featureCodes, "dnd_toggle"), _settings.DndToggleCode);
            _settings.CallScreeningToggleCode = FirstText(FeatureCode(featureCodes, "call_screening_toggle"), _settings.CallScreeningToggleCode);
        }

        private static string FeatureCode(IReadOnlyDictionary<string, string> featureCodes, string key)
        {
            return featureCodes.TryGetValue(key, out var value) ? value.Trim() : "";
        }

        private async Task TryRememberedSignInAsync()
        {
            if (_triedRememberedSignIn || !_settings.RememberSignIn || _accounts.Count > 0 || !_credentialStore.Exists)
            {
                return;
            }

            _triedRememberedSignIn = true;
            IReadOnlyList<RememberedFlexPhoneAccount> rememberedAccounts;
            try
            {
                rememberedAccounts = _credentialStore.LoadAll()
                    .Where(account => account.CanRegister)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log($"Remembered sign-in could not be read: {ex.Message}");
                return;
            }

            if (rememberedAccounts.Count == 0)
            {
                return;
            }

            RememberProvisioningCheckBox.IsChecked = true;
            RememberExistingSignInCheckBox.IsChecked = true;
            Log(rememberedAccounts.Count == 1
                ? $"Signing in as {rememberedAccounts[0].Extension}."
                : $"Signing in to {rememberedAccounts.Count} remembered accounts.");

            for (var index = 0; index < rememberedAccounts.Count; index++)
            {
                var remembered = rememberedAccounts[index];
                ServerBox.Text = remembered.Server;
                EmailBox.Text = remembered.Email;
                ExtensionBox.Text = remembered.Extension;
                await RegisterAccountAsync(
                    remembered.Server,
                    FirstText(remembered.SipServer, remembered.Server),
                    remembered.Extension,
                    remembered.Password,
                    new FlexPhoneLoginResponse
                    {
                        Success = true,
                        Extension = remembered.Extension,
                        Username = remembered.Username,
                        Email = remembered.Email,
                        FullName = remembered.FullName,
                        SessionToken = remembered.SessionToken,
                        Role = remembered.Role,
                        Group = remembered.Group,
                        Team = remembered.Team,
                        AutoQueueSignInOut = remembered.AutoQueuePolicy
                    },
                    null,
                    replaceSelected: index == 0 && _accounts.Count == 0,
                    rememberSignIn: false);
            }
        }

        private void SaveRememberedSignIn(PbxAccountSession account)
        {
            try
            {
                _credentialStore.Save(new RememberedFlexPhoneAccount
                {
                    Server = account.Server,
                    SipServer = account.SipServer,
                    Extension = account.Extension,
                    Username = account.Username,
                    Email = account.Email,
                    Password = account.Password,
                    SessionToken = account.SessionToken,
                    FullName = account.FullName,
                    Role = account.Role,
                    Group = account.Group,
                    Team = account.Team,
                    AutoQueuePolicy = account.AutoQueuePolicy
                });
                Log("Sign-in remembered on this Windows account.");
            }
            catch (Exception ex)
            {
                Log($"Remember sign-in failed: {ex.Message}");
            }
        }

        private async Task EnsureCurrentDevicePairedAsync(PbxAccountSession account)
        {
            if (account.IsPaired || string.IsNullOrWhiteSpace(account.SessionToken))
            {
                return;
            }

            try
            {
                var result = await _pbxClient.PairCurrentDeviceAsync(account.Server, account.Extension, account.SessionToken, account.DeviceId);
                account.IsPaired = result.Success;
                if (result.Success)
                {
                    Log("This device is linked to the extension.");
                    await ReportDeviceStatusAsync();
                }
            }
            catch
            {
                // Pairing support can be added server-side without blocking normal sign-in.
            }
        }

        private void NotifyPendingUpdateSuccess()
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DevineCreations",
                "FlexPhone",
                "pending-update-success.txt");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                var version = File.ReadAllText(path).Trim();
                File.Delete(path);
                if (!string.IsNullOrWhiteSpace(version))
                {
                    Log($"Flex Phone {version} was installed successfully.");
                }
            }
            catch
            {
                // Startup should continue even if the marker cannot be read.
            }
        }

        private void PromptForPortableCleanupAfterMigration()
        {
            if (!IsInstalledProgramFilesRun())
            {
                return;
            }

            var portableDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "FlexPhone",
                "current");
            var portableExe = Path.Combine(portableDir, "FlexPhone.exe");
            if (!File.Exists(portableExe))
            {
                return;
            }

            var currentExe = Environment.ProcessPath ?? "";
            if (currentExe.StartsWith(portableDir, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var answer = MessageBox.Show(
                "Your account is signed in on the installed Flex Phone. Do you want to remove the old portable copy from Downloads now?",
                "Flex Phone - Portable copy",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                Log("Portable copy left alone.");
                return;
            }

            try
            {
                var resolvedPortableDir = Path.GetFullPath(portableDir);
                var allowedRoot = Path.GetFullPath(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads",
                    "FlexPhone"));
                if (!resolvedPortableDir.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(resolvedPortableDir, allowedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    Log("Portable cleanup skipped.");
                    return;
                }

                Directory.Delete(resolvedPortableDir, recursive: true);
                Log("Portable copy removed.");
            }
            catch (Exception ex)
            {
                Log($"Portable cleanup failed: {ex.Message}");
            }
        }

        private static bool IsInstalledProgramFilesRun()
        {
            var exe = Environment.ProcessPath ?? "";
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            return exe.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase)
                || exe.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase);
        }

        private void SaveSettings()
        {
            _settings.RememberSignIn = RememberCurrentLoginChoice;
            _settings.DefaultPbxServer = ServerBox.Text.Trim();
            _settingsService.Save(_settings);
        }

        private Uri BuildLoginUri() => _pbxClient.BuildBrowserLoginUri(
            ServerBox.Text,
            ExtensionBox.Text,
            _settings.BrowserLoginPath);

        private void UpdateProvisioningLink()
        {
            try
            {
                _ = BuildLoginUri();
            }
            catch
            {
                // Link generation is surfaced from Settings where the readonly URL lives.
            }
        }

        private NotifyIcon CreateTrayIcon()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Flex Phone", null, (_, _) => RestoreFromTray());
            menu.Items.Add("Exit", null, (_, _) =>
            {
                _isExiting = true;
                Close();
            });

            var icon = new NotifyIcon
            {
                Text = "Flex Phone",
                Icon = System.Drawing.SystemIcons.Application,
                ContextMenuStrip = menu,
                Visible = true
            };
            icon.DoubleClick += (_, _) => RestoreFromTray();
            return icon;
        }

        private void HideToTray()
        {
            SaveSettings();
            Hide();
            _trayIcon.ShowBalloonTip(1500, "Flex Phone", "Still running in the system tray.", ToolTipIcon.Info);
            _sounds.PlayQuickAlert(_settings.PlayCallSounds);
        }

        private void MinimizeFromEscape()
        {
            if (_settings.MinimizeToTray)
            {
                HideToTray();
                return;
            }

            WindowState = WindowState.Minimized;
        }

        public void RestoreForExternalActivation()
        {
            RestoreFromTray();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Focus();

            if (LoginPanel.Visibility == Visibility.Visible)
            {
                if (RequestExtensionPanel.Visibility == Visibility.Visible)
                {
                    EmailBox.Focus();
                }
                else if (ExistingSignInPanel.Visibility == Visibility.Visible)
                {
                    ExtensionBox.Focus();
                }
                else
                {
                    ShowRequestExtensionButton.Focus();
                }
            }
            else
            {
                DestinationBox.Focus();
            }
        }

        private void ShowIncomingCallWindow(PbxAccountSession account, string caller)
        {
            RestoreFromTray();

            if (_incomingCallWindow is { IsVisible: true })
            {
                _incomingCallWindow.UpdateCall(account.DisplayName, caller, () => account.Softphone.HasIncomingCall);
                _incomingCallWindow.Activate();
                return;
            }

            _incomingCallWindow = new IncomingCallWindow(
                account.DisplayName,
                caller,
                () => account.Softphone.HasIncomingCall,
                async () => await RunIncomingHotKeyActionAsync("Answer", answer: true, holdAfterAnswer: false),
                async () => await RunIncomingHotKeyActionAsync("Decline", answer: false, holdAfterAnswer: false),
                async () => await SendIncomingCallToVoicemailAsync(account),
                async destination =>
                {
                    if (string.IsNullOrWhiteSpace(destination))
                    {
                        Log("Transfer destination is required.");
                        return;
                    }

                    if (account.Softphone.HasIncomingCall)
                    {
                        await RunActionAsync("Answer for transfer", () => account.Softphone.AnswerAsync(), account.Softphone);
                        await Task.Delay(500);
                    }

                    await RunActionAsync("Transfer", () => account.Softphone.TransferAsync(account.Server, destination), account.Softphone);
                    CloseIncomingCallWindow();
                })
            {
                Owner = this
            };
            _incomingCallWindow.Closed += (_, _) => _incomingCallWindow = null;
            _incomingCallWindow.Show();
            _incomingCallWindow.Activate();
        }

        private void CloseIncomingCallWindow()
        {
            if (_incomingCallWindow is { IsVisible: true })
            {
                _incomingCallWindow.Close();
            }

            _incomingCallWindow = null;
        }

        private void RegisterGlobalCallHotKeys()
        {
            var helper = new WindowInteropHelper(this);
            if (helper.Handle == IntPtr.Zero)
            {
                return;
            }

            _hotKeySource = HwndSource.FromHwnd(helper.Handle);
            _hotKeySource?.AddHook(HandleGlobalHotKeyMessage);
            RegisterConfiguredHotKey(helper.Handle, HotKeyAnswer, _settings.AnswerHotKey, "Ctrl+Shift+A");
            RegisterConfiguredHotKey(helper.Handle, HotKeyDecline, _settings.HangupHotKey, "Ctrl+Shift+H");
            RegisterConfiguredHotKey(helper.Handle, HotKeyHold, _settings.HoldHotKey, "Ctrl+Shift+O");
        }

        private void UnregisterGlobalCallHotKeys()
        {
            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                UnregisterHotKey(helper.Handle, HotKeyAnswer);
                UnregisterHotKey(helper.Handle, HotKeyDecline);
                UnregisterHotKey(helper.Handle, HotKeyHold);
            }

            _hotKeySource?.RemoveHook(HandleGlobalHotKeyMessage);
            _hotKeySource = null;
        }

        private IntPtr HandleGlobalHotKeyMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int wmHotKey = 0x0312;
            if (msg != wmHotKey)
            {
                return IntPtr.Zero;
            }

            handled = true;
            _ = wParam.ToInt32() switch
            {
                HotKeyAnswer => RunIncomingHotKeyActionAsync("Answer", answer: true, holdAfterAnswer: false),
                HotKeyDecline => RunIncomingHotKeyActionAsync("Hang up", answer: false, holdAfterAnswer: false),
                HotKeyHold => RunIncomingHotKeyActionAsync("Hold incoming", answer: true, holdAfterAnswer: true),
                _ => Task.CompletedTask
            };
            return IntPtr.Zero;
        }

        private static void RegisterConfiguredHotKey(IntPtr handle, int id, string configuredHotKey, string fallbackHotKey)
        {
            if (!TryParseHotKey(configuredHotKey, out var modifiers, out var key)
                && !TryParseHotKey(fallbackHotKey, out modifiers, out key))
            {
                return;
            }

            RegisterHotKey(handle, id, modifiers, (uint)key);
        }

        private static bool TryParseHotKey(string hotKey, out HotKeyModifiers modifiers, out System.Windows.Forms.Keys key)
        {
            modifiers = 0;
            key = System.Windows.Forms.Keys.None;
            if (string.IsNullOrWhiteSpace(hotKey))
            {
                return false;
            }

            foreach (var rawPart in hotKey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var part = rawPart.Trim();
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                    || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= HotKeyModifiers.Control;
                }
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= HotKeyModifiers.Alt;
                }
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= HotKeyModifiers.Shift;
                }
                else if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
                {
                    key = Enum.Parse<System.Windows.Forms.Keys>(part.ToUpperInvariant());
                }
                else if (Enum.TryParse<System.Windows.Forms.Keys>(part, true, out var parsed))
                {
                    key = parsed;
                }
            }

            return modifiers != 0 && key != System.Windows.Forms.Keys.None;
        }

        private static bool HotKeyMatches(string actualHotKey, string configuredHotKey, string fallbackHotKey)
        {
            return HotKeyTextMatches(actualHotKey, configuredHotKey)
                || HotKeyTextMatches(actualHotKey, fallbackHotKey);
        }

        private static bool HotKeyTextMatches(string actualHotKey, string configuredHotKey)
        {
            return !string.IsNullOrWhiteSpace(configuredHotKey)
                && string.Equals(NormalizeHotKeyText(actualHotKey), NormalizeHotKeyText(configuredHotKey), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeHotKeyText(string hotKey)
        {
            return hotKey.Replace("Control", "Ctrl", StringComparison.OrdinalIgnoreCase)
                .Replace(" ", "", StringComparison.OrdinalIgnoreCase);
        }

        private static string HotKeyText(System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.None
                || key == Key.LeftCtrl
                || key == Key.RightCtrl
                || key == Key.LeftShift
                || key == Key.RightShift
                || key == Key.LeftAlt
                || key == Key.RightAlt)
            {
                return "";
            }

            var parts = new List<string>();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                parts.Add("Ctrl");
            }

            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                parts.Add("Alt");
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                parts.Add("Shift");
            }

            if (parts.Count == 0)
            {
                return "";
            }

            parts.Add(key.ToString().ToUpperInvariant());
            return string.Join("+", parts);
        }

        private async Task RunIncomingHotKeyActionAsync(string label, bool answer, bool holdAfterAnswer)
        {
            var account = _accounts.FirstOrDefault(item => item.Softphone.HasIncomingCall)
                ?? SelectedAccount;
            if (account is null)
            {
                ShowFlexPhoneNotification("Flex Phone", "There is no incoming call waiting.", ToolTipIcon.Info);
                return;
            }

            try
            {
                var hasIncomingCall = account.Softphone.HasIncomingCall;
                if (answer)
                {
                    if (hasIncomingCall)
                    {
                        await RunActionAsync(label, () => account.Softphone.AnswerAsync(), account.Softphone);
                        if (holdAfterAnswer)
                        {
                            await RunActionAsync("Hold", () => account.Softphone.HoldAsync(), account.Softphone);
                        }
                    }
                    else if (holdAfterAnswer)
                    {
                        await RunActionAsync("Hold", () => account.Softphone.HoldAsync(), account.Softphone);
                    }
                    else
                    {
                        ShowFlexPhoneNotification("Flex Phone", "There is no incoming call waiting.", ToolTipIcon.Info);
                    }
                }
                else
                {
                    await RunActionAsync(label, () => account.Softphone.HangupAsync(), account.Softphone);
                }
            }
            catch (Exception ex)
            {
                Log($"{label} hotkey failed: {ex.Message}");
            }
            finally
            {
                RefreshState();
                if (!_accounts.Any(item => item.Softphone.HasIncomingCall))
                {
                    CloseIncomingCallWindow();
                }
            }
        }

        private bool IsMinimizedOrHidden()
        {
            return !IsVisible || WindowState == WindowState.Minimized;
        }

        [Flags]
        private enum HotKeyModifiers : uint
        {
            Alt = 0x0001,
            Control = 0x0002,
            Shift = 0x0004
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, HotKeyModifiers fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private void ShowFlexPhoneNotification(string title, string message, ToolTipIcon icon, bool playNetworkTone = false)
        {
            _trayIcon.ShowBalloonTip(2500, title, message, icon);
            if (playNetworkTone)
            {
                _sounds.PlayNetworkChange(_settings.PlayCallSounds);
            }
            else
            {
                _sounds.PlayQuickAlert(_settings.PlayCallSounds);
            }
        }

        private int SelectedLineNumber()
        {
            if (LinesList.SelectedItem is LineViewItem item)
            {
                return item.LineNumber;
            }

            return SelectedAccount?.Softphone.FirstFreeLineNumber ?? 1;
        }

        private void SelectLineInList(int lineNumber)
        {
            var item = _lineItems.FirstOrDefault(line => line.LineNumber == lineNumber);
            if (item is not null && !Equals(LinesList.SelectedItem, item))
            {
                LinesList.SelectedItem = item;
                LinesList.ScrollIntoView(item);
            }
        }

        private static bool IsTextInputFocused()
        {
            return Keyboard.FocusedElement is System.Windows.Controls.TextBox
                or System.Windows.Controls.PasswordBox
                or System.Windows.Controls.ComboBox;
        }

        private bool IsMenuInteractionFocused()
        {
            if (Keyboard.FocusedElement is not DependencyObject focused)
            {
                return false;
            }

            var current = focused;
            while (current is not null)
            {
                if (ReferenceEquals(current, MainMenu)
                    || current is MenuItem
                    || current is System.Windows.Controls.ContextMenu)
                {
                    return true;
                }

                current = GetFocusParent(current);
            }

            return false;
        }

        private static DependencyObject? GetFocusParent(DependencyObject current)
        {
            if (current is FrameworkElement frameworkElement && frameworkElement.Parent is DependencyObject frameworkParent)
            {
                return frameworkParent;
            }

            if (current is FrameworkContentElement contentElement && contentElement.Parent is DependencyObject contentParent)
            {
                return contentParent;
            }

            try
            {
                return System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            catch (InvalidOperationException)
            {
                return LogicalTreeHelper.GetParent(current);
            }
        }

        private static bool TryActivateFocusedButton()
        {
            if (Keyboard.FocusedElement is not System.Windows.Controls.Button button
                || !button.IsEnabled
                || !button.IsVisible)
            {
                return false;
            }

            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
            return true;
        }

        private void MoveFocusWithinActiveView(bool backwards)
        {
            var root = LoginPanel.Visibility == Visibility.Visible
                ? (FrameworkElement)LoginPanel
                : PhonePanel;

            if (Keyboard.FocusedElement is UIElement current && IsDescendantOf(current, root))
            {
                var request = new TraversalRequest(backwards ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next)
                {
                    Wrapped = true
                };
                current.MoveFocus(request);

                if (Keyboard.FocusedElement is DependencyObject moved && IsDescendantOf(moved, root))
                {
                    return;
                }
            }

            FocusFirstTabStop(root, backwards);
        }

        private bool FocusFirstTabStop(DependencyObject root, bool backwards)
        {
            var candidates = new List<UIElement>();
            CollectTabStops(root, candidates);
            if (backwards)
            {
                candidates.Reverse();
            }

            foreach (var candidate in candidates)
            {
                if (candidate.Focus())
                {
                    return true;
                }
            }

            return false;
        }

        private void CollectTabStops(DependencyObject node, List<UIElement> candidates)
        {
            if (node is UIElement element && IsMainViewTabStop(element))
            {
                candidates.Add(element);
            }

            var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
            for (var index = 0; index < childCount; index++)
            {
                CollectTabStops(System.Windows.Media.VisualTreeHelper.GetChild(node, index), candidates);
            }
        }

        private bool IsMainViewTabStop(UIElement element)
        {
            if (!element.Focusable || !element.IsEnabled || !element.IsVisible)
            {
                return false;
            }

            if (ReferenceEquals(element, MainMenu)
                || ReferenceEquals(element, SettingsButton))
            {
                return false;
            }

            return element is not System.Windows.Controls.Control control || control.IsTabStop;
        }

        private bool IsKeyboardFocusInKeypad()
        {
            return Keyboard.FocusedElement is DependencyObject focused
                && IsDescendantOf(focused, DtmfKeypad);
        }

        private static bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            var current = child;
            while (current is not null)
            {
                if (ReferenceEquals(current, parent))
                {
                    return true;
                }

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void FocusKeypadButton(int index)
        {
            if (index < 0 || index >= DtmfKeypad.Children.Count)
            {
                return;
            }

            if (DtmfKeypad.Children[index] is System.Windows.Controls.Button button)
            {
                button.Focus();
                if (button.Content?.ToString() is { Length: > 0 } label)
                {
                    Log($"Keypad {label}.");
                }
            }
        }

        private static bool TryMapKeyboardDigit(Key key, out char digit)
        {
            digit = key switch
            {
                Key.D0 or Key.NumPad0 => '0',
                Key.D1 or Key.NumPad1 => '1',
                Key.D2 or Key.NumPad2 => '2',
                Key.D3 or Key.NumPad3 => '3',
                Key.D4 or Key.NumPad4 => '4',
                Key.D5 or Key.NumPad5 => '5',
                Key.D6 or Key.NumPad6 => '6',
                Key.D7 or Key.NumPad7 => '7',
                Key.D8 or Key.NumPad8 => '8',
                Key.D9 or Key.NumPad9 => '9',
                Key.Multiply => '*',
                _ => '\0'
            };

            if (Keyboard.Modifiers == ModifierKeys.Shift && key == Key.D8)
            {
                digit = '*';
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift && key == Key.D3)
            {
                digit = '#';
            }

            return digit != '\0';
        }

        private static string QueueStatusText(PbxAccountSession account)
        {
            var state = account.QueueState == QueueState.LoggedIn ? "logged in to" : "logged out of";
            var duration = FormatDuration(DateTime.Now - account.QueueStateChangedAt);
            return $"You are {state} the call queue. You have been {state} for {duration}.";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes < 1)
            {
                return "less than 1 minute";
            }

            var parts = new List<string>();
            if (duration.Days > 0) parts.Add($"{duration.Days} day{(duration.Days == 1 ? "" : "s")}");
            if (duration.Hours > 0) parts.Add($"{duration.Hours} hour{(duration.Hours == 1 ? "" : "s")}");
            if (duration.Minutes > 0) parts.Add($"{duration.Minutes} minute{(duration.Minutes == 1 ? "" : "s")}");
            return string.Join(", ", parts);
        }

        private string GetOrCreateDeviceId()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevineCreations", "FlexPhone");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "device-id.txt");
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    return existing;
                }
            }

            var id = "flexphone-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(path, id);
            return id;
        }

        private async Task ReportDeviceStatusAsync()
        {
            if (SelectedAccount is not { } account || string.IsNullOrWhiteSpace(account.SessionToken))
            {
                return;
            }

            try
            {
                await _pbxClient.ReportDeviceStatusAsync(account.Server, account.SessionToken, new
                {
                    action = "device_status",
                    extension = account.Extension,
                    display_name = account.FullName,
                    email = account.Email,
                    role = account.Role,
                    group = account.Group,
                    team = account.Team,
                    device_id = account.DeviceId,
                    paired = account.IsPaired,
                    linked_device_ring_policy = "all_linked_devices",
                    linked_device_transfer_behavior = "hold_caller_until_answered",
                    device_name = Environment.MachineName,
                    app_version = FlexPbxClient.GetCurrentVersion(),
                    os = Environment.OSVersion.VersionString,
                    queue_state = account.QueueState == QueueState.LoggedIn ? "in" : "out",
                    auto_queue_sign_in_out = FirstText(account.AutoQueuePolicy, _settings.AutoQueueSignInOutMode),
                    queue_state_changed_at = account.QueueStateChangedAt.ToUniversalTime().ToString("O"),
                    queue_state_age_seconds = (int)(DateTime.Now - account.QueueStateChangedAt).TotalSeconds,
                    active_line = account.Softphone.ActiveLineNumber,
                    active_call_count = account.Softphone.ActiveCallCount,
                    line_count = account.Softphone.Lines.Count,
                    registered_at = account.RegisteredAt.ToUniversalTime().ToString("O"),
                    lines = account.Softphone.Lines.Select(line => new
                    {
                        line = line.LineNumber,
                        state = line.State.ToString(),
                        remote = line.RemoteParty,
                        active = line.IsActive
                    }).ToArray()
                });
            }
            catch
            {
                // Device heartbeats must never interrupt calls.
            }
        }

        private async Task CheckForUpdatesInBackgroundAsync(bool interactive, bool force = false)
        {
            if (!_settings.CheckForUpdates && !force)
            {
                return;
            }

            try
            {
                var manifest = await _pbxClient.GetUpdateManifestAsync(ServerBox.Text, _settings.UpdateManifestPath);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.EffectiveVersion) || string.IsNullOrWhiteSpace(manifest.ResolvedDownloadUrl))
                {
                    if (interactive) Log("No valid Flex Phone update information was found.");
                    return;
                }

                if (!IsNewerVersion(manifest.EffectiveVersion, FlexPbxClient.GetCurrentVersion()))
                {
                    if (interactive) Log($"Flex Phone is up to date. Current version {FlexPbxClient.GetCurrentVersion()}.");
                    return;
                }

                if (_settings.UpdatePostponedUntil > DateTime.Now && _settings.UpdatePostponeCount < MaxUpdatePostpones && !manifest.Critical)
                {
                    if (interactive) Log($"Flex Phone update is postponed until {_settings.UpdatePostponedUntil:g}.");
                    return;
                }

                if (HasActiveCalls())
                {
                    _pendingUpdateManifest = manifest;
                    ShowFlexPhoneNotification("Flex Phone Update", $"Flex Phone {manifest.EffectiveVersion} is ready and will wait until your call ends.", ToolTipIcon.Info);
                    Log($"Flex Phone {manifest.EffectiveVersion} is ready. Update postponed until calls are finished.");
                    return;
                }

                await PromptAndMaybeInstallUpdateAsync(manifest, afterCall: false);
            }
            catch (Exception ex)
            {
                if (interactive)
                {
                    Log($"Update check failed: {FriendlyNetworkError(ex, ServerBox.Text)}");
                }
            }
        }

        private async Task TryRunPendingUpdateAfterCallAsync()
        {
            if (_updateInstallInProgress || _pendingUpdateManifest is null || HasActiveCalls())
            {
                return;
            }

            var manifest = _pendingUpdateManifest;
            _pendingUpdateManifest = null;
            ShowFlexPhoneNotification("Flex Phone Update", $"Your call ended. Flex Phone {manifest.EffectiveVersion} can install now.", ToolTipIcon.Info);
            await PromptAndMaybeInstallUpdateAsync(manifest, afterCall: true);
        }

        private async Task PromptAndMaybeInstallUpdateAsync(FlexPhoneUpdateManifest manifest, bool afterCall)
        {
            if (_updateInstallInProgress)
            {
                return;
            }

            if (HasActiveCalls())
            {
                _pendingUpdateManifest = manifest;
                ShowFlexPhoneNotification("Flex Phone Update", $"Flex Phone {manifest.EffectiveVersion} will wait until your call ends.", ToolTipIcon.Info);
                return;
            }

            if (_settings.AutomaticallyInstallUpdates
                && _settings.UpdatePostponeCount >= MaxUpdatePostpones)
            {
                ShowFlexPhoneNotification("Flex Phone Update", $"Installing Flex Phone {manifest.EffectiveVersion}.", ToolTipIcon.Info);
                await DownloadAndInstallUpdateAsync(manifest, launchInstalledAfterInstall: false);
                return;
            }

            var dialog = new UpdateWindow(manifest, FlexPbxClient.GetCurrentVersion(), _settings.UpdatePostponeCount, MaxUpdatePostpones)
            {
                Owner = this
            };
            if (afterCall)
            {
                Log("Update is ready now that the call is over.");
            }

            dialog.ShowDialog();

            if (manifest.Critical
                || _settings.AutomaticallyInstallUpdates
                || _settings.UpdatePostponeCount >= MaxUpdatePostpones)
            {
                _settings.UpdatePostponeCount = 0;
                _settings.UpdatePostponedUntil = DateTime.MinValue;
                _settingsService.Save(_settings);
                ShowFlexPhoneNotification("Flex Phone Update", $"Installing Flex Phone {manifest.EffectiveVersion}.", ToolTipIcon.Info);
                await DownloadAndInstallUpdateAsync(manifest, launchInstalledAfterInstall: false);
            }
        }

        private bool HasActiveCalls()
        {
            return _accounts.Any(account => account.Softphone.ActiveCallCount > 0 || account.Softphone.HasIncomingCall);
        }

        private async Task DownloadAndInstallUpdateAsync(FlexPhoneUpdateManifest manifest, bool launchInstalledAfterInstall)
        {
            if (HasActiveCalls())
            {
                _pendingUpdateManifest = manifest;
                ShowFlexPhoneNotification("Flex Phone Update", $"Flex Phone {manifest.EffectiveVersion} will install after your call ends.", ToolTipIcon.Info);
                return;
            }

            _updateInstallInProgress = true;
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevineCreations", "FlexPhone");
            var updatesDir = Path.Combine(root, "updates");
            Directory.CreateDirectory(updatesDir);
            var downloadUri = ResolveDownloadUri(manifest.ResolvedDownloadUrl);
            var fileName = string.IsNullOrWhiteSpace(manifest.FileName)
                ? Path.GetFileName(downloadUri.LocalPath)
                : manifest.FileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"FlexPhone-Setup-{manifest.EffectiveVersion}.exe";
            }

            var installerPath = Path.Combine(updatesDir, fileName);
            var tempPath = installerPath + ".download";
            Log($"Downloading Flex Phone {manifest.EffectiveVersion}.");
            using var http = new HttpClient();
            await using (var remote = await http.GetStreamAsync(downloadUri))
            await using (var local = File.Create(tempPath))
            {
                await remote.CopyToAsync(local);
            }

            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                await using var stream = File.OpenRead(tempPath);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
                if (!hash.Equals(manifest.Sha256.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tempPath);
                    throw new InvalidOperationException("The downloaded update did not match the expected checksum.");
                }
            }

            if (File.Exists(installerPath)) File.Delete(installerPath);
            File.Move(tempPath, installerPath);
            File.WriteAllText(Path.Combine(root, "pending-update-success.txt"), manifest.EffectiveVersion);
            if (_settings.AnnounceUpdateInstallRestart)
            {
                Log($"Flex Phone {manifest.EffectiveVersion} will be installed now. Flex Phone will restart when the update is complete.");
            }
            var relaunchPath = launchInstalledAfterInstall
                ? GetInstalledAppPath()
                : Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "FlexPhone.exe");
            StartInstallerAfterExit(updatesDir, installerPath, relaunchPath);
            _isExiting = true;
            Close();
        }

        private Uri ResolveDownloadUri(string urlOrPath)
        {
            return Uri.TryCreate(urlOrPath, UriKind.Absolute, out var absolute)
                ? absolute
                : _pbxClient.BuildDownloadUri(ServerBox.Text, urlOrPath);
        }

        private static string FriendlyNetworkError(Exception ex, string server)
        {
            if (ex is TaskCanceledException or TimeoutException)
            {
                var target = string.IsNullOrWhiteSpace(server) ? "the selected Flex PBX server" : server.Trim();
                return $"The phone system at {target} did not respond in time. Check the PBX domain, network connection, or try again after the server route is restored.";
            }

            if (ex is HttpRequestException)
            {
                var target = string.IsNullOrWhiteSpace(server) ? "the selected Flex PBX server" : server.Trim();
                return $"Flex Phone could not reach {target}. Check the PBX domain, network connection, or server route.";
            }

            return ex.Message;
        }

        private static string FriendlySipRegistrationError(Exception ex)
        {
            if (ex.Message.Contains("401 Unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                return "Phone registration was rejected by the PBX. Flex Phone signs in with your portal password, then uses phone registration credentials returned by the server. Refresh account credentials or contact support if this continues.";
            }

            return ex.Message;
        }

        private static string GetInstalledAppPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Flex Phone",
                "FlexPhone.exe");
        }

        private static void StartInstallerAfterExit(string updatesDir, string installerPath, string appPath)
        {
            var scriptPath = Path.Combine(updatesDir, "run-flexphone-update.ps1");
            var processId = Environment.ProcessId;
            File.WriteAllText(scriptPath,
                "param([int]$ProcessId, [string]$InstallerPath, [string]$AppPath)\r\n" +
                "try { Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction SilentlyContinue } catch { }\r\n" +
                "$args = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS'\r\n" +
                "$identity = [Security.Principal.WindowsIdentity]::GetCurrent()\r\n" +
                "$principal = New-Object Security.Principal.WindowsPrincipal($identity)\r\n" +
                "$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)\r\n" +
                "if ($isAdmin) {\r\n" +
                "    Start-Process -FilePath $InstallerPath -ArgumentList $args -Wait\r\n" +
                "} else {\r\n" +
                "    Start-Process -FilePath $InstallerPath -ArgumentList $args -Verb RunAs -Wait\r\n" +
                "}\r\n" +
                "if (Test-Path $AppPath) { Start-Process -FilePath $AppPath }\r\n");
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -ProcessId {processId} -InstallerPath \"{installerPath}\" -AppPath \"{appPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            });
        }

        private static bool IsNewerVersion(string candidate, string current)
        {
            return Version.TryParse(candidate, out var candidateVersion)
                && Version.TryParse(current, out var currentVersion)
                && candidateVersion > currentVersion;
        }

        private static string FirstText(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }
    }
}
