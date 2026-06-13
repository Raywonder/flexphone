using System.Text;
using System.Windows;
using FlexPhone.Services;

namespace FlexPhone.Views
{
    public enum UpdateWindowDecision
    {
        None,
        Install,
        Postpone,
        Acknowledge
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
            SummaryText.Text = BuildSummary(manifest, currentVersion, postponeCount, maxPostpones);
            UpdateInfoText.Text = BuildUpdateInfo(manifest, currentVersion, postponeCount, maxPostpones);
            Loaded += (_, _) => OkButton.Focus();
        }

        public FlexPhoneUpdateManifest Manifest { get; }
        public string CurrentVersion { get; }
        public int PostponeCount { get; }
        public int MaxPostpones { get; }
        public UpdateWindowDecision Decision { get; private set; }
        public TimeSpan PostponeFor { get; private set; } = TimeSpan.FromHours(1);

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Decision = UpdateWindowDecision.Acknowledge;
            DialogResult = true;
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

        private static string BuildUpdateInfo(FlexPhoneUpdateManifest manifest, string currentVersion, int postponeCount, int maxPostpones)
        {
            var builder = new StringBuilder();
            builder.AppendLine(BuildSummary(manifest, currentVersion, postponeCount, maxPostpones));
            builder.AppendLine();
            builder.AppendLine($"Installed version: {currentVersion}");
            builder.AppendLine($"Available version: {manifest.EffectiveVersion}");

            if (!string.IsNullOrWhiteSpace(manifest.FileName))
            {
                builder.AppendLine($"Installer: {manifest.FileName}");
            }

            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                builder.AppendLine($"Checksum: {manifest.Sha256}");
            }

            builder.AppendLine();
            builder.AppendLine("What is new:");
            foreach (var note in BuildNotes(manifest))
            {
                builder.AppendLine("- " + note);
            }

            if (manifest.Links.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Links:");
                foreach (var link in manifest.Links)
                {
                    var label = string.IsNullOrWhiteSpace(link.DisplayText) ? "Link" : link.DisplayText.Trim();
                    builder.AppendLine($"- {label}: {link.Url}");
                }
            }

            if (!manifest.Critical && postponeCount < maxPostpones)
            {
                builder.AppendLine();
                builder.AppendLine("Automatic update settings control whether this update installs now or later.");
            }

            return builder.ToString();
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
