using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace VRChatInstanceLogger
{
    public partial class LoginForm : Form
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VRChatInstanceLogger");
        private static readonly string CredentialsFile = Path.Combine(SettingsFolder, "credentials.dat");

        public string Email { get; private set; } = string.Empty;
        public string Password { get; private set; } = string.Empty;
        public string UserId { get; private set; } = string.Empty;
        public string DisplayName { get; private set; } = string.Empty;
        public CookieContainer? AuthCookieContainer { get; private set; }
        public bool IsTwoFactorAccount { get; private set; }

        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _userId = string.Empty;
        private string _displayName = string.Empty;
        private HttpClientHandler? _handler;
        private HttpClient? _client;
        private bool _waitingForTwoFactor;
        private bool _hasSavedPassword;

        public LoginForm()
        {
            InitializeComponent();
            UiTheme.Apply(this);
            LoadSavedCredentials();
        }

        public static bool TryLoadSavedCredentials(out string email, out string password, out CookieContainer? cookieContainer)
            => TryLoadSavedCredentials(out email, out password, out cookieContainer, out _, out _);

        public static bool TryLoadSavedCredentials(out string email, out string password, out CookieContainer? cookieContainer, out string userId)
            => TryLoadSavedCredentials(out email, out password, out cookieContainer, out userId, out _);

        public static bool TryLoadSavedCredentials(out string email, out string password, out CookieContainer? cookieContainer, out string userId, out string displayName)
        {
            email = string.Empty;
            password = string.Empty;
            cookieContainer = null;
            userId = string.Empty;
            displayName = string.Empty;

            if (!File.Exists(CredentialsFile))
                return false;

            try
            {
                var encrypted = File.ReadAllBytes(CredentialsFile);
                var jsonBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var data = JsonSerializer.Deserialize<CredentialData>(jsonBytes);
                if (data == null || string.IsNullOrWhiteSpace(data.Email) || string.IsNullOrWhiteSpace(data.Password))
                    return false;

                email = data.Email;
                password = data.Password;
                userId = data.UserId ?? string.Empty;
                displayName = data.DisplayName ?? string.Empty;
                cookieContainer = RestoreCookieContainer(data.Cookies);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Persists a freshly-resolved VRChat user id (and display name) onto the already-
        // saved credentials so future launches know the account without a lookup.
        public static void UpdateSavedUserId(string userId, string? displayName = null)
        {
            try
            {
                if (!File.Exists(CredentialsFile))
                    return;
                if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(displayName))
                    return;

                var encrypted = File.ReadAllBytes(CredentialsFile);
                var jsonBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var data = JsonSerializer.Deserialize<CredentialData>(jsonBytes);
                if (data == null)
                    return;

                if (!string.IsNullOrWhiteSpace(userId))
                    data.UserId = userId;
                if (!string.IsNullOrWhiteSpace(displayName))
                    data.DisplayName = displayName;

                var outBytes = JsonSerializer.SerializeToUtf8Bytes(data);
                var outEncrypted = ProtectedData.Protect(outBytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(CredentialsFile, outEncrypted);
            }
            catch
            {
                // Ignore persistence errors.
            }
        }

        // Re-persists the latest auth cookies onto the already-saved credentials. VRChat can
        // rotate the auth cookie during a session; saving the freshest jar after a successful
        // validation keeps the next launch logged in instead of falling back to a stale cookie
        // and forcing another 2FA prompt. Never overwrites good saved cookies with an empty set.
        public static void UpdateSavedCookies(CookieContainer? cookieContainer)
        {
            try
            {
                if (cookieContainer == null || !File.Exists(CredentialsFile))
                    return;

                var fresh = CaptureCookies(cookieContainer);
                if (fresh.Count == 0)
                    return;

                var encrypted = File.ReadAllBytes(CredentialsFile);
                var jsonBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var data = JsonSerializer.Deserialize<CredentialData>(jsonBytes);
                if (data == null)
                    return;

                data.Cookies = fresh;

                var outBytes = JsonSerializer.SerializeToUtf8Bytes(data);
                var outEncrypted = ProtectedData.Protect(outBytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(CredentialsFile, outEncrypted);
            }
            catch
            {
                // Ignore persistence errors.
            }
        }

        public static void ClearSavedCredentials()
        {
            try
            {
                if (File.Exists(CredentialsFile))
                    File.Delete(CredentialsFile);
            }
            catch
            {
                // Ignore cleanup errors.
            }
        }

        private static void SaveCredentials(string email, string password, CookieContainer? cookieContainer, string userId, string displayName)
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                    Directory.CreateDirectory(SettingsFolder);

                var data = new CredentialData
                {
                    Email = email,
                    Password = password,
                    UserId = userId,
                    DisplayName = displayName,
                    Cookies = CaptureCookies(cookieContainer)
                };

                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(data);
                var encrypted = ProtectedData.Protect(jsonBytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(CredentialsFile, encrypted);
            }
            catch
            {
                // Ignore persistence errors.
            }
        }

        private void LoadSavedCredentials()
        {
            if (TryLoadSavedCredentials(out var savedEmail, out var savedPassword, out _))
            {
                emailTextBox.Text = savedEmail;
                _email = savedEmail;
                // Never place the saved password into the visible textbox. Keeping it out of
                // any UI control means there is nothing on screen (even masked) that a tool or
                // injected code could un-mask. The password stays only in memory for the login
                // request below. A placeholder tells the user their credentials are remembered.
                _password = savedPassword;
                _hasSavedPassword = !string.IsNullOrEmpty(savedPassword);
                if (_hasSavedPassword)
                {
                    passwordTextBox.PlaceholderText = "(saved \u2014 leave blank to reuse)";
                }
            }
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            if (!_waitingForTwoFactor)
            {
                _email = emailTextBox.Text.Trim();

                // If the user typed a password, use it; otherwise fall back to the saved one
                // that was loaded into memory (never shown in the textbox).
                var typedPassword = passwordTextBox.Text;
                if (!string.IsNullOrEmpty(typedPassword))
                {
                    _password = typedPassword;
                }

                if (string.IsNullOrWhiteSpace(_email) || string.IsNullOrWhiteSpace(_password))
                {
                    MessageBox.Show("Please enter your VRChat email and password.", "Missing Credentials",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                TryStartLogin();
                return;
            }

            VerifyTwoFactor();
        }

        private async void TryStartLogin()
        {
            try
            {
                _handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
                _client = new HttpClient(_handler);
                ConfigureClient(_client);

                var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_email}:{_password}"));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

                var response = await _client.GetAsync("https://api.vrchat.cloud/api/1/auth/user");
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var details = string.IsNullOrWhiteSpace(json) ? response.ReasonPhrase : json;
                    MessageBox.Show($"Login failed ({(int)response.StatusCode}): {details}", "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (json.Contains("requiresTwoFactorAuth", StringComparison.OrdinalIgnoreCase))
                {
                    _waitingForTwoFactor = true;
                    twoFactorLabel.Visible = true;
                    twoFactorBox.Visible = true;
                    okButton.Text = "Verify 2FA";
                    twoFactorBox.Focus();
                    AuthCookieContainer = _handler.CookieContainer;
                    return;
                }

                _userId = ExtractUserId(json);
                _displayName = ExtractDisplayName(json);
                FinishLogin();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Login error: " + ex.Message, "Login Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void VerifyTwoFactor()
        {
            if (_client == null || _handler == null)
                return;

            var code = twoFactorBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show("Enter your 2FA code.", "Missing 2FA Code", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var content = new StringContent($"{{\"code\":\"{code}\"}}", Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("https://api.vrchat.cloud/api/1/auth/twofactorauth/totp/verify", content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var details = string.IsNullOrWhiteSpace(json) ? response.ReasonPhrase : json;
                    MessageBox.Show($"Invalid 2FA code ({(int)response.StatusCode}): {details}", "2FA Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    var userJson = await _client.GetStringAsync("https://api.vrchat.cloud/api/1/auth/user");
                    _userId = ExtractUserId(userJson);
                    _displayName = ExtractDisplayName(userJson);
                }
                catch
                {
                    // The account is authenticated even if the follow-up id lookup fails;
                    // the id can be resolved later from the main window.
                }

                FinishLogin();
            }
            catch (Exception ex)
            {
                MessageBox.Show("2FA error: " + ex.Message, "2FA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FinishLogin()
        {
            Email = _email;
            Password = _password;
            UserId = _userId;
            DisplayName = _displayName;
            IsTwoFactorAccount = _waitingForTwoFactor;
            AuthCookieContainer = _handler?.CookieContainer;
            SaveCredentials(_email, _password, AuthCookieContainer, _userId, _displayName);
            // Wipe any typed password out of the visible control now that it has been captured,
            // so it does not linger on screen (even masked) after a successful login.
            passwordTextBox.Clear();
            DialogResult = DialogResult.OK;
            Close();
        }

        private static string ExtractUserId(string json)
        {
            return ExtractStringProperty(json, "id");
        }

        private static string ExtractDisplayName(string json)
        {
            return ExtractStringProperty(json, "displayName");
        }

        private static string ExtractStringProperty(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty(propertyName, out var element) &&
                    element.ValueKind == JsonValueKind.String)
                {
                    return element.GetString() ?? string.Empty;
                }
            }
            catch
            {
                // Ignore malformed responses; the value can be resolved later.
            }

            return string.Empty;
        }

        private static List<SavedCookieData> CaptureCookies(CookieContainer? cookieContainer)
        {
            var cookies = new List<SavedCookieData>();
            if (cookieContainer == null)
                return cookies;

            try
            {
                var uri = new Uri("https://api.vrchat.cloud/");
                foreach (Cookie cookie in cookieContainer.GetCookies(uri))
                {
                    if (string.IsNullOrWhiteSpace(cookie.Name))
                        continue;

                    if (cookie.Expired)
                        continue;

                    cookies.Add(new SavedCookieData
                    {
                        Name = cookie.Name,
                        Value = cookie.Value,
                        Domain = string.IsNullOrWhiteSpace(cookie.Domain) ? ".vrchat.cloud" : cookie.Domain,
                        Path = string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
                        Secure = cookie.Secure,
                        HttpOnly = cookie.HttpOnly,
                        ExpiresUtc = cookie.Expires == DateTime.MinValue ? null : cookie.Expires.ToUniversalTime()
                    });
                }
            }
            catch
            {
                // Ignore cookie capture errors.
            }

            return cookies;
        }

        private static CookieContainer? RestoreCookieContainer(List<SavedCookieData>? savedCookies)
        {
            if (savedCookies == null || savedCookies.Count == 0)
                return null;

            var container = new CookieContainer();
            var apiUri = new Uri("https://api.vrchat.cloud/");
            var nowUtc = DateTime.UtcNow;

            foreach (var item in savedCookies)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Name))
                    continue;

                if (item.ExpiresUtc.HasValue && item.ExpiresUtc.Value <= nowUtc)
                    continue;

                try
                {
                    var cookie = new Cookie(
                        item.Name,
                        item.Value ?? string.Empty,
                        string.IsNullOrWhiteSpace(item.Path) ? "/" : item.Path)
                    {
                        Secure = item.Secure,
                        HttpOnly = item.HttpOnly
                    };

                    if (item.ExpiresUtc.HasValue)
                        cookie.Expires = item.ExpiresUtc.Value.ToLocalTime();

                    // Associate the cookie against the API host so the container resolves
                    // its domain/path exactly as it did when first received. Recreating a
                    // cookie with an explicit (host-only) domain and using Add(Cookie) can
                    // fail to match on later requests, silently dropping the saved session
                    // and forcing a fresh 2FA prompt on every launch.
                    container.Add(apiUri, cookie);
                }
                catch
                {
                    // Ignore bad cookie entries.
                }
            }

            return container;
        }

        private static void ConfigureClient(HttpClient client)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VRChat Group Auto Moderation/1.0");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private sealed class CredentialData
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public List<SavedCookieData> Cookies { get; set; } = new List<SavedCookieData>();
        }

        private sealed class SavedCookieData
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public string Domain { get; set; } = string.Empty;
            public string Path { get; set; } = "/";
            public bool Secure { get; set; }
            public bool HttpOnly { get; set; }
            public DateTime? ExpiresUtc { get; set; }
        }
    }
}
