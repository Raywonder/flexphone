using System.Diagnostics;
using System.Reflection;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace FlexPhone.Services
{
    public class FlexPhoneLoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Error { get; set; } = "";
        public string Extension { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = "";
        [JsonPropertyName("session_token")]
        public string SessionToken { get; set; } = "";
        [JsonPropertyName("auth_methods")]
        public List<string> AuthMethods { get; set; } = [];
        public string Role { get; set; } = "";
        public string Group { get; set; } = "";
        public string Team { get; set; } = "";
        [JsonPropertyName("auto_queue_sign_in_out")]
        public string AutoQueueSignInOut { get; set; } = "";
        [JsonPropertyName("feature_codes")]
        public Dictionary<string, string> FeatureCodes { get; set; } = [];
        [JsonPropertyName("sip_password")]
        public string SipPassword { get; set; } = "";
        [JsonPropertyName("token_url")]
        public string TokenUrl { get; set; } = "";
        [JsonPropertyName("authorization_url")]
        public string AuthorizationUrl { get; set; } = "";
        [JsonPropertyName("sip_settings")]
        public FlexPhoneSipSettings SipSettings { get; set; } = new();
    }

    public sealed class FlexPhoneSipSettings
    {
        public string Host { get; set; } = "";
        public string Server { get; set; } = "";
        public int Port { get; set; } = 5060;
        public string Transport { get; set; } = "UDP";
        public List<FlexPhoneSipRoute> Routes { get; set; } = [];
        public List<FlexPhoneSipRoute> Fallbacks { get; set; } = [];
    }

    public sealed class FlexPhoneSipRoute
    {
        public string Label { get; set; } = "";
        public string Host { get; set; } = "";
        public string Server { get; set; } = "";
        public int Port { get; set; } = 5060;
        public string Transport { get; set; } = "UDP";
        [JsonPropertyName("route_type")]
        public string RouteType { get; set; } = "";
        public bool Preferred { get; set; }
    }

    public sealed class FlexPhoneProvisionResponse : FlexPhoneLoginResponse
    {
        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; } = "";
        [JsonPropertyName("expires_at")]
        public string ExpiresAt { get; set; } = "";

        [JsonIgnore]
        public string DeviceAuthorizationUrl => string.IsNullOrWhiteSpace(TokenUrl) ? AuthorizationUrl : TokenUrl;
    }

    public sealed class FlexPhoneActionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Error { get; set; } = "";
        [JsonPropertyName("pairing_code")]
        public string PairingCode { get; set; } = "";
        [JsonPropertyName("pairing_url")]
        public string PairingUrl { get; set; } = "";
        public List<FlexPhoneCallInfo> Calls { get; set; } = [];
        public List<FlexPhonePresenceInfo> People { get; set; } = [];
        public List<FlexPhoneRecordingInfo> Recordings { get; set; } = [];
        public List<FlexPhoneVoicemailInfo> Voicemails { get; set; } = [];
        public List<FlexPhoneMessageInfo> Messages { get; set; } = [];
        public List<FlexPhoneQueueInfo> Queues { get; set; } = [];
        public List<FlexPhoneDeviceInfo> Devices { get; set; } = [];
    }

    public sealed class FlexPhoneDeviceInfo
    {
        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; } = "";
        [JsonPropertyName("device_name")]
        public string DeviceName { get; set; } = "";
        public string Extension { get; set; } = "";
        public bool Online { get; set; }
        [JsonPropertyName("flexphone_capable")]
        public bool FlexPhoneCapable { get; set; }
        [JsonPropertyName("can_receive_named_transfer")]
        public bool CanReceiveNamedTransfer { get; set; }

        [JsonIgnore]
        public string AccessibleSummary => $"{(string.IsNullOrWhiteSpace(DeviceName) ? "Unnamed device" : DeviceName)}, {(Online ? "online" : "offline")}";

        public override string ToString() => AccessibleSummary;
    }

    public sealed class FlexPhoneCallInfo
    {
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = "";
        public string Number { get; set; } = "";
        [JsonPropertyName("last4")]
        public string Last4 { get; set; } = "";
        public string State { get; set; } = "";
        public string Queue { get; set; } = "";
        public string Wait { get; set; } = "";
    }

    public sealed class FlexPhoneQueueInfo
    {
        public string Name { get; set; } = "";
        [JsonPropertyName("calls_waiting")]
        public int CallsWaiting { get; set; }
        [JsonPropertyName("members_total")]
        public int MembersTotal { get; set; }
        [JsonPropertyName("members_available")]
        public int MembersAvailable { get; set; }
    }

    public sealed class FlexPhonePresenceInfo
    {
        public string Extension { get; set; } = "";
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";

        [JsonIgnore]
        public bool IsOnline => !Status.Contains("offline", StringComparison.OrdinalIgnoreCase)
            && !Status.Contains("signed out", StringComparison.OrdinalIgnoreCase)
            && !Status.Contains("unavailable", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public string AccessibleSummary
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(DisplayName) ? "Unknown user" : DisplayName;
                var extension = string.IsNullOrWhiteSpace(Extension) ? "no extension" : $"extension {Extension}";
                var role = string.IsNullOrWhiteSpace(Role) ? "" : $", {Role}";
                var status = string.IsNullOrWhiteSpace(Status) ? "status unknown" : Status;
                return $"{name}, {extension}{role}, {status}";
            }
        }

        public override string ToString() => AccessibleSummary;
    }

    public sealed class FlexPhoneRecordingInfo
    {
        public string Name { get; set; } = "";
        public string Date { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public sealed class FlexPhoneVoicemailInfo
    {
        public string Caller { get; set; } = "";
        public string Date { get; set; } = "";
        public string Folder { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public sealed class FlexPhoneMessageInfo
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string Body { get; set; } = "";
        public string Direction { get; set; } = "";
        public string Date { get; set; } = "";
        public string Provider { get; set; } = "";
        public string Status { get; set; } = "";
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = "";

        [JsonIgnore]
        public string AccessibleSummary
        {
            get
            {
                var direction = string.IsNullOrWhiteSpace(Direction) ? "message" : Direction;
                var party = string.IsNullOrWhiteSpace(DisplayName)
                    ? (string.Equals(direction, "out", StringComparison.OrdinalIgnoreCase) ? To : From)
                    : DisplayName;
                if (string.IsNullOrWhiteSpace(party))
                {
                    party = "unknown number";
                }

                var status = string.IsNullOrWhiteSpace(Status) ? "" : $", {Status}";
                return $"{direction} from {party}{status}. {Body}";
            }
        }
    }

    public sealed class AccountRecoveryResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Delivery { get; set; } = "";
        [JsonPropertyName("action_taken")]
        public string ActionTaken { get; set; } = "";
        [JsonPropertyName("new_password_generated")]
        public bool NewPasswordGenerated { get; set; }
    }

    public sealed class FlexPhoneUpdateManifest
    {
        public string Version { get; set; } = "";
        [JsonPropertyName("latest_version")]
        public string LatestVersion { get; set; } = "";
        [JsonPropertyName("minimum_supported_version")]
        public string MinimumSupportedVersion { get; set; } = "";
        [JsonPropertyName("critical")]
        public bool Critical { get; set; }
        [JsonPropertyName("skip_versions")]
        public List<string> SkipVersions { get; set; } = [];
        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = "";
        [JsonPropertyName("installer_url")]
        public string InstallerUrl { get; set; } = "";
        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = "";
        [JsonPropertyName("release_notes")]
        public string ReleaseNotes { get; set; } = "";
        [JsonPropertyName("release_notes_list")]
        public List<string> ReleaseNotesList { get; set; } = [];
        [JsonPropertyName("links")]
        public List<FlexPhoneUpdateLink> Links { get; set; } = [];
        [JsonPropertyName("checksum_sha256")]
        public string Sha256 { get; set; } = "";

        [JsonIgnore]
        public string ResolvedDownloadUrl => string.IsNullOrWhiteSpace(InstallerUrl) ? DownloadUrl : InstallerUrl;

        [JsonIgnore]
        public string EffectiveVersion => string.IsNullOrWhiteSpace(LatestVersion) ? Version : LatestVersion;
    }

    public sealed class FlexPhoneUpdateLink
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";

        [JsonIgnore]
        public string DisplayText => string.IsNullOrWhiteSpace(Title) ? Url : Title;
    }

    public sealed class FlexPbxClient
    {
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(20) };

        public Uri BuildBrowserLoginUri(string server, string extension, string path)
        {
            var builder = new UriBuilder(NormalizeHttpsBase(server))
            {
                Path = NormalizePath(path),
                Query = $"client=flexphone&extension={Uri.EscapeDataString(extension.Trim())}"
            };
            return builder.Uri;
        }

        public Uri BuildDownloadUri(string server, string path)
        {
            var builder = new UriBuilder(NormalizeHttpsBase(server))
            {
                Path = NormalizePath(path)
            };
            return builder.Uri;
        }

        public Uri BuildPasswordResetUri(string server, string extension, string path)
        {
            var builder = new UriBuilder(NormalizeHttpsBase(server))
            {
                Path = NormalizePath(path),
                Query = $"extension={Uri.EscapeDataString(extension.Trim())}&client=flexphone"
            };
            return builder.Uri;
        }

        public async Task<FlexPhoneLoginResponse> LoginAsync(string server, string identifier, string password)
        {
            var payload = JsonSerializer.Serialize(new
            {
                identifier = identifier.Trim(),
                password,
                account_type = "user",
                client = "Flex Phone"
            });

            using var response = await PostJsonAsync(new Uri(NormalizeHttpsBase(server), "/api/login.php"), payload);
            var result = await ReadJsonAsync<FlexPhoneLoginResponse>(response);
            if (result is null)
            {
                return new FlexPhoneLoginResponse
                {
                    Success = false,
                    Error = response.IsSuccessStatusCode
                        ? "Flex Phone could not read the login reply."
                        : "The phone system did not accept that login."
                };
            }

            result.Success = result.Success && response.IsSuccessStatusCode;
            return result;
        }

        public async Task<FlexPhoneProvisionResponse> RequestExtensionAsync(string server, string email, string deviceId)
        {
            var payload = JsonSerializer.Serialize(new
            {
                action = "request_extension",
                email = email.Trim(),
                device_id = deviceId,
                client = "Flex Phone",
                app_version = GetCurrentVersion(),
                requested_auth = "device_token",
                confirmation_required = true,
                roles_supported = new[] { "head_admin", "admin", "team_manager", "agent", "user" }
            });

            var result = await PostFlexPhoneProvisionAsync(server, payload);
            result.Email = email.Trim();
            result.DeviceId = deviceId;
            return result;
        }

        public async Task<FlexPhoneProvisionResponse> CompleteDeviceAuthorizationAsync(string server, string email, string deviceId, string tokenUrl)
        {
            var payload = JsonSerializer.Serialize(new
            {
                action = "complete_device_authorization",
                email = email.Trim(),
                device_id = deviceId,
                token_url = tokenUrl.Trim(),
                client = "Flex Phone",
                app_version = GetCurrentVersion(),
                confirmation_required = true
            });

            var result = await PostFlexPhoneProvisionAsync(server, payload);
            result.Email = email.Trim();
            result.DeviceId = deviceId;
            return result;
        }

        private async Task<FlexPhoneProvisionResponse> PostFlexPhoneProvisionAsync(string server, string payload)
        {
            using var response = await PostJsonAsync(new Uri(NormalizeHttpsBase(server), "/api/flexphone-client.php"), payload);
            var result = await ReadJsonAsync<FlexPhoneProvisionResponse>(response);
            if (result is null)
            {
                return new FlexPhoneProvisionResponse
                {
                    Success = false,
                    Error = response.IsSuccessStatusCode
                        ? "Flex Phone could not read the phone system reply."
                        : "The phone system could not start device authorization."
                };
            }

            result.Success = result.Success && response.IsSuccessStatusCode;
            return result;
        }

        public Task<FlexPhoneActionResponse> CreatePairingCodeAsync(string server, string extension, string token)
        {
            return PostControlAsync(server, token, new { action = "pairing_code", extension });
        }

        public Task<FlexPhoneActionResponse> ValidatePairingCodeAsync(string server, string extension, string token, string pairingCode)
        {
            return PostControlAsync(server, token, new
            {
                action = "validate_pairing_code",
                extension,
                pairing_code = pairingCode.Trim()
            });
        }

        public Task<FlexPhoneActionResponse> PairCurrentDeviceAsync(string server, string extension, string token, string deviceId)
        {
            return PostControlAsync(server, token, new
            {
                action = "pair_device",
                extension,
                device_id = deviceId,
                client = "Flex Phone",
                app_version = GetCurrentVersion()
            });
        }

        public Task<FlexPhoneActionResponse> GetWaitingCallsAsync(string server, string extension, string token)
        {
            return PostControlAsync(server, token, new { action = "waiting_calls", extension });
        }

        public Task<FlexPhoneActionResponse> GetPresenceAsync(string server, string extension, string token)
        {
            return PostControlAsync(server, token, new { action = "presence", extension });
        }

        public Task<FlexPhoneActionResponse> SendPresenceActionAsync(
            string server,
            string extension,
            string token,
            string targetExtension,
            string presenceAction,
            string message = "")
        {
            return PostControlAsync(server, token, new
            {
                action = "presence_action",
                extension,
                target_extension = targetExtension.Trim(),
                presence_action = presenceAction,
                message = message.Trim()
            });
        }

        public Task<FlexPhoneActionResponse> UpdateDisplayNameAsync(string server, string extension, string token, string displayName)
        {
            return PostControlAsync(server, token, new
            {
                action = "update_display_name",
                extension,
                display_name = displayName.Trim()
            });
        }

        public Task<FlexPhoneActionResponse> GetVoicemailAsync(string server, string extension, string token)
        {
            return PostControlAsync(server, token, new { action = "voicemail", extension });
        }

        public Task<FlexPhoneActionResponse> GetRecordingsAsync(string server, string extension, string token)
        {
            return PostControlAsync(server, token, new { action = "recordings", extension });
        }

        public Task<FlexPhoneActionResponse> GetMessagesAsync(string server, string extension, string token)
        {
            return PostControlAsync(server, token, new
            {
                action = "messages",
                channel = "sms",
                extension
            });
        }

        public Task<FlexPhoneActionResponse> SendMessageAsync(string server, string extension, string token, string to, string body)
        {
            return PostControlAsync(server, token, new
            {
                action = "send_message",
                channel = "sms",
                provider = "flexpbx",
                extension,
                to = to.Trim(),
                body = body.Trim()
            });
        }

        public Task<FlexPhoneActionResponse> ToggleServerRecordingAsync(string server, string extension, string token)
        {
            return PostControlAsync(server, token, new { action = "toggle_recording", extension });
        }

        public Task<FlexPhoneActionResponse> ReportDeviceStatusAsync(
            string server,
            string token,
            object status)
        {
            return PostControlAsync(server, token, status);
        }

        public Task<FlexPhoneActionResponse> GetMyDevicesAsync(string server, string extension, string token)
        {
            return PostControlAsync(server, token, new
            {
                action = "list_my_devices",
                extension
            });
        }

        public Task<FlexPhoneActionResponse> GetAudioCapabilitiesAsync(string server, string extension, string token)
        {
            return PostControlAsync(server, token, new
            {
                action = "audio_capabilities",
                extension
            });
        }

        public Task<FlexPhoneActionResponse> TransferToDeviceAsync(
            string server,
            string extension,
            string token,
            string deviceId)
        {
            return PostControlAsync(server, token, new
            {
                action = "transfer_to_device",
                extension,
                device_id = deviceId.Trim()
            });
        }

        public async Task<FlexPhoneUpdateManifest?> GetUpdateManifestAsync(string server, string manifestPath)
        {
            var candidates = new List<Uri>();
            if (Uri.TryCreate(manifestPath, UriKind.Absolute, out var absolute))
            {
                candidates.Add(absolute);
            }
            else
            {
                var path = NormalizePath(manifestPath);
                candidates.Add(new Uri(NormalizeHttpsBase(server), path));
                candidates.Add(new Uri(new Uri("https://devinecreations.net"), path));
            }

            Exception? lastError = null;
            foreach (var uri in candidates.DistinctBy(item => item.ToString()))
            {
                try
                {
                    await using var stream = await _httpClient.GetStreamAsync(uri);
                    return await JsonSerializer.DeserializeAsync<FlexPhoneUpdateManifest>(
                        stream,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (lastError is not null)
            {
                throw lastError;
            }

            return null;
        }

        public static string GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
                ?? "1.0.0";
        }

        private async Task<FlexPhoneActionResponse> PostControlAsync(string server, string token, object request)
        {
            var uri = new Uri(NormalizeHttpsBase(server), "/api/flexphone-client.php");
            var node = JsonNode.Parse(JsonSerializer.Serialize(request)) as JsonObject ?? [];
            if (!string.IsNullOrWhiteSpace(token))
            {
                node["session_token"] = token;
            }
            var payload = node.ToJsonString();
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
            if (!string.IsNullOrWhiteSpace(token))
            {
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await _httpClient.SendAsync(httpRequest);
            return await ReadJsonAsync<FlexPhoneActionResponse>(response) ?? new FlexPhoneActionResponse
            {
                Success = false,
                Error = response.IsSuccessStatusCode
                    ? "Flex Phone could not read the phone system reply."
                    : "The phone system could not complete the request."
            };
        }

        public async Task<bool> PostUserStatusAsync(string server, string extension, string status)
        {
            var uri = new Uri(NormalizeHttpsBase(server), "/api/flexphone/status");
            var payload = JsonSerializer.Serialize(new { extension, status, client = "Flex Phone" });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            try
            {
                var response = await _httpClient.PostAsync(uri, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<AccountRecoveryResponse> RequestAccountRecoveryAsync(
            string server,
            string path,
            string extension,
            string action,
            bool confirmed)
        {
            var payload = JsonSerializer.Serialize(new
            {
                extension = extension.Trim(),
                action,
                confirmed,
                client = "Flex Phone",
                delivery = "email"
            });

            var uri = BuildRecoveryUri(server, path);
            using var response = await PostJsonAsync(uri, payload);
            if (response.StatusCode == HttpStatusCode.NotFound && ShouldTryRecoveryFallback(uri))
            {
                using var fallbackResponse = await PostJsonAsync(
                    new Uri("https://pbx.tappedin.fm/api/flexphone-account-recovery.php"),
                    payload);
                return await ReadAccountRecoveryResponseAsync(fallbackResponse);
            }

            return await ReadAccountRecoveryResponseAsync(response);
        }

        private async Task<HttpResponseMessage> PostJsonAsync(Uri uri, string payload)
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            return await _httpClient.PostAsync(uri, content);
        }

        private static Uri BuildRecoveryUri(string server, string path)
        {
            if (Uri.TryCreate(path.Trim(), UriKind.Absolute, out var absoluteUri))
            {
                return absoluteUri;
            }

            return new Uri(NormalizeHttpsBase(server), NormalizePath(path));
        }

        private static bool ShouldTryRecoveryFallback(Uri uri)
        {
            return uri.Host.Equals("pbx.tappedin.fm", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("pbx.devinecreations.net", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("vps1.tappedin.fm", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<AccountRecoveryResponse> ReadAccountRecoveryResponseAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            AccountRecoveryResponse? parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<AccountRecoveryResponse>(
                    body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                // Fall through to the generic error below.
            }

            if (parsed is not null)
            {
                parsed.Success = parsed.Success && response.IsSuccessStatusCode;
                if (string.IsNullOrWhiteSpace(parsed.Message) && !response.IsSuccessStatusCode)
                {
                    parsed.Message = "The phone system could not complete the request right now.";
                }
                return parsed;
            }

            return new AccountRecoveryResponse
            {
                Success = false,
                Message = response.IsSuccessStatusCode
                    ? "The phone system sent a reply Flex Phone could not read."
                    : "The phone system could not complete the request right now."
            };
        }

        private async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            try
            {
                return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return default;
            }
        }

        public void OpenInBrowser(Uri uri)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.ToString(),
                UseShellExecute = true
            });
        }

        private static Uri NormalizeHttpsBase(string server)
        {
            var value = server.Trim();
            if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                value = $"https://{value}";
            }

            return new Uri(value.TrimEnd('/') + "/");
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/";
            }

            return path.StartsWith('/') ? path : "/" + path;
        }
    }
}
