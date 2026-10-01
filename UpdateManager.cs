using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VRChatInstanceLogger
{
    /// <summary>
    /// Self-update via GitHub Releases. On startup it checks the configured repo for a
    /// newer release and downloads the new .exe into an "update" staging folder. When the
    /// app closes, the staged build is swapped in and the app relaunches.
    ///
    /// Configuration lives in update_config.txt in the app-data folder:
    ///   repo=owner/name      (required to enable updates)
    ///   token=ghp_xxx        (only needed for PRIVATE repos)
    /// </summary>
    public class UpdateManager
    {
        private const string ConfigFileName = "update_config.txt";
        private const string StageFolderName = "update";
        private const string StagedExeName = "staged.exe";
        private const string StagedVersionName = "staged.version";

        // Persistent record of the last version we actually tried to apply. Lives outside
        // the app folder (and the update/ staging folder, which the updater deletes) so it
        // survives the exe swap. Used to detect a broken/mislabeled release binary and stop
        // an endless update loop.
        private static readonly string AppliedMarkerPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VRChatInstanceLogger", "applied_update.txt");

        // Built-in defaults so the app auto-updates out of the box, with no need for the
        // user to edit update_config.txt. The config file (if present) overrides these.
        // For a PUBLIC repo, leave DefaultToken empty - no secret is shipped.
        private const string DefaultRepo = "UmaNagi/VRChatGroupAutoModeration";
        private const string DefaultToken = "";

        private static readonly HttpClient http = CreateClient();

        private readonly string baseDir;
        private readonly string configPath;
        private readonly string stageDir;
        private readonly string stagedExePath;
        private readonly string stagedVersionPath;

        private string repo = string.Empty;
        private string token = string.Empty;
        private string signerThumbprint = string.Empty;
        private bool updateApplied;
        private bool allowUnsignedUpdates;

        // Serializes update checks so a startup auto-check and a manual "Check for Updates"
        // click can't run DownloadAssetAsync at the same time and collide on the staged
        // .part file ("the process cannot access the file ... because it is being used by
        // another process").
        private readonly SemaphoreSlim checkGate = new SemaphoreSlim(1, 1);

        public event Action<string>? OnLog;

        /// <summary>
        /// True after the most recent check detected that a previously-applied update did not
        /// actually raise the running version (a broken/mislabeled release binary). Callers
        /// can use this to show a "failed to update" message instead of "up to date".
        /// </summary>
        public bool LastCheckFailedToApply { get; private set; }

        public Version CurrentVersion { get; } =
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

        public UpdateManager()
        {
            baseDir = RuntimePaths.Root;
            configPath = Path.Combine(baseDir, ConfigFileName);
            stageDir = Path.Combine(baseDir, StageFolderName);
            stagedExePath = Path.Combine(stageDir, StagedExeName);
            stagedVersionPath = Path.Combine(stageDir, StagedVersionName);
            LoadConfig();
        }

        public bool IsEnabled => !string.IsNullOrWhiteSpace(repo);

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VRChat-Group-Auto-Moderation");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        private void LoadConfig()
        {
            // Start from the built-in defaults so updates work even with no config file
            // or an empty one. A non-empty value in update_config.txt overrides them.
            repo = DefaultRepo;
            token = DefaultToken;
            signerThumbprint = string.Empty;
            allowUnsignedUpdates = false;

            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath,
@"# Auto-update settings.
#
# Auto-update is enabled by default using the app's built-in repo, so you can
# leave this file alone. Only set values here to point at a DIFFERENT repo.
#
# GitHub repo. Format: owner/name
#   e.g.  repo=YourName/VRChatGroupAutoModeration
repo=

# PRIVATE repos only: a GitHub personal access token with read access to the repo.
# WARNING: anyone who has this app can read this token. For wide/public distribution
# use a PUBLIC repo and leave this blank.
token=

# Required to enable self-updates. Use the SHA-1 thumbprint of the Authenticode
# certificate used to sign release executables.
signer_thumbprint=
                # Set true only for a trusted private repository when releases are not signed.
                allow_unsigned_updates=false
");
                return;
            }

            foreach (var raw in File.ReadAllLines(configPath))
            {
                var line = raw?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var idx = line.IndexOf('=');
                if (idx <= 0)
                    continue;

                var key = line.Substring(0, idx).Trim().ToLowerInvariant();
                var value = line.Substring(idx + 1).Trim();

                // Only override the built-in default when the file actually provides a value.
                if (key == "repo" && !string.IsNullOrWhiteSpace(value))
                    repo = value;
                else if (key == "token" && !string.IsNullOrWhiteSpace(value))
                    token = value;
                else if (key == "signer_thumbprint" && !string.IsNullOrWhiteSpace(value))
                    signerThumbprint = value.Replace(" ", string.Empty, StringComparison.Ordinal);
                else if (key == "allow_unsigned_updates" && bool.TryParse(value, out var allowUnsigned))
                    allowUnsignedUpdates = allowUnsigned;
            }
        }

        private void AddAuth(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        /// <summary>
        /// Checks the repo for a newer release and, if found, downloads it to the staging
        /// folder. Returns true if a newer build is staged and ready to install.
        /// </summary>
        public async Task<bool> CheckAndStageUpdateAsync()
        {
            if (!IsEnabled)
                return false;

            if (string.IsNullOrWhiteSpace(signerThumbprint) && !allowUnsignedUpdates)
            {
                OnLog?.Invoke("[UPDATE] Skipped: signer_thumbprint is not configured; unsigned updates are disabled.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(signerThumbprint))
                OnLog?.Invoke("[UPDATE] Warning: unsigned updates are enabled by local configuration.");

            // Don't try to self-update while running under the debugger / dev build.
            if (Debugger.IsAttached)
            {
                OnLog?.Invoke("[UPDATE] Skipped (running under debugger).");
                return false;
            }

            // Only one check/download at a time. If another check is already running
            // (e.g. the startup check when the user also clicks "Check for Updates"),
            // don't start a second download that would fight over the staged .part file.
            if (!await checkGate.WaitAsync(0))
            {
                OnLog?.Invoke("[UPDATE] An update check is already in progress.");
                return HasStagedUpdate();
            }

            try
            {
                LastCheckFailedToApply = false;
                OnLog?.Invoke($"[UPDATE] Current version {CurrentVersion}. Checking {repo} for updates...");

                var release = await GetLatestReleaseAsync();
                if (release == null)
                {
                    OnLog?.Invoke("[UPDATE] No release information available.");
                    return false;
                }

                var tag = release["tag_name"]?.ToString();
                if (!TryParseVersion(tag, out var latest))
                {
                    OnLog?.Invoke($"[UPDATE] Could not read latest version from tag '{tag}'.");
                    return false;
                }

                if (latest <= CurrentVersion)
                {
                    OnLog?.Invoke("[UPDATE] No updates available.");
                    ClearStagingIfObsolete(latest);
                    return false;
                }

                // Loop guard: if we already applied a build at or above this latest version
                // but we're STILL on an older version, the release binary is broken/mislabeled
                // (its assembly version doesn't match its release tag). Re-downloading it would
                // just loop forever, so stop and surface a clear message. A genuinely newer
                // release (tag above what we last applied) is not blocked.
                if (TryReadAppliedVersion(out var applied) && applied >= latest)
                {
                    LastCheckFailedToApply = true;
                    OnLog?.Invoke("[UPDATE] Failed to update properly. The owner is probably already fixing it.");
                    ClearStagingIfObsolete(latest);
                    return false;
                }

                // Already downloaded this version?
                if (File.Exists(stagedExePath) && File.Exists(stagedVersionPath) &&
                    TryParseVersion(File.ReadAllText(stagedVersionPath).Trim(), out var staged) &&
                    staged >= latest)
                {
                    OnLog?.Invoke($"[UPDATE] Update {latest} is ready. It will install when you restart the app.");
                    return true;
                }

                var asset = SelectExeAsset(release);
                if (asset == null)
                {
                    OnLog?.Invoke("[UPDATE] A newer release exists but it has no .exe asset to download.");
                    return false;
                }

                OnLog?.Invoke($"[UPDATE] Downloading update {latest}...");
                await DownloadAssetAsync(asset, latest);
                OnLog?.Invoke($"[UPDATE] Update {latest} downloaded. It will install automatically when you close and reopen the app.");
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[UPDATE] Update check failed: {ex.Message}");
                return false;
            }
            finally
            {
                checkGate.Release();
            }
        }

        private async Task<JObject?> GetLatestReleaseAsync()
        {
            var url = $"https://api.github.com/repos/{repo}/releases/latest";

            var response = await SendGitHubGetAsync(url, forceAnonymous: false);
            if (response == null)
                return null;

            // A stale/invalid token makes GitHub return 401 even for public repos. Fall back
            // to an anonymous request so public-repo updates keep working regardless of a bad
            // token left in update_config.txt.
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && !string.IsNullOrWhiteSpace(token))
            {
                response.Dispose();
                OnLog?.Invoke("[UPDATE] Configured token was rejected; retrying without it (public repo).");
                response = await SendGitHubGetAsync(url, forceAnonymous: true);
                if (response == null)
                    return null;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    OnLog?.Invoke($"[UPDATE] GitHub returned {(int)response.StatusCode} when checking releases.");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                return JObject.Parse(json);
            }
        }

        private async Task<HttpResponseMessage?> SendGitHubGetAsync(string url, bool forceAnonymous)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!forceAnonymous)
                    AddAuth(request);
                return await http.SendAsync(request);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[UPDATE] Update check request failed: {ex.Message}");
                return null;
            }
        }

        private static JObject? SelectExeAsset(JObject release)
        {
            if (release["assets"] is not JArray assets)
                return null;

            return assets
                .OfType<JObject>()
                .FirstOrDefault(a => (a["name"]?.ToString() ?? string.Empty)
                    .EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        }

        private async Task DownloadAssetAsync(JObject asset, Version version)
        {
            // Use the asset API URL with octet-stream so it works for both public and
            // private repos (browser_download_url requires no auth only for public repos).
            var assetApiUrl = asset["url"]?.ToString();
            if (string.IsNullOrWhiteSpace(assetApiUrl))
                throw new InvalidOperationException("Release asset has no download URL.");

            using var request = new HttpRequestMessage(HttpMethod.Get, assetApiUrl);
            AddAuth(request);
            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("application/octet-stream");

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            Directory.CreateDirectory(stageDir);

            // Use a unique temp name per download so a stale/locked .part left over from a
            // previously interrupted download can never block this one. Clean up any old
            // .part files best-effort first.
            CleanupStalePartFiles();
            var tempPath = stagedExePath + "." + Guid.NewGuid().ToString("N") + ".part";

            try
            {
                await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }

                if (File.Exists(stagedExePath))
                    File.Delete(stagedExePath);
                File.Move(tempPath, stagedExePath);
                File.WriteAllText(stagedVersionPath, version.ToString());
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                catch { /* best effort */ }
            }
        }

        private void CleanupStalePartFiles()
        {
            try
            {
                if (!Directory.Exists(stageDir))
                    return;

                foreach (var part in Directory.EnumerateFiles(stageDir, "*.part"))
                {
                    try { File.Delete(part); }
                    catch { /* still locked by another process; skip it */ }
                }
            }
            catch { /* best effort */ }
        }

        private void ClearStagingIfObsolete(Version latest)
        {
            try
            {
                if (File.Exists(stagedVersionPath) &&
                    TryParseVersion(File.ReadAllText(stagedVersionPath).Trim(), out var staged) &&
                    staged <= CurrentVersion)
                {
                    if (Directory.Exists(stageDir))
                        Directory.Delete(stageDir, true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        public bool HasStagedUpdate()
        {
            try
            {
                if (!File.Exists(stagedExePath) || !File.Exists(stagedVersionPath))
                    return false;

                return TryParseVersion(File.ReadAllText(stagedVersionPath).Trim(), out var staged) &&
                       staged > CurrentVersion;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// If a newer build is staged, launches a detached updater that waits for this
        /// process to exit, swaps in the new exe, and relaunches it. Call from FormClosing.
        /// </summary>
        public void ApplyStagedUpdateIfPresent()
        {
            if (updateApplied)
                return;

            if (!HasStagedUpdate())
                return;

            var targetExe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe))
                return;

            // Only meaningful for the published single-file exe.
            if (!targetExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var scriptPath = Path.Combine(stageDir, "apply_update.ps1");
                File.WriteAllText(scriptPath, UpdaterScript);

                // Record which version we're about to apply, in a location that survives the
                // swap, so the next launch can tell whether the update actually took effect.
                RecordAppliedVersionFromStage();

                var pid = Environment.ProcessId;
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" " +
                                $"-ProcessId {pid} -Source \"{stagedExePath}\" -Target \"{targetExe}\" " +
                                $"-StageDir \"{stageDir}\" -SignerThumbprint \"{signerThumbprint}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
                updateApplied = true;
            }
            catch
            {
                // If the updater fails to launch, the app simply runs the old version.
            }
        }

        private static bool TryReadAppliedVersion(out Version version)
        {
            version = new Version(0, 0, 0, 0);
            try
            {
                if (!File.Exists(AppliedMarkerPath))
                    return false;
                return TryParseVersion(File.ReadAllText(AppliedMarkerPath).Trim(), out version);
            }
            catch
            {
                return false;
            }
        }

        private void RecordAppliedVersionFromStage()
        {
            try
            {
                if (!File.Exists(stagedVersionPath))
                    return;
                var version = File.ReadAllText(stagedVersionPath).Trim();
                var dir = Path.GetDirectoryName(AppliedMarkerPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(AppliedMarkerPath, version);
            }
            catch
            {
                // Best effort; the loop guard just won't engage if this can't be written.
            }
        }

        private static bool TryParseVersion(string? text, out Version version)
        {
            version = new Version(0, 0, 0, 0);
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(1);

            return Version.TryParse(trimmed, out version!);
        }

        private const string UpdaterScript =
@"param(
    [int]$ProcessId,
    [string]$Source,
    [string]$Target,
    [string]$StageDir,
    [string]$SignerThumbprint
)

# Wait for the app to fully exit so the exe is unlocked.
try { Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction SilentlyContinue } catch {}

$copied = $false
$signature = Get-AuthenticodeSignature -FilePath $Source
if ($signature.Status -ne 'Valid' -or
    $signature.SignerCertificate.Thumbprint.Replace(' ', '') -ne $SignerThumbprint.Replace(' ', '')) {
    Remove-Item -LiteralPath $StageDir -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}

for ($i = 0; $i -lt 30; $i++) {
    try {
        Copy-Item -LiteralPath $Source -Destination $Target -Force
        $copied = $true
        break
    } catch {
        Start-Sleep -Milliseconds 500
    }
}

if ($copied) {
    Remove-Item -LiteralPath $StageDir -Recurse -Force -ErrorAction SilentlyContinue
}

Start-Process -FilePath $Target
";
    }
}
