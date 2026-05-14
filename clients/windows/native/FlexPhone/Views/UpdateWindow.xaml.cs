using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlexPhone.Services;

namespace FlexPhone.Views
{
    public enum UpdateWindowDecision
    {
        None,
        Install,
        Postpone
    }

    public partial class UpdateWindow : Window
    {
        public UpdateWindow(FlexPhoneUpdateManifest manifest, string currentVersion, int postponeCount, int maxPostpones)
        {
            InitializeComponent();
            Manifest = manifest;
            CurrentVersion = currentVersion;
            PostponeCount = postponeCount;
            MaxPostpones = maxPostpones;
            PostponeComboBox.SelectedIndex = 1;
            NotesList.ItemsSource = BuildNotes(manifest);
            LinksList.ItemsSource = manifest.Links;
            SummaryText.Text = BuildSummary(manifest, currentVersion, postponeCount, maxPostpones);
            PostponeButton.IsEnabled = postponeCount < maxPostpones && !manifest.Critical;
            if (!PostponeButton.IsEnabled)
            {
                PostponeButton.Content = "Postpone limit reached";
            }
        }

        public FlexPhoneUpdateManifest Manifest { get; }
        public string CurrentVersion { get; }
        public int PostponeCount { get; }
        public int MaxPostpones { get; }
        public UpdateWindowDecision Decision { get; private set; }
        public TimeSpan PostponeFor { get; private set; } = TimeSpan.FromHours(1);

        private void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            Decision = UpdateWindowDecision.Install;
            DialogResult = true;
        }

        private void PostponeButton_Click(object sender, RoutedEventArgs e)
        {
            Decision = UpdateWindowDecision.Postpone;
            PostponeFor = SelectedPostponeDuration();
            DialogResult = true;
        }

        private void OpenLinkButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedLink();
        }

        private void LinksList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedLink();
        }

        private void OpenSelectedLink()
        {
            if (LinksList.SelectedItem is not FlexPhoneUpdateLink link || string.IsNullOrWhiteSpace(link.Url))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = link.Url,
                UseShellExecute = true
            });
        }

        private TimeSpan SelectedPostponeDuration()
        {
            return (PostponeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
            {
                "15 minutes" => TimeSpan.FromMinutes(15),
                "4 hours" => TimeSpan.FromHours(4),
                "Tomorrow" => TimeSpan.FromDays(1),
                _ => TimeSpan.FromHours(1)
            };
        }

        private static List<string> BuildNotes(FlexPhoneUpdateManifest manifest)
        {
            if (manifest.ReleaseNotesList.Count > 0)
            {
                return manifest.ReleaseNotesList
                    .Where(note => !string.IsNullOrWhiteSpace(note))
                    .Select(note => note.Trim())
                    .ToList();
            }

            return manifest.ReleaseNotes
                .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .DefaultIfEmpty("No release notes were provided.")
                .ToList();
        }

        private static string BuildSummary(FlexPhoneUpdateManifest manifest, string currentVersion, int postponeCount, int maxPostpones)
        {
            var target = manifest.EffectiveVersion;
            var summary = $"Flex Phone {target} is available. Current version is {currentVersion}.";
            if (manifest.Critical)
            {
                summary += " This is marked critical and cannot be postponed.";
            }
            else if (postponeCount >= maxPostpones)
            {
                summary += " The postpone limit has been reached, so the update must be installed.";
            }

            if (manifest.SkipVersions.Contains(currentVersion, StringComparer.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(manifest.MinimumSupportedVersion)
                    && Version.TryParse(manifest.MinimumSupportedVersion, out var minimum)
                    && Version.TryParse(currentVersion, out var current)
                    && current < minimum))
            {
                summary += $" Your current version will be skipped to the latest available version, {target}.";
            }

            return summary;
        }
    }
}
