namespace FlexPhone.Models
{
    public sealed class FlexPhoneSettings
    {
        public string DefaultPbxServer { get; set; } = "pbx.tappedin.fm";
        public string DefaultTurnServer { get; set; } = "turn.tappedin.fm";
        public bool UseCustomTurnServer { get; set; }
        public string CustomTurnServer { get; set; } = "";
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimizedToTray { get; set; }
        public bool StartWithWindows { get; set; }
        public bool RememberSignIn { get; set; } = true;
        public bool PlayCallSounds { get; set; } = true;
        public string IncomingRingtone { get; set; } = "Incoming call";
        public bool AutoAnswer { get; set; }
        public string ProviderType { get; set; } = "Flex PBX";
        public string EnterDefaultAction { get; set; } = "Activate focused control";
        public string SpacebarInCallAction { get; set; } = "Mute or unmute microphone";
        public string InputAudioDevice { get; set; } = "Default communications microphone";
        public string OutputAudioDevice { get; set; } = "Default communications speaker";
        public string ClientDisplayName { get; set; } = "";
        public string AutoQueueSignInOutMode { get; set; } = "Off";
        public bool AllowIntercom { get; set; } = true;
        public bool CheckForUpdates { get; set; } = true;
        public bool AutomaticallyInstallUpdates { get; set; } = true;
        public bool AnnounceUpdateInstallRestart { get; set; } = true;
        public int UpdatePostponeCount { get; set; }
        public DateTime UpdatePostponedUntil { get; set; } = DateTime.MinValue;
        public int DefaultLocalSipPort { get; set; } = 5066;
        public string UserStatus { get; set; } = "Available";
        public string BrowserLoginPath { get; set; } = "/flexphone/link";
        public string PasswordResetPath { get; set; } = "/user/password/reset";
        public string AccountRecoveryPath { get; set; } = "/api/flexphone-account-recovery.php";
        public string ClientDownloadPath { get; set; } = "/downloads/flexphone/";
        public string UpdateManifestPath { get; set; } = "/downloads/flexphone/flexphone-update.json";
        public string QueueToggleCode { get; set; } = "*45";
        public string QueueLoginCode { get; set; } = "*45";
        public string QueueLogoutCode { get; set; } = "*46";
        public bool QueueUsesSingleToggleCode { get; set; } = true;
        public string VoicemailCode { get; set; } = "*97";
        public string DndToggleCode { get; set; } = "*76";
        public string CallScreeningToggleCode { get; set; } = "*56";
        public bool AnnounceLineChanges { get; set; } = true;
        public bool AnnounceQueueDuration { get; set; } = true;
        public bool AnnounceCallEnded { get; set; } = true;
        public bool DetailedLineAnnouncements { get; set; } = true;
        public bool ShowKeyboardHints { get; set; } = true;
        public string AnswerHotKey { get; set; } = "Ctrl+Shift+A";
        public string HangupHotKey { get; set; } = "Ctrl+Shift+H";
        public string HoldHotKey { get; set; } = "Ctrl+Shift+O";
        public bool HasSeenGettingStarted { get; set; }

        public string EffectiveTurnServer =>
            UseCustomTurnServer && !string.IsNullOrWhiteSpace(CustomTurnServer)
                ? CustomTurnServer.Trim()
                : DefaultTurnServer;
    }
}
