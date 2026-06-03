using System.Collections.ObjectModel;
using System.Net;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Windows;
using FlexPhone.Models;

namespace FlexPhone.Services
{
    public sealed class PbxSoftphoneService : IDisposable
    {
        private sealed class LineRuntime
        {
            public PbxLineStateSnapshot Snapshot { get; set; }
            public SIPUserAgent? UserAgent { get; set; }
            public SIPServerUserAgent? PendingIncomingCall { get; set; }
            public VoIPMediaSession? MediaSession { get; set; }
            public AudioExtrasSource? MuteAudioSource { get; set; }
            public bool IsMuted { get; set; }

            public LineRuntime(int lineNumber)
            {
                Snapshot = new PbxLineStateSnapshot
                {
                    LineNumber = lineNumber,
                    State = PbxLineState.Idle,
                    Status = "Idle"
                };
            }
        }

        private readonly object _sync = new();
        private readonly List<LineRuntime> _lines = [];
        private SIPTransport? _sipTransport;
        private SIPRegistrationUserAgent? _registration;
        private int _activeLine = 1;
        private bool _disposed;
        private static readonly TimeSpan RegistrationWaitTimeout = TimeSpan.FromSeconds(18);

        public PbxSoftphoneService()
        {
            for (var i = 1; i <= 8; i++)
            {
                _lines.Add(new LineRuntime(i));
            }
        }

        public string RegistrationStatus { get; private set; } = "Not registered";
        public bool IsRegistered { get; private set; }
        public bool HasIncomingCall => _lines.Any(line => line.PendingIncomingCall != null);
        public bool IsInCall => ActiveRuntime.UserAgent?.IsCallActive == true && ActiveLineState == PbxLineState.Connected;
        public bool IsMuted => ActiveRuntime.IsMuted;
        public int ActiveLineNumber => _activeLine;
        public PbxLineState ActiveLineState => ActiveRuntime.Snapshot.State;
        public int ActiveCallCount => _lines.Count(line => line.UserAgent?.IsCallActive == true || line.PendingIncomingCall != null);
        public int FirstFreeLineNumber => FirstIdleLine();
        public ReadOnlyCollection<PbxLineStateSnapshot> Lines
        {
            get
            {
                lock (_sync)
                {
                    return _lines.Select(line => line.Snapshot).ToList().AsReadOnly();
                }
            }
        }

        private LineRuntime ActiveRuntime => _lines[Math.Clamp(_activeLine, 1, _lines.Count) - 1];

        public event EventHandler? StateChanged;
        public event EventHandler<string>? IncomingCall;
        public event EventHandler<string>? RegistrationSucceeded;
        public event EventHandler<string>? LineFreed;
        public event EventHandler<string>? ActiveLineChanged;

        public Task StartAsync(int localPort = 5066)
        {
            if (_sipTransport != null)
            {
                return Task.CompletedTask;
            }

            _sipTransport = new SIPTransport();
            _sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, localPort)));
            _sipTransport.AddSIPChannel(new SIPTCPChannel(new IPEndPoint(IPAddress.Any, localPort)));
            foreach (var line in _lines)
            {
                line.UserAgent = CreateUserAgent(line.Snapshot.LineNumber);
                line.UserAgent.OnIncomingCall += OnIncomingCall;
            }
            RaiseStateChanged();
            return Task.CompletedTask;
        }

        public async Task RegisterAsync(string server, string extension, string password, int localPort = 5066)
        {
            ValidateAccount(server, extension, password);
            await StartAsync(localPort);

            var registrar = NormalizeSipServer(server);
            var domain = registrar;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            string? lastRegistrationError = null;

            _registration?.Stop();
            _registration = new SIPRegistrationUserAgent(
                _sipTransport,
                extension,
                password,
                domain,
                120,
                300,
                60,
                3,
                true,
                false);

            _registration.RegistrationSuccessful += (_, _) =>
            {
                var wasRegistered = IsRegistered;
                IsRegistered = true;
                RegistrationStatus = $"Registered {extension} on {registrar}";
                RaiseStateChanged();
                completion.TrySetResult();
                if (!wasRegistered)
                {
                    RegistrationSucceeded?.Invoke(this, RegistrationStatus);
                }
            };
            _registration.RegistrationFailed += (_, error, _) =>
            {
                IsRegistered = false;
                RegistrationStatus = $"Registration failed: {error}";
                RaiseStateChanged();
                completion.TrySetException(new InvalidOperationException(RegistrationStatus));
            };
            _registration.RegistrationTemporaryFailure += (_, error, _) =>
            {
                IsRegistered = false;
                lastRegistrationError = error?.ToString();
                RegistrationStatus = $"Registration retrying: {lastRegistrationError}";
                RaiseStateChanged();
            };
            _registration.Start();
            RegistrationStatus = $"Registering {extension} on {registrar}";
            RaiseStateChanged();

            try
            {
                await completion.Task.WaitAsync(RegistrationWaitTimeout);
            }
            catch (TimeoutException ex)
            {
                IsRegistered = false;
                RegistrationStatus = string.IsNullOrWhiteSpace(lastRegistrationError)
                    ? $"Registration timed out on {registrar}"
                    : $"Registration timed out on {registrar}: {lastRegistrationError}";
                RaiseStateChanged();
                throw new TimeoutException($"{RegistrationStatus}. Check that SIP UDP 5060 and RTP ports are reachable for this PBX.", ex);
            }
        }

        public Task UnregisterAsync()
        {
            _registration?.Stop();
            _registration = null;
            foreach (var line in _lines)
            {
                line.UserAgent?.Hangup();
                line.MediaSession?.Close("unregister");
                line.PendingIncomingCall = null;
                line.MediaSession = null;
                line.MuteAudioSource = null;
                line.IsMuted = false;
                SetLine(line.Snapshot.LineNumber, PbxLineState.Idle, "", "Idle", raise: false);
            }
            IsRegistered = false;
            RegistrationStatus = "Not registered";
            RaiseStateChanged();
            return Task.CompletedTask;
        }

        public Task SelectLineAsync(int lineNumber)
        {
            if (lineNumber < 1 || lineNumber > _lines.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line must be between 1 and 8.");
            }

            if (lineNumber == _activeLine)
            {
                return Task.CompletedTask;
            }

            var previous = ActiveRuntime;
            var next = _lines[lineNumber - 1];
            if (previous.UserAgent?.IsCallActive == true && previous.Snapshot.State == PbxLineState.Connected)
            {
                previous.UserAgent.PutOnHold();
                SetLine(previous.Snapshot.LineNumber, PbxLineState.Holding, previous.Snapshot.RemoteParty, "On hold", raise: false);
            }

            _activeLine = lineNumber;
            if (next.UserAgent?.IsCallActive == true && next.Snapshot.State == PbxLineState.Holding)
            {
                next.UserAgent.TakeOffHold();
                SetLine(lineNumber, PbxLineState.Connected, next.Snapshot.RemoteParty, "Connected", raise: false);
            }

            MarkActiveLine();
            ActiveLineChanged?.Invoke(this, $"Line {lineNumber} selected. {next.Snapshot.AccessibleSummary}");
            RaiseStateChanged();
            return Task.CompletedTask;
        }

        public async Task DialAsync(string server, string extension, string password, string destination, int? requestedLine = null)
        {
            ValidateAccount(server, extension, password);
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new ArgumentException("Destination is required.", nameof(destination));
            }

            await StartAsync();
            var lineNumber = requestedLine is >= 1 and <= 8 && IsLineFree(requestedLine.Value)
                ? requestedLine.Value
                : FirstIdleLine();
            await SelectLineAsync(lineNumber);

            var line = _lines[lineNumber - 1];
            var registrar = NormalizeSipServer(server);
            var dst = destination.Contains("@", StringComparison.Ordinal)
                ? destination
                : $"{destination}@{registrar}";

            SetLine(lineNumber, PbxLineState.Dialing, dst, $"Dialing {dst}");

            line.UserAgent = CreateUserAgent(lineNumber);
            line.UserAgent.OnIncomingCall += OnIncomingCall;
            line.MediaSession = CreateMediaSession();
            var result = await line.UserAgent.Call($"sip:{dst}", extension, password, line.MediaSession);

            SetLine(lineNumber, result ? PbxLineState.Connected : PbxLineState.Failed, dst, result ? $"Connected to {dst}" : "Call failed");
            if (!result)
            {
                LineFreed?.Invoke(this, $"Line {lineNumber} is free after a failed call.");
            }
        }

        public async Task AnswerAsync()
        {
            var line = ActiveRuntime.PendingIncomingCall != null ? ActiveRuntime : _lines.FirstOrDefault(l => l.PendingIncomingCall != null);
            if (line == null)
            {
                throw new InvalidOperationException("There is no incoming call waiting to answer.");
            }

            await SelectLineAsync(line.Snapshot.LineNumber);
            line.MediaSession = CreateMediaSession();
            var result = await line.UserAgent!.Answer(line.PendingIncomingCall, line.MediaSession);
            line.PendingIncomingCall = null;
            SetLine(line.Snapshot.LineNumber, result ? PbxLineState.Connected : PbxLineState.Failed, line.Snapshot.RemoteParty, result ? "Connected" : "Answer failed");
        }

        public Task HangupAsync()
        {
            var line = ActiveRuntime.UserAgent?.IsCallActive == true || ActiveRuntime.PendingIncomingCall != null
                ? ActiveRuntime
                : _lines.FirstOrDefault(item => item.UserAgent?.IsCallActive == true || item.PendingIncomingCall != null) ?? ActiveRuntime;
            _activeLine = line.Snapshot.LineNumber;
            line.UserAgent?.Hangup();
            line.MediaSession?.Close("hangup");
            line.PendingIncomingCall = null;
            line.IsMuted = false;
            line.MuteAudioSource = null;
            SetLine(line.Snapshot.LineNumber, PbxLineState.Ended, line.Snapshot.RemoteParty, "Ended");
            LineFreed?.Invoke(this, $"Line {line.Snapshot.LineNumber} is free.");
            return Task.CompletedTask;
        }

        public Task HoldAsync()
        {
            var line = ActiveRuntime;
            if (line.UserAgent?.IsCallActive == true)
            {
                line.UserAgent.PutOnHold();
                SetLine(_activeLine, PbxLineState.Holding, line.Snapshot.RemoteParty, "On hold");
            }

            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            var line = ActiveRuntime;
            if (line.UserAgent?.IsCallActive == true)
            {
                line.UserAgent.TakeOffHold();
                SetLine(_activeLine, PbxLineState.Connected, line.Snapshot.RemoteParty, "Connected");
            }

            return Task.CompletedTask;
        }

        public async Task TransferAsync(string server, string destination)
        {
            var line = ActiveRuntime;
            if (line.UserAgent?.IsCallActive != true)
            {
                throw new InvalidOperationException("There is no active call to transfer.");
            }

            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new ArgumentException("Transfer destination is required.", nameof(destination));
            }

            var registrar = NormalizeSipServer(server);
            var target = destination.Contains("@", StringComparison.Ordinal)
                ? destination
                : $"{destination}@{registrar}";
            var targetUri = SIPURI.ParseSIPURI($"sip:{target}");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var accepted = await line.UserAgent.BlindTransfer(targetUri, TimeSpan.FromSeconds(12), cts.Token);
            SetLine(_activeLine, accepted ? PbxLineState.Ended : PbxLineState.Failed, target, accepted ? $"Transferred to {target}" : "Transfer failed");
            if (accepted)
            {
                LineFreed?.Invoke(this, $"Line {_activeLine} is free after transfer.");
            }
        }

        public Task SendDtmfAsync(char digit)
        {
            var line = ActiveRuntime;
            if (line.UserAgent?.IsCallActive == true && TryMapDtmf(digit, out var tone))
            {
                line.UserAgent.SendDtmf(tone);
            }

            return Task.CompletedTask;
        }

        public Task SetMutedAsync(bool muted)
        {
            var line = ActiveRuntime;
            line.IsMuted = muted;
            if (line.UserAgent?.IsCallActive != true)
            {
                RaiseStateChanged();
                return Task.CompletedTask;
            }

            if (muted)
            {
                line.MuteAudioSource ??= new AudioExtrasSource(new AudioEncoder(), new AudioSourceOptions
                {
                    AudioSource = AudioSourcesEnum.Silence
                });
                line.MuteAudioSource.SetSource(AudioSourcesEnum.Silence);
            }

            SetLine(_activeLine, line.Snapshot.State, line.Snapshot.RemoteParty, muted ? "Muted" : "Connected");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _registration?.Stop();
            foreach (var line in _lines)
            {
                line.MediaSession?.Close("dispose");
            }
            _sipTransport?.Shutdown();
        }

        private SIPUserAgent CreateUserAgent(int lineNumber)
        {
            var userAgent = new SIPUserAgent(_sipTransport, null);
            userAgent.ClientCallFailed += (_, error, _) =>
            {
                var line = _lines[lineNumber - 1];
                SetLine(lineNumber, PbxLineState.Failed, line.Snapshot.RemoteParty, $"Call failed: {error}");
                LineFreed?.Invoke(this, $"Line {lineNumber} is free after a failed call.");
            };
            userAgent.OnCallHungup += _ =>
            {
                var line = _lines[lineNumber - 1];
                line.IsMuted = false;
                line.MuteAudioSource = null;
                SetLine(lineNumber, PbxLineState.Ended, line.Snapshot.RemoteParty, "Ended");
                LineFreed?.Invoke(this, $"Line {lineNumber} is free.");
            };
            return userAgent;
        }

        private VoIPMediaSession CreateMediaSession()
        {
            var audioEndPoint = new WindowsAudioEndPoint(new AudioEncoder());
            return new VoIPMediaSession(audioEndPoint.ToMediaEndPoints());
        }

        private void OnIncomingCall(SIPUserAgent userAgent, SIPRequest request)
        {
            var lineNumber = FirstIdleLine();
            var line = _lines[lineNumber - 1];
            _activeLine = lineNumber;
            line.UserAgent = userAgent;
            line.PendingIncomingCall = userAgent.AcceptCall(request);
            var remote = request.Header.From?.FromURI?.User ?? request.Header.From?.FromName ?? "Unknown caller";
            SetLine(lineNumber, PbxLineState.Ringing, remote, $"Incoming call from {remote}");
            IncomingCall?.Invoke(this, $"Line {lineNumber} from {remote}");
        }

        private int FirstIdleLine()
        {
            lock (_sync)
            {
                return _lines.FirstOrDefault(line => line.Snapshot.IsFree)?.Snapshot.LineNumber ?? 1;
            }
        }

        private bool IsLineFree(int lineNumber)
        {
            lock (_sync)
            {
                return _lines[lineNumber - 1].Snapshot.IsFree;
            }
        }

        private void SetLine(int lineNumber, PbxLineState state, string remoteParty, string status, bool raise = true)
        {
            lock (_sync)
            {
                var runtime = _lines[lineNumber - 1];
                runtime.Snapshot = new PbxLineStateSnapshot
                {
                    LineNumber = lineNumber,
                    State = state,
                    RemoteParty = remoteParty,
                    Status = status,
                    IsActive = lineNumber == _activeLine,
                    IsMuted = runtime.IsMuted
                };
            }

            if (raise)
            {
                RaiseStateChanged();
            }
        }

        private void MarkActiveLine()
        {
            lock (_sync)
            {
                foreach (var runtime in _lines)
                {
                    var snapshot = runtime.Snapshot;
                    runtime.Snapshot = new PbxLineStateSnapshot
                    {
                        AccountName = snapshot.AccountName,
                        LineNumber = snapshot.LineNumber,
                        State = snapshot.State,
                        RemoteParty = snapshot.RemoteParty,
                        Status = snapshot.Status,
                        IsActive = snapshot.LineNumber == _activeLine,
                        IsMuted = runtime.IsMuted
                    };
                }
            }
        }

        private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        private static void ValidateAccount(string server, string extension, string password)
        {
            if (string.IsNullOrWhiteSpace(server)) throw new ArgumentException("PBX server is required.", nameof(server));
            if (string.IsNullOrWhiteSpace(extension)) throw new ArgumentException("Extension is required.", nameof(extension));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password is required.", nameof(password));
        }

        private static string NormalizeSipServer(string server)
        {
            var value = server.Trim();
            if (value.StartsWith("sip:", StringComparison.OrdinalIgnoreCase))
            {
                value = value[4..];
            }

            return value.TrimEnd('/');
        }

        private static bool TryMapDtmf(char digit, out byte tone)
        {
            tone = digit switch
            {
                >= '0' and <= '9' => (byte)(digit - '0'),
                '*' => 10,
                '#' => 11,
                'A' or 'a' => 12,
                'B' or 'b' => 13,
                'C' or 'c' => 14,
                'D' or 'd' => 15,
                _ => 255
            };
            return tone != 255;
        }
    }
}
