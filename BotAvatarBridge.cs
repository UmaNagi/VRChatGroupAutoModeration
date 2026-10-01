using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace VRChatInstanceLogger
{
    internal enum AvatarAlertDeliveryResult
    {
        Posted,
        Suppressed,
        Failed
    }

    internal static class BotAvatarBridge
    {
        private const string AvatarEndpoint = "http://127.0.0.1:48125/avatar-observed";
        private const string PlayerPresenceEndpoint = "http://127.0.0.1:48125/player-presence";
        private const string LoggingStoppedEndpoint = "http://127.0.0.1:48125/logging-stopped";
        private static readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(3) };
        private static readonly string secretPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VRChatInstanceLogger", "discord-bot-bridge.secret");

        public static async Task<AvatarAlertDeliveryResult> SendAvatarObservedAsync(string ownerUserId, LoggerEngine.AvatarObservation observation)
        {
            var secret = TryReadSecret();
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(ownerUserId))
            return AvatarAlertDeliveryResult.Failed;

            var payload = new
            {
                ownerUserId,
                playerName = observation.DisplayName,
                playerId = observation.UserId,
                avatarName = observation.AvatarName,
                avatarId = observation.AvatarId,
                thumbnailUrl = observation.ThumbnailUrl,
                previewImage = observation.PreviewImage
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, AvatarEndpoint)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-AutoMod-Bridge-Token", secret);

            try
            {
                using var response = await client.SendAsync(request);
                return response.StatusCode == System.Net.HttpStatusCode.Accepted
                    ? AvatarAlertDeliveryResult.Posted
                    : response.StatusCode == System.Net.HttpStatusCode.NoContent
                        ? AvatarAlertDeliveryResult.Suppressed
                        : AvatarAlertDeliveryResult.Failed;
            }
            catch
            {
                return AvatarAlertDeliveryResult.Failed;
            }
        }

        public static async Task<bool> SendPlayerPresenceAsync(string ownerUserId, string playerName, string playerId, bool joined,
            string worldName, string worldId, string instanceId)
        {
            var secret = TryReadSecret();
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(ownerUserId))
                return false;

            var payload = new
            {
                ownerUserId,
                playerName,
                playerId,
                joined,
                worldName,
                worldId,
                instanceId
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, PlayerPresenceEndpoint)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-AutoMod-Bridge-Token", secret);

            try
            {
                using var response = await client.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> SendLoggingStoppedAsync(string ownerUserId)
        {
            var secret = TryReadSecret();
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(ownerUserId))
                return false;

            using var request = new HttpRequestMessage(HttpMethod.Post, LoggingStoppedEndpoint)
            {
                Content = new StringContent(JsonConvert.SerializeObject(new { ownerUserId }), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-AutoMod-Bridge-Token", secret);

            try
            {
                using var response = await client.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static string? TryReadSecret()
        {
            try
            {
                if (!File.Exists(secretPath))
                    return null;

                var protectedBytes = Convert.FromBase64String(File.ReadAllText(secretPath).Trim());
                var secretBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(secretBytes);
            }
            catch
            {
                return null;
            }
        }
    }
}