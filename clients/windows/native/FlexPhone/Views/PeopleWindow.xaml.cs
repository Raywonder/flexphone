using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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
        private readonly FlexPhoneContactsService _contactsService = new();
        private readonly ObservableCollection<ContactRow> _rows = [];
        private readonly List<FlexPhonePresenceInfo> _directory = [];
        private readonly List<FlexPhoneContact> _contacts = [];
        private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(45) };
        private readonly bool _directoryMode;
        private bool _isRefreshing;
        private string _selectedGroup = "__all";

        public PeopleWindow(PbxAccountSession account, FlexPbxClient pbxClient, IEnumerable<FlexPhonePresenceInfo> people, bool directoryMode = false)
        {
            InitializeComponent();
            _account = account;
            _pbxClient = pbxClient;
            _directoryMode = directoryMode;
            ContactsGrid.ItemsSource = _rows;
            _directory.AddRange(people);
            _contacts.AddRange(_contactsService.Load());
            ConfigureMode();
            RebuildGroups();
            RebuildRows(announce: true);
            ContactsGrid.SelectionChanged += (_, _) => RefreshActionState();
            ContactsGrid.ContextMenuOpening += (_, _) => RefreshActionState();
            _refreshTimer.Tick += async (_, _) => await RefreshDirectoryAsync(announce: false);
            Loaded += (_, _) =>
            {
                if (_rows.Count > 0)
                {
                    ContactsGrid.SelectedIndex = 0;
                    ContactsGrid.Focus();
                }

                _refreshTimer.Start();
            };
            Closed += (_, _) => _refreshTimer.Stop();
        }

        private ContactRow? SelectedRow => ContactsGrid.SelectedItem as ContactRow;

        private void ConfigureMode()
        {
            if (_directoryMode)
            {
                Title = "Directory";
                StatusText.Text = "Directory";
                AutomationProperties.SetName(this, "Flex Phone directory");
                AutomationProperties.SetName(StatusText, "Directory status");
                AutomationProperties.SetName(ContactsGrid, "Directory table");
                AddContactButton.Visibility = Visibility.Collapsed;
                EditContactButton.Visibility = Visibility.Collapsed;
                RemoveContactButton.Visibility = Visibility.Collapsed;
                ContextEditContactMenuItem.Visibility = Visibility.Collapsed;
                ContextRemoveContactMenuItem.Visibility = Visibility.Collapsed;
            }
            else
            {
                Title = "Contacts";
                StatusText.Text = "Contacts";
                SaveDirectoryContactButton.Visibility = Visibility.Collapsed;
                ContextSaveDirectoryMenuItem.Visibility = Visibility.Collapsed;
            }
        }

        private async Task RefreshDirectoryAsync(bool announce)
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            var selectedKey = SelectedRow?.Key;
            try
            {
                if (announce)
                {
                    StatusText.Text = _directoryMode ? "Refreshing directory." : "Refreshing contact status.";
                }

                var result = await _pbxClient.GetPresenceAsync(_account.Server, _account.Extension, _account.SessionToken);
                if (!result.Success)
                {
                    if (announce)
                    {
                        StatusText.Text = FirstText(result.Error, result.Message, "Directory is not available right now.");
                    }

                    return;
                }

                _directory.Clear();
                _directory.AddRange(result.People);
                RebuildGroups();
                RebuildRows(announce);
                RestoreSelectedRow(selectedKey);
            }
            catch (Exception ex)
            {
                if (announce)
                {
                    StatusText.Text = $"Refresh failed: {ex.Message}";
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshDirectoryAsync(announce: true);
        }

        private void AddContactButton_Click(object sender, RoutedEventArgs e)
        {
            var contact = new FlexPhoneContact { Group = SelectedGroupForNewContact() };
            if (ContactEditDialog.Show(this, contact, "Add contact"))
            {
                _contacts.Add(contact);
                SaveContacts();
                RebuildGroups();
                RebuildRows(announce: true);
                RestoreSelectedRow(contact.Id);
            }
        }

        private void EditContactButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRow?.Contact is not { } contact)
            {
                StatusText.Text = "Choose a saved contact first.";
                return;
            }

            var editable = CopyContact(contact);
            if (ContactEditDialog.Show(this, editable, "Edit contact"))
            {
                contact.DisplayName = editable.DisplayName;
                contact.Extension = editable.Extension;
                contact.PhoneNumber = editable.PhoneNumber;
                contact.Email = editable.Email;
                contact.Group = editable.Group;
                contact.Notes = editable.Notes;
                contact.IsFavorite = editable.IsFavorite;
                SaveContacts();
                RebuildGroups();
                RebuildRows(announce: true);
                RestoreSelectedRow(contact.Id);
            }
        }

        private void RemoveContactButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRow?.Contact is not { } contact)
            {
                StatusText.Text = "Choose a saved contact first.";
                return;
            }

            var name = ContactName(contact);
            var confirm = MessageBox.Show($"Remove {name} from contacts?", "Flex Phone - Contacts", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            _contacts.Remove(contact);
            SaveContacts();
            RebuildGroups();
            RebuildRows(announce: true);
        }

        private void SaveDirectoryContactButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRow?.DirectoryEntry is not { } person)
            {
                StatusText.Text = "Choose a directory entry first.";
                return;
            }

            var existing = _contacts.FirstOrDefault(contact =>
                !string.IsNullOrWhiteSpace(contact.Extension)
                && contact.Extension.Equals(person.Extension, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                StatusText.Text = $"{ContactName(existing)} is already saved in contacts.";
                return;
            }

            var contact = FlexPhoneContact.FromPresence(person);
            if (ContactEditDialog.Show(this, contact, "Save to contacts"))
            {
                _contacts.Add(contact);
                SaveContacts();
                RebuildGroups();
                RebuildRows(announce: true);
                RestoreSelectedRow(contact.Id);
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
                MessageBox.Show("Enter the message first, then use Send text message.", "Flex Phone - Contacts", MessageBoxButton.OK, MessageBoxImage.Information);
                MessageTextBox.Focus();
                return;
            }

            await SendPresenceActionAsync("text", "Text message", message);
        }

        private async void SendVoicemailMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await SendPresenceActionAsync("voicemail", "Voicemail", MessageTextBox.Text.Trim());
        }

        private async void ContactsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedRow?.IsOnline == true)
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

        private void GroupsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (GroupsTree.SelectedItem is TreeViewItem item && item.Tag is string tag)
            {
                _selectedGroup = tag;
                RebuildRows(announce: true);
                if (_rows.Count > 0)
                {
                    ContactsGrid.SelectedIndex = 0;
                    ContactsGrid.Focus();
                }
            }
        }

        private void ContactsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshActionState();
        }

        private async Task SendPresenceActionAsync(string action, string label, string message = "")
        {
            var row = SelectedRow;
            var target = row?.Extension;
            if (string.IsNullOrWhiteSpace(target))
            {
                StatusText.Text = "Choose a contact or directory entry with an extension first.";
                return;
            }

            try
            {
                StatusText.Text = $"{label} request for {row!.DisplayName}.";
                var result = await _pbxClient.SendPresenceActionAsync(
                    _account.Server,
                    _account.Extension,
                    _account.SessionToken,
                    target,
                    action,
                    message);
                StatusText.Text = result.Success
                    ? FirstText(result.Message, $"{label} request sent to {row.DisplayName}.")
                    : FirstText(result.Error, result.Message, $"{label} is not available for {row.DisplayName} right now.");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"{label} failed: {ex.Message}";
            }
        }

        private void RebuildGroups()
        {
            var current = _selectedGroup;
            GroupsTree.Items.Clear();
            AddGroup("All contacts", "__all", current == "__all");
            AddGroup("Favorites", "__favorites", current == "__favorites");

            var groups = (_directoryMode
                    ? _directory.Select(person => string.IsNullOrWhiteSpace(person.Role) ? "Directory" : person.Role)
                    : _contacts.Select(contact => string.IsNullOrWhiteSpace(contact.Group) ? "General" : contact.Group))
                .Append("General")
                .Where(group => !string.IsNullOrWhiteSpace(group))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group);

            foreach (var group in groups)
            {
                AddGroup(group, group, current.Equals(group, StringComparison.OrdinalIgnoreCase));
            }

            if (GroupsTree.SelectedItem is null && GroupsTree.Items.Count > 0)
            {
                ((TreeViewItem)GroupsTree.Items[0]).IsSelected = true;
            }
        }

        private void AddGroup(string header, string tag, bool selected)
        {
            GroupsTree.Items.Add(new TreeViewItem
            {
                Header = header,
                Tag = tag,
                IsSelected = selected
            });
        }

        private void RebuildRows(bool announce)
        {
            _rows.Clear();
            var rows = _directoryMode ? BuildDirectoryRows() : BuildContactRows();
            foreach (var row in FilterRows(rows))
            {
                _rows.Add(row);
            }

            if (announce)
            {
                var noun = _directoryMode ? "directory entries" : "contacts";
                StatusText.Text = _rows.Count == 0
                    ? $"No {noun} found for the selected group."
                    : $"{_rows.Count} {noun}. Use the context menu or buttons for actions. Press Escape to close.";
            }

            RefreshActionState();
        }

        private IEnumerable<ContactRow> BuildContactRows()
        {
            foreach (var contact in _contacts)
            {
                var directoryEntry = FindDirectoryEntry(contact.Extension);
                yield return ContactRow.FromContact(contact, directoryEntry);
            }
        }

        private IEnumerable<ContactRow> BuildDirectoryRows()
        {
            return _directory
                .OrderByDescending(person => person.IsOnline)
                .ThenBy(person => string.IsNullOrWhiteSpace(person.DisplayName) ? person.Extension : person.DisplayName)
                .Select(ContactRow.FromDirectory);
        }

        private IEnumerable<ContactRow> FilterRows(IEnumerable<ContactRow> rows)
        {
            return _selectedGroup switch
            {
                "__all" => rows,
                "__favorites" => rows.Where(row => row.IsFavorite),
                _ => rows.Where(row => row.Group.Equals(_selectedGroup, StringComparison.OrdinalIgnoreCase))
            };
        }

        private void RefreshActionState()
        {
            var row = SelectedRow;
            var hasExtension = row is not null && !string.IsNullOrWhiteSpace(row.Extension);
            var isOnline = hasExtension && row!.IsOnline;
            CallMenuItem.IsEnabled = isOnline;
            IntercomMenuItem.IsEnabled = isOnline;
            EditContactButton.IsEnabled = !_directoryMode && row?.Contact is not null;
            RemoveContactButton.IsEnabled = !_directoryMode && row?.Contact is not null;
            SaveDirectoryContactButton.IsEnabled = _directoryMode && row?.DirectoryEntry is not null;
            ContextEditContactMenuItem.IsEnabled = EditContactButton.IsEnabled;
            ContextRemoveContactMenuItem.IsEnabled = RemoveContactButton.IsEnabled;
            ContextSaveDirectoryMenuItem.IsEnabled = SaveDirectoryContactButton.IsEnabled;
        }

        private void RestoreSelectedRow(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var row = _rows.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (row is not null)
            {
                ContactsGrid.SelectedItem = row;
                ContactsGrid.ScrollIntoView(row);
            }
        }

        private FlexPhonePresenceInfo? FindDirectoryEntry(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return null;
            }

            return _directory.FirstOrDefault(person => person.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        private void SaveContacts()
        {
            _contactsService.Save(_contacts);
        }

        private string SelectedGroupForNewContact()
        {
            return _selectedGroup is "__all" or "__favorites" ? "General" : _selectedGroup;
        }

        private static FlexPhoneContact CopyContact(FlexPhoneContact contact)
        {
            return new FlexPhoneContact
            {
                Id = contact.Id,
                DisplayName = contact.DisplayName,
                Extension = contact.Extension,
                PhoneNumber = contact.PhoneNumber,
                Email = contact.Email,
                Group = contact.Group,
                Notes = contact.Notes,
                IsFavorite = contact.IsFavorite
            };
        }

        private static string ContactName(FlexPhoneContact contact)
        {
            return string.IsNullOrWhiteSpace(contact.DisplayName) ? FirstText(contact.Extension, contact.PhoneNumber, contact.Email, "contact") : contact.DisplayName;
        }

        private static string FirstText(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }

        private sealed class ContactRow
        {
            public string Key { get; init; } = "";
            public string DisplayName { get; init; } = "";
            public string Extension { get; init; } = "";
            public string PhoneNumber { get; init; } = "";
            public string Email { get; init; } = "";
            public string Group { get; init; } = "";
            public string Status { get; init; } = "";
            public bool IsFavorite { get; init; }
            public bool IsOnline { get; init; }
            public FlexPhoneContact? Contact { get; init; }
            public FlexPhonePresenceInfo? DirectoryEntry { get; init; }

            public static ContactRow FromContact(FlexPhoneContact contact, FlexPhonePresenceInfo? directoryEntry)
            {
                return new ContactRow
                {
                    Key = contact.Id,
                    DisplayName = string.IsNullOrWhiteSpace(contact.DisplayName) ? FirstText(contact.Extension, contact.PhoneNumber, contact.Email, "Unnamed contact") : contact.DisplayName,
                    Extension = contact.Extension,
                    PhoneNumber = contact.PhoneNumber,
                    Email = contact.Email,
                    Group = string.IsNullOrWhiteSpace(contact.Group) ? "General" : contact.Group,
                    Status = directoryEntry?.Status ?? "Saved contact",
                    IsFavorite = contact.IsFavorite,
                    IsOnline = directoryEntry?.IsOnline == true,
                    Contact = contact,
                    DirectoryEntry = directoryEntry
                };
            }

            public static ContactRow FromDirectory(FlexPhonePresenceInfo person)
            {
                return new ContactRow
                {
                    Key = $"directory:{person.Extension}",
                    DisplayName = string.IsNullOrWhiteSpace(person.DisplayName) ? FirstText(person.Extension, "Unknown user") : person.DisplayName,
                    Extension = person.Extension,
                    Group = string.IsNullOrWhiteSpace(person.Role) ? "Directory" : person.Role,
                    Status = string.IsNullOrWhiteSpace(person.Status) ? "status unknown" : person.Status,
                    IsOnline = person.IsOnline,
                    DirectoryEntry = person
                };
            }
        }
    }

    internal sealed class ContactEditDialog : Window
    {
        private readonly System.Windows.Controls.TextBox _nameBox = new();
        private readonly System.Windows.Controls.TextBox _extensionBox = new();
        private readonly System.Windows.Controls.TextBox _phoneBox = new();
        private readonly System.Windows.Controls.TextBox _emailBox = new();
        private readonly System.Windows.Controls.TextBox _groupBox = new();
        private readonly System.Windows.Controls.TextBox _notesBox = new();
        private readonly System.Windows.Controls.CheckBox _favoriteBox = new() { Content = "Favorite" };
        private bool _accepted;

        private ContactEditDialog(FlexPhoneContact contact, string title)
        {
            Title = title;
            Width = 520;
            Height = 560;
            MinWidth = 420;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;
            Foreground = System.Windows.Media.Brushes.Black;
            AutomationProperties.SetName(this, $"Flex Phone {title.ToLowerInvariant()}");

            _nameBox.Text = contact.DisplayName;
            _extensionBox.Text = contact.Extension;
            _phoneBox.Text = contact.PhoneNumber;
            _emailBox.Text = contact.Email;
            _groupBox.Text = string.IsNullOrWhiteSpace(contact.Group) ? "General" : contact.Group;
            _notesBox.Text = contact.Notes;
            _notesBox.AcceptsReturn = true;
            _notesBox.TextWrapping = TextWrapping.Wrap;
            _notesBox.MinHeight = 90;
            _favoriteBox.IsChecked = contact.IsFavorite;

            var panel = new StackPanel { Margin = new Thickness(16) };
            AddField(panel, "Name", _nameBox, "Contact name");
            AddField(panel, "Extension", _extensionBox, "PBX extension");
            AddField(panel, "Phone number", _phoneBox, "Phone number");
            AddField(panel, "Email", _emailBox, "Email address");
            AddField(panel, "Group", _groupBox, "Contact group");
            AddField(panel, "Notes", _notesBox, "Contact notes");
            panel.Children.Add(_favoriteBox);

            var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            var saveButton = new System.Windows.Controls.Button { Content = "OK", MinWidth = 90, IsDefault = true, Margin = new Thickness(0, 8, 8, 0) };
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", MinWidth = 90, IsCancel = true, Margin = new Thickness(0, 8, 0, 0) };
            saveButton.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_nameBox.Text)
                    && string.IsNullOrWhiteSpace(_extensionBox.Text)
                    && string.IsNullOrWhiteSpace(_phoneBox.Text)
                    && string.IsNullOrWhiteSpace(_emailBox.Text))
                {
                    MessageBox.Show("Enter at least a name, extension, phone number, or email.", title, MessageBoxButton.OK, MessageBoxImage.Information);
                    _nameBox.Focus();
                    return;
                }

                _accepted = true;
                Close();
            };
            buttons.Children.Add(saveButton);
            buttons.Children.Add(cancelButton);
            panel.Children.Add(buttons);
            Content = panel;

            Closed += (_, _) =>
            {
                if (!_accepted)
                {
                    return;
                }

                contact.DisplayName = _nameBox.Text.Trim();
                contact.Extension = _extensionBox.Text.Trim();
                contact.PhoneNumber = _phoneBox.Text.Trim();
                contact.Email = _emailBox.Text.Trim();
                contact.Group = string.IsNullOrWhiteSpace(_groupBox.Text) ? "General" : _groupBox.Text.Trim();
                contact.Notes = _notesBox.Text.Trim();
                contact.IsFavorite = _favoriteBox.IsChecked == true;
            };
        }

        public static bool Show(Window owner, FlexPhoneContact contact, string title)
        {
            var dialog = new ContactEditDialog(contact, title) { Owner = owner };
            dialog.ShowDialog();
            return dialog._accepted;
        }

        private static void AddField(System.Windows.Controls.Panel panel, string label, System.Windows.Controls.TextBox box, string automationName)
        {
            panel.Children.Add(new TextBlock { Text = label });
            box.Margin = new Thickness(0, 3, 0, 8);
            box.MinHeight = 30;
            AutomationProperties.SetName(box, automationName);
            panel.Children.Add(box);
        }
    }
}
