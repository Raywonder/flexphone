using FlexPhone.Services;

namespace FlexPhone.Models
{
    public enum PbxLineState
    {
        Idle,
        Ringing,
        Dialing,
        Connected,
        Holding,
        Ended,
        Failed
    }

    public sealed class PbxLineStateSnapshot
    {
        public string AccountName { get; init; } = "";
        public int LineNumber { get; init; }
        public PbxLineState State { get; init; }
        public string RemoteParty { get; init; } = "";
        public string Status { get; init; } = "Idle";
        public bool IsActive { get; init; }
        public bool IsMuted { get; init; }

        public bool IsFree => State is PbxLineState.Idle or PbxLineState.Ended or PbxLineState.Failed;

        public string AccessibleSummary
        {
            get
            {
                var lineName = $"Line {LineNumber}";
                var stateText = State switch
                {
                    PbxLineState.Idle => "free",
                    PbxLineState.Ringing => $"ringing from {SafeRemoteParty}",
                    PbxLineState.Dialing => $"dialing {SafeRemoteParty}",
                    PbxLineState.Connected => $"call with {SafeRemoteParty}",
                    PbxLineState.Holding => $"call with {SafeRemoteParty}, on hold",
                    PbxLineState.Ended => "free",
                    PbxLineState.Failed => "free after failed call",
                    _ => Status
                };
                var activeText = IsActive ? ", active" : "";
                var mutedText = IsMuted ? ", muted" : "";
                return $"{lineName}, {stateText}{activeText}{mutedText}";
            }
        }

        private string SafeRemoteParty => string.IsNullOrWhiteSpace(RemoteParty) ? "unknown caller" : RemoteParty;
    }

    public sealed class LineViewItem
    {
        public required PbxLineStateSnapshot Snapshot { get; init; }
        public int LineNumber => Snapshot.LineNumber;
        public string DisplayText => Snapshot.AccessibleSummary;
        public string DetailText => Snapshot.Status;
        public override string ToString() => DisplayText;
    }

    public sealed class PbxAccountSession
    {
        public required string Server { get; init; }
        public string SipServer { get; init; } = "";
        public string Username { get; init; } = "";
        public required string Extension { get; init; }
        public required string Password { get; init; }
        public string SessionToken { get; init; } = "";
        public string FullName { get; set; } = "";
        public required PbxSoftphoneService Softphone { get; init; }
        public int LocalPort { get; init; }
        public DateTime RegisteredAt { get; init; } = DateTime.Now;
        public string DeviceId { get; init; } = "";
        public string Email { get; init; } = "";
        public string Role { get; init; } = "";
        public string Group { get; init; } = "";
        public string Team { get; init; } = "";
        public string AutoQueuePolicy { get; init; } = "";
        public bool IsPaired { get; set; }
        public QueueState QueueState { get; set; } = QueueState.LoggedOut;
        public DateTime QueueStateChangedAt { get; set; } = DateTime.Now;

        public string DisplayName => string.IsNullOrWhiteSpace(FullName)
            ? $"{Extension} on {Server} ({LocalPort})"
            : $"{FullName} ({Extension})";

        public override string ToString() => DisplayName;
    }

    public enum QueueState
    {
        LoggedIn,
        LoggedOut
    }
}
