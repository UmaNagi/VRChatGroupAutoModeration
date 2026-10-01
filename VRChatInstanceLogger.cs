using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace VRChatInstanceLogger
{
    public partial class VRChatInstanceLogger : Form
    {
        private LoggerEngine? logger;
        private VRChatAPI? api;
        private string? email = null;
        private string? password = null;
        private string? userId = null;
        private string? displayName = null;
        private CookieContainer? authCookies;
        private int instanceBanCount;
        private readonly List<(string GroupId, string GroupName)> blacklistedGroupItems = new List<(string GroupId, string GroupName)>();
        private int blacklistHighlightGeneration;
        private readonly List<(string GroupId, string GroupName)> ownedGroupItems = new List<(string GroupId, string GroupName)>();
        private readonly List<(string GroupId, string GroupName)> staffGroupItems = new List<(string GroupId, string GroupName)>();
        private readonly List<(string GroupId, string GroupName)> whitelistedGroupItems = new List<(string GroupId, string GroupName)>();
        private readonly List<string> blacklistedAvatarItems = new List<string>();
        private int avatarHighlightGeneration;
        private FileSystemWatcher? avatarBlacklistWatcher;
        private readonly System.Windows.Forms.Timer startCooldownTimer = new System.Windows.Forms.Timer();
        private int startCooldownRemainingSeconds;
        private readonly UpdateManager updateManager = new UpdateManager();
        private bool updatePending;
        private readonly DiscordWebhook discordWebhook = new DiscordWebhook();
        private readonly object avatarWebhookLock = new object();
        private readonly Dictionary<string, PendingAvatarWebhook> pendingAvatarWebhooks = new Dictionary<string, PendingAvatarWebhook>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan avatarWebhookQuietPeriod = TimeSpan.FromSeconds(3);
        private readonly object autoBanWebhookLock = new object();
        private readonly Dictionary<string, DateTime> recentAutoBanWebhookUsers = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan autoBanWebhookAuditWindow = TimeSpan.FromMinutes(10);
        private volatile string currentWorldName = "(none)";
        private readonly ComboBox themePresetComboBox = new ComboBox();

        // Tabbed body: the dashboard, a dedicated logs page, and a world-info page.
        private TabControl bodyTabs = null!;
        private TabPage dashboardTab = null!;
        private TabPage worldInfoTab = null!;
        private TabPage logsTab = null!;
        private TabPage udonLogTab = null!;
        private TabPage blacklistTab = null!;
        private TabPage webhooksTab = null!;
        private TabPage moderationsTab = null!;
        private TabPage worldModerationsTab = null!;
        private TabPage groupRelatedTab = null!;
        private TabPage lobbyPlayersTab = null!;
        private readonly Label worldInfoHeaderLabel = new Label();
        private readonly Label udonLogsLabel = new Label();
        private readonly Label udonFloodLabel = new Label();
        private readonly RichTextBox udonLogBox = new RichTextBox();
        private readonly RichTextBox udonFloodBox = new RichTextBox();
        private readonly FlowLayoutPanel logCardsPanel = new FlowLayoutPanel();
        private readonly Button clearLogsButton = new Button();
        private readonly RichTextBox worldInfoBox = new RichTextBox();
        private readonly Label dashboardUserHeaderLabel = new Label();
        private readonly RichTextBox dashboardUserInfoBox = new RichTextBox();
        private readonly Label worldModerationsHeaderLabel = new Label();
        private readonly RichTextBox worldModerationsBox = new RichTextBox();
        private readonly RichTextBox worldModerationScriptBox = new RichTextBox();
        private readonly Button worldModerationScriptButton = new Button();
        private readonly Button worldModerationCopyButton = new Button();
        private readonly Button worldModerationSaveButton = new Button();
        private readonly Button worldModerationOpenFolderButton = new Button();
        private readonly Button worldModerationDetectButton = new Button();
        private readonly Button worldModerationToggleButton = new Button();
        private readonly Button worldModerationClearButton = new Button();
        private readonly Button logSizeDownButton = new Button();
        private readonly Button logSizeUpButton = new Button();
        // Flagged-words watchlist (warning-only) controls, on the Blacklist tab.
        private readonly Label flaggedWordsLabel = new Label();
        private readonly RichTextBox flaggedWordsBox = new RichTextBox();
        private readonly TextBox addFlaggedWordBox = new TextBox();
        private readonly Button addFlaggedWordButton = new Button();
        private readonly Button removeFlaggedWordButton = new Button();
        private readonly Button clearFlaggedWordsButton = new Button();
        private readonly List<string> flaggedWordItems = new List<string>();
        private readonly Label alertWebhookExplanationLabel = new Label();
        private readonly TextBox alertWebhookBox = new TextBox();
        private readonly Label forumWebhookExplanationLabel = new Label();
        private readonly TextBox forumWebhookBox = new TextBox();
        private readonly Button saveWebhooksButton = new Button();
        // Rows-based lobby list (name + per-player action buttons) on its own tab.
        private readonly FlowLayoutPanel lobbyPlayersPanel = new FlowLayoutPanel();
        private readonly Label inInstanceStaffLabel = new Label();
        private readonly RichTextBox inInstanceStaffBox = new RichTextBox();
        private readonly Label whitelistedGroupsLabel = new Label();
        private readonly RichTextBox whitelistedGroupsBox = new RichTextBox();
        private readonly TextBox addWhitelistedGroupIdBox = new TextBox();
        private readonly Button addWhitelistedGroupButton = new Button();
        private readonly Button removeWhitelistedGroupButton = new Button();
        private readonly Button clearGroupBlacklistButton = new Button();
        private readonly Button clearAvatarBlacklistButton = new Button();

        private sealed class PendingAvatarWebhook
        {
            public string AvatarName { get; set; } = string.Empty;
            public string AvatarId { get; set; } = string.Empty;
            public string? ImageUrl { get; set; }
            public HashSet<string> Players { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public CancellationTokenSource Cancellation { get; set; } = new CancellationTokenSource();
        }

        public VRChatInstanceLogger()
        {
            InitializeComponent();
            AddThemePresetSelector();
            UiTheme.Apply(this);
            StyleSectionHeaders();
            bannedPlayersPanel.BackColor = UiTheme.Surface;
            SetupTabs();
            LoadFlaggedWordsDisplay();
            LoadSavedLogin();
            UpdateWebhookControlsEnabled();
            _ = PreloadBlacklistedGroupsAsync();
            _ = PreloadBlacklistedAvatarsAsync();
            _ = PreloadOwnedGroupsAsync();
            _ = PreloadStaffGroupsAsync();
            LoadWhitelistedGroupsDisplay();
            _ = RefreshOwnedGroupNameLabelAsync();
            SetGroupModerationStatus(false);
            SetInstanceBanCount(0);
            startCooldownTimer.Interval = 1000;
            startCooldownTimer.Tick += StartCooldownTimer_Tick;
            this.Resize += (s, e) => LayoutAllTabs();
            this.Resize += (s, e) => this.Invalidate();
            this.Shown += async (s, e) =>
            {
                LayoutAllTabs();
                if (Array.Exists(Environment.GetCommandLineArgs(), arg =>
                    string.Equals(arg, "--send-webhook-test", StringComparison.OrdinalIgnoreCase)))
                {
                    await SendWebhookTestAsync();
                    Close();
                }
                else if (Array.Exists(Environment.GetCommandLineArgs(), arg =>
                    string.Equals(arg, "--send-ban-webhook-test", StringComparison.OrdinalIgnoreCase)))
                {
                    await SendBanWebhookTestAsync();
                    Close();
                }
            };
            bannedPlayersPanel.Resize += (s, e) => SyncBannedRowWidths();
            LayoutAllTabs();
            updateManager.OnLog += AppendLog;
            discordWebhook.OnLog += AppendLog;
            _ = CheckUpdateOnStartupAsync();
        }

        private void AddThemePresetSelector()
        {
            themePresetComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            themePresetComboBox.FlatStyle = FlatStyle.Flat;
            themePresetComboBox.BackColor = UiTheme.Surface;
            themePresetComboBox.ForeColor = UiTheme.TextPrimary;
            themePresetComboBox.Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Regular);
            themePresetComboBox.Items.AddRange(new object[]
            {
                "Green Aero",
                "Blue Aero",
                "Lavender Aero",
                "Dark Glass",
                "Minimal White",
                "Orange Aero",
                "Black Theme",
                "Red Theme",
                "Yellow Aero",
                "Teal Aero",
                "Pink Aero",
                "Cyan Aero"
            });
            themePresetComboBox.SelectedIndex = (int)ThemePreset.GreenAero;
            themePresetComboBox.Width = 140;
            themePresetComboBox.Height = 28;
            themePresetComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            themePresetComboBox.Location = new Point(this.ClientSize.Width - themePresetComboBox.Width - 16, 8);
            themePresetComboBox.SelectedIndexChanged += (s, e) =>
            {
                try
                {
                    ApplyThemeSelection();
                }
                catch
                {
                    themePresetComboBox.SelectedIndex = (int)ThemePreset.GreenAero;
                    UiTheme.ApplyTheme(this, ThemePreset.GreenAero);
                    StyleSectionHeaders();
                    Invalidate();
                }
            };

            UiTheme.ApplyTheme(this, ThemePreset.GreenAero);

            this.Controls.Add(themePresetComboBox);
            this.Resize += (s, e) =>
            {
                int rightEdge = this.ClientSize.Width - 16;
                int themeX = Math.Max(15, rightEdge - themePresetComboBox.Width);
                themePresetComboBox.Location = new Point(themeX, 8);
            };
        }

        private async Task SendWebhookTestAsync()
        {
            if (!IsLoggedIn)
            {
                AppendLog("[WEBHOOK] Log in before sending a webhook test.");
                return;
            }

            if (!discordWebhook.IsEnabled)
            {
                AppendLog("[WEBHOOK] No alert webhook is configured for this account.");
                return;
            }

            var worldName = string.IsNullOrWhiteSpace(currentWorldName) ? "No active world" : currentWorldName;
            var worldId = logger?.CurrentWorldId ?? "No active world";
            var instanceId = logger?.CurrentInstanceId ?? "No active instance";
            const string testUserId = "usr_webhook-test";

            await discordWebhook.SendEmbedAsync(
                "Webhook test - player joined format",
                "This is a test message. No player joined or was moderated.",
                DiscordWebhook.ColorInfo,
                new[]
                {
                    ("Player", "Webhook Test Player", true),
                    ("User ID", testUserId, true),
                    ("Profile", $"https://vrchat.com/home/user/{testUserId}", false),
                    ("World", worldName, true),
                    ("World ID", worldId, false),
                    ("Instance", instanceId, false),
                    ("Event time (UTC)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"), false)
                });
            AppendLog("[WEBHOOK] Detailed test embed sent.");
        }

        private async Task SendBanWebhookTestAsync()
        {
            if (!IsLoggedIn)
            {
                AppendLog("[WEBHOOK] Log in before sending a webhook test.");
                return;
            }

            if (!discordWebhook.IsEnabled)
            {
                AppendLog("[WEBHOOK] No alert webhook is configured for this account.");
                return;
            }

            var worldName = string.IsNullOrWhiteSpace(currentWorldName) ? "No active world" : currentWorldName;
            const string testUserId = "usr_webhook-ban-test";
            const string testPlayer = "Webhook Ban Test Player";
            const string testReason = "test ban webhook (no real player was banned)";
            var testStaffName = string.IsNullOrWhiteSpace(displayName) ? "Webhook Test Staff Member" : displayName;

            await discordWebhook.SendEmbedAsync(
                "\uD83D\uDD28 Player banned",
                $"**{testPlayer}** was banned from your group.",
                DiscordWebhook.ColorBan,
                new[]
                {
                    ("Player", testPlayer, true),
                    ("User ID", testUserId, true),
                    ("Banned by", testStaffName, true),
                    ("Reason", testReason, false),
                    ("World", worldName, false)
                });
            AppendLog("[WEBHOOK] Ban test embed sent. No real player was banned.");
        }

        private void ApplyThemeSelection()
        {
            var selectedPreset = themePresetComboBox.SelectedIndex switch
            {
                0 => ThemePreset.GreenAero,
                1 => ThemePreset.BlueAero,
                2 => ThemePreset.LavenderAero,
                3 => ThemePreset.DarkGlass,
                4 => ThemePreset.MinimalWhite,
                5 => ThemePreset.OrangeAero,
                6 => ThemePreset.BlackTheme,
                7 => ThemePreset.RedTheme,
                8 => ThemePreset.YellowAero,
                9 => ThemePreset.TealAero,
                10 => ThemePreset.PinkAero,
                _ => ThemePreset.CyanAero
            };

            UiTheme.ApplyTheme(this, selectedPreset);
            StyleSectionHeaders();
            Invalidate();
        }

        // Accents the section headers with VRChat's signal blue for a branded look.
        private void StyleSectionHeaders()
        {
            var headers = new[]
            {
                logsLabel, udonLogsLabel, udonFloodLabel, blacklistedGroupsLabel, blacklistedAvatarsLabel, banReasonLabel,
                bannedPlayersLabel, lobbyPlayersLabel, ownedGroupsLabel, staffGroupsLabel, inInstanceStaffLabel, whitelistedGroupsLabel
            };

            foreach (var header in headers)
            {
                if (header == null)
                    continue;
                header.ForeColor = UiTheme.Accent;
                try { header.Font = new System.Drawing.Font("Segoe UI Semibold", header.Font.Size, System.Drawing.FontStyle.Bold); }
                catch { /* keep existing font if unavailable */ }
            }

            if (madeByLabel != null)
            {
                madeByLabel.ForeColor = UiTheme.Accent;
                try { madeByLabel.Font = new System.Drawing.Font("Segoe UI Semibold", madeByLabel.Font.Size, System.Drawing.FontStyle.Bold); }
                catch { /* keep existing font if unavailable */ }
            }
        }

        private async Task CheckUpdateOnStartupAsync()
        {
            if (!updateManager.IsEnabled)
            {
                // No update source configured — nothing to verify against.
                updatePending = false;
                SetUpdateStatus("Update: not configured", UiTheme.TextMuted);
                return;
            }

            SetUpdateStatus("Update: checking...", UiTheme.TextMuted);

            bool updateAvailable;
            try
            {
                updateAvailable = await updateManager.CheckAndStageUpdateAsync();
            }
            catch
            {
                // If the check fails (e.g. offline), don't block the user.
                updatePending = false;
                SetUpdateStatus("Update: check failed", UiTheme.TextMuted);
                return;
            }

            updatePending = updateAvailable;
            if (updateAvailable)
                SetUpdateStatus("App needs to be updated", UiTheme.LogDanger);
            else if (updateManager.LastCheckFailedToApply)
                SetUpdateStatus("Update failed \u2014 owner is likely fixing it", UiTheme.LogDanger);
            else
                SetUpdateStatus("App up to date", UiTheme.LogJoin);
        }

        private void SetUpdateStatus(string text, System.Drawing.Color color)
        {
            if (updateStatusLabel.InvokeRequired)
            {
                updateStatusLabel.Invoke(new Action(() => SetUpdateStatus(text, color)));
                return;
            }
            updateStatusLabel.Text = text;
            updateStatusLabel.ForeColor = color;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            try
            {
                updateManager.ApplyStagedUpdateIfPresent();
            }
            catch
            {
                // Never block app shutdown on an update failure.
            }
        }

        private async void UpdateButton_Click(object? sender, EventArgs e)
        {
            if (!updateManager.IsEnabled)
            {
                AppendLog("[UPDATE] Auto-update is not configured. Set 'repo=owner/name' in update_config.txt.");
                MessageBox.Show(
                    "Auto-update is not configured.\n\nOpen update_config.txt in the app-data folder and set:\n    repo=owner/name\n\nFor a private repo, also set a token.",
                    "Update not configured",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            updateButton.Enabled = false;
            var originalText = updateButton.Text;
            updateButton.Text = "Checking...";

            bool updateReady = false;
            try
            {
                updateReady = await updateManager.CheckAndStageUpdateAsync();
            }
            catch
            {
                updateReady = false;
            }

            if (updateReady)
            {
                updatePending = true;
                SetUpdateStatus("App needs to be updated", System.Drawing.Color.Red);
                updateButton.Text = "Updating...";
                AppendLog("[UPDATE] Restarting to install the update...");
                // Stage the swap, then close. The updater waits for exit, replaces the
                // exe, and relaunches the app on the new version.
                updateManager.ApplyStagedUpdateIfPresent();
                Application.Exit();
                return;
            }

            updatePending = false;
            if (updateManager.LastCheckFailedToApply)
                SetUpdateStatus("Update failed \u2014 owner is likely fixing it", System.Drawing.Color.Red);
            else
                SetUpdateStatus("App up to date", System.Drawing.Color.Green);
            updateButton.Text = originalText;
            updateButton.Enabled = true;
        }

        // Builds the tabbed body (Dashboard / World Information / Logs) and reparents the
        // existing controls into it. Called once after InitializeComponent.
        private void SetupTabs()
        {
            bodyTabs = new TabControl();
            bodyTabs.Appearance = TabAppearance.Normal;
            bodyTabs.Multiline = false;
            bodyTabs.SelectedIndex = 0;
            dashboardTab = new TabPage("Dashboard");
            moderationsTab = new TabPage("Moderations");
            worldModerationsTab = new TabPage("World Moderations");
            groupRelatedTab = new TabPage("Group Related");
            blacklistTab = new TabPage("Blacklist");
            webhooksTab = new TabPage("Webhooks");
            worldInfoTab = new TabPage("World Information");
            lobbyPlayersTab = new TabPage("Lobby Players");
            logsTab = new TabPage("Logs");
            udonLogTab = new TabPage("Udon Logs");

            bodyTabs.Controls.Add(dashboardTab);
            bodyTabs.Controls.Add(worldInfoTab);
            bodyTabs.Controls.Add(lobbyPlayersTab);
            bodyTabs.Controls.Add(groupRelatedTab);
            bodyTabs.Controls.Add(blacklistTab);
            bodyTabs.Controls.Add(webhooksTab);
            bodyTabs.Controls.Add(moderationsTab);
            bodyTabs.Controls.Add(logsTab);
            bodyTabs.Controls.Add(udonLogTab);

            bodyTabs.BackColor = UiTheme.Background;
            bodyTabs.ForeColor = UiTheme.TextPrimary;
            foreach (TabPage page in bodyTabs.TabPages)
            {
                page.BackColor = UiTheme.Background;
                page.ForeColor = UiTheme.TextPrimary;
            }

            // Blacklist controls live on their own tab.
            var blacklistControls = new Control[]
            {
                blacklistedGroupsLabel, blacklistedGroupsBox, addGroupIdBox, addGroupButton, removeGroupButton, clearGroupBlacklistButton,
                blacklistedAvatarsLabel, blacklistedAvatarsBox, addAvatarIdBox, addAvatarButton, removeAvatarButton, clearAvatarBlacklistButton
            };
            foreach (var c in blacklistControls)
            {
                this.Controls.Remove(c);
                blacklistTab.Controls.Add(c);
            }

            clearGroupBlacklistButton.Text = "Clear list";
            clearAvatarBlacklistButton.Text = "Clear list";
            UiTheme.StyleButton(clearGroupBlacklistButton);
            UiTheme.StyleButton(clearAvatarBlacklistButton);
            clearGroupBlacklistButton.Click += ClearGroupBlacklistButton_Click;
            clearAvatarBlacklistButton.Click += ClearAvatarBlacklistButton_Click;

            // Flagged-words watchlist (warning-only), to the right of blacklisted avatars.
            flaggedWordsLabel.Text = "Flagged words (warn only)";
            flaggedWordsLabel.ForeColor = UiTheme.Accent;
            flaggedWordsLabel.BackColor = System.Drawing.Color.Transparent;
            flaggedWordsLabel.AutoSize = true;
            try { flaggedWordsLabel.Font = new System.Drawing.Font("Segoe UI Semibold", flaggedWordsLabel.Font.Size, System.Drawing.FontStyle.Bold); }
            catch { /* keep default font */ }
            flaggedWordsBox.ReadOnly = true;
            flaggedWordsBox.BorderStyle = BorderStyle.None;
            flaggedWordsBox.BackColor = UiTheme.Surface;
            flaggedWordsBox.ForeColor = UiTheme.TextPrimary;
            addFlaggedWordBox.BorderStyle = BorderStyle.FixedSingle;
            addFlaggedWordBox.BackColor = UiTheme.Surface;
            addFlaggedWordBox.ForeColor = UiTheme.TextPrimary;
            addFlaggedWordBox.PlaceholderText = "Enter a word or phrase";
            addFlaggedWordButton.Text = "Add word";
            removeFlaggedWordButton.Text = "Remove";
            clearFlaggedWordsButton.Text = "Clear list";
            UiTheme.StyleButton(addFlaggedWordButton);
            UiTheme.StyleButton(removeFlaggedWordButton);
            UiTheme.StyleButton(clearFlaggedWordsButton);
            addFlaggedWordButton.Click += AddFlaggedWordButton_Click;
            removeFlaggedWordButton.Click += RemoveFlaggedWordButton_Click;
            clearFlaggedWordsButton.Click += ClearFlaggedWordsButton_Click;
            blacklistTab.Controls.Add(flaggedWordsLabel);
            blacklistTab.Controls.Add(flaggedWordsBox);
            blacklistTab.Controls.Add(addFlaggedWordBox);
            blacklistTab.Controls.Add(addFlaggedWordButton);
            blacklistTab.Controls.Add(removeFlaggedWordButton);
            blacklistTab.Controls.Add(clearFlaggedWordsButton);

            alertWebhookExplanationLabel.Text = "Top link: regular alerts such as player joins, leaves, bans, and blacklist matches go here.";
            forumWebhookExplanationLabel.Text = "Bottom link: moderation actions are posted as separate topics in a Discord Forum channel. Leave it blank if you do not use a Forum channel.";
            foreach (var label in new[] { alertWebhookExplanationLabel, forumWebhookExplanationLabel })
            {
                label.AutoSize = false;
                label.ForeColor = UiTheme.TextPrimary;
                label.BackColor = System.Drawing.Color.Transparent;
            }
            alertWebhookBox.BorderStyle = BorderStyle.FixedSingle;
            forumWebhookBox.BorderStyle = BorderStyle.FixedSingle;
            alertWebhookBox.PlaceholderText = "https://discord.com/api/webhooks/...";
            forumWebhookBox.PlaceholderText = "https://discord.com/api/webhooks/... (optional)";
            saveWebhooksButton.Text = "Save webhooks";
            UiTheme.StyleButton(saveWebhooksButton);
            saveWebhooksButton.Click += SaveWebhooksButton_Click;
            webhooksTab.Controls.Add(alertWebhookExplanationLabel);
            webhooksTab.Controls.Add(alertWebhookBox);
            webhooksTab.Controls.Add(forumWebhookExplanationLabel);
            webhooksTab.Controls.Add(forumWebhookBox);
            webhooksTab.Controls.Add(saveWebhooksButton);

            // Ban reason + banned players go on the Moderations tab.
            var moderationControls = new Control[]
            {
                banReasonLabel, banReasonBox, bannedPlayersLabel, bannedPlayersPanel
            };
            foreach (var c in moderationControls)
            {
                this.Controls.Remove(c);
                moderationsTab.Controls.Add(c);
            }

            // Managed group controls belong on the Group Related tab.
            var groupRelatedControls = new Control[]
            {
                ownedGroupsLabel, ownedGroupsBox, addOwnedGroupIdBox, addOwnedGroupButton, removeOwnedGroupButton,
                staffGroupsLabel, staffGroupsBox, addStaffGroupIdBox, addStaffGroupButton, removeStaffGroupButton,
                whitelistedGroupsLabel, whitelistedGroupsBox, addWhitelistedGroupIdBox, addWhitelistedGroupButton, removeWhitelistedGroupButton
            };
            foreach (var c in groupRelatedControls)
            {
                this.Controls.Remove(c);
                groupRelatedTab.Controls.Add(c);
            }

            inInstanceStaffLabel.Text = "Current staff in instance";
            inInstanceStaffBox.ReadOnly = true;
            inInstanceStaffBox.BorderStyle = BorderStyle.None;
            inInstanceStaffBox.BackColor = UiTheme.Surface;
            inInstanceStaffBox.ForeColor = UiTheme.TextPrimary;
            groupRelatedTab.Controls.Add(inInstanceStaffLabel);
            groupRelatedTab.Controls.Add(inInstanceStaffBox);

            whitelistedGroupsLabel.Text = "Group whitelist (bypasses all bans)";
            addWhitelistedGroupIdBox.PlaceholderText = "Enter group ID (grp_...)";
            addWhitelistedGroupButton.Text = "Add group";
            removeWhitelistedGroupButton.Text = "Remove";
            UiTheme.StyleButton(addWhitelistedGroupButton);
            UiTheme.StyleButton(removeWhitelistedGroupButton);
            addWhitelistedGroupIdBox.Enabled = false;
            addWhitelistedGroupButton.Enabled = false;
            removeWhitelistedGroupButton.Enabled = false;
            addWhitelistedGroupButton.Click += AddWhitelistedGroupButton_Click;
            removeWhitelistedGroupButton.Click += RemoveWhitelistedGroupButton_Click;
            whitelistedGroupsBox.ReadOnly = true;
            whitelistedGroupsBox.BorderStyle = BorderStyle.None;
            whitelistedGroupsBox.BackColor = UiTheme.Surface;
            whitelistedGroupsBox.ForeColor = UiTheme.TextPrimary;
            groupRelatedTab.Controls.Add(whitelistedGroupsLabel);
            groupRelatedTab.Controls.Add(whitelistedGroupsBox);
            groupRelatedTab.Controls.Add(addWhitelistedGroupIdBox);
            groupRelatedTab.Controls.Add(addWhitelistedGroupButton);
            groupRelatedTab.Controls.Add(removeWhitelistedGroupButton);

            // Dashboard and log controls also need to be owned by their tab pages.
            var dashboardControls = new Control[]
            {
                dashboardUserHeaderLabel, dashboardUserInfoBox
            };
            dashboardUserHeaderLabel.Text = "Logged-in account";
            foreach (var c in dashboardControls)
            {
                this.Controls.Remove(c);
                dashboardTab.Controls.Add(c);
            }

            var logControls = new Control[]
            {
                logsLabel, clearLogsButton, logBox, logCardsPanel, udonLogsLabel, udonFloodLabel, udonLogBox, udonFloodBox
            };
            foreach (var c in logControls)
            {
                this.Controls.Remove(c);
            }
            logsTab.Controls.Add(logsLabel);
            clearLogsButton.Text = "Clear Logs";
            UiTheme.StyleButton(clearLogsButton);
            clearLogsButton.Click += ClearLogsButton_Click;
            logsTab.Controls.Add(clearLogsButton);
            logsTab.Controls.Add(logBox);
            logsTab.Controls.Add(logCardsPanel);
            logCardsPanel.FlowDirection = FlowDirection.TopDown;
            logCardsPanel.WrapContents = false;
            logCardsPanel.AutoScroll = true;
            logCardsPanel.Padding = new Padding(6);
            logCardsPanel.BackColor = UiTheme.Surface;
            logBox.Visible = false;
            udonLogTab.Controls.Add(udonLogsLabel);
            udonLogTab.Controls.Add(udonFloodLabel);
            udonLogTab.Controls.Add(udonLogBox);
            udonLogTab.Controls.Add(udonFloodBox);

            // The legacy lobby text box is replaced by the row-based lobby panel.
            this.Controls.Remove(lobbyPlayersBox);

            worldModerationsHeaderLabel.Text = "World Staff Moderation";
            worldModerationsHeaderLabel.ForeColor = UiTheme.Accent;
            worldModerationsHeaderLabel.BackColor = System.Drawing.Color.Transparent;
            worldModerationsHeaderLabel.AutoSize = true;
            try { worldModerationsHeaderLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 12f, System.Drawing.FontStyle.Bold); }
            catch { /* keep default font */ }

            worldModerationsBox.ReadOnly = true;
            worldModerationsBox.BorderStyle = BorderStyle.None;
            worldModerationsBox.BackColor = UiTheme.Surface;
            worldModerationsBox.ForeColor = UiTheme.TextPrimary;
            worldModerationsBox.Text = "┌──────────────────────────────────────────────────────────────────────────────┐\n│ World Staff Moderation Preview                                              │\n├──────────────────────────────────────────────────────────────────────────────┤\n│ Panel detection preview will appear here.                                    │\n│ Staff-owned / admin panel signals will be rendered in this panel area.      │\n│ Use Detect Panel to scan the active log output.                             │\n└──────────────────────────────────────────────────────────────────────────────┘";

            worldModerationScriptBox.ReadOnly = true;
            worldModerationScriptBox.BorderStyle = BorderStyle.None;
            worldModerationScriptBox.BackColor = UiTheme.Surface;
            worldModerationScriptBox.ForeColor = UiTheme.TextPrimary;
            worldModerationScriptBox.Text = "using UdonSharp;\nusing UnityEngine;\nusing VRC.SDKBase;\n\n[UdonBehaviourSyncMode(BehaviourSyncMode.None)]\npublic class DestroyModernUiIfStaffOwnsIt : UdonSharpBehaviour\n{\n    [SerializeField] private GameObject panelToDestroy;\n\n    private void Start()\n    {\n        CheckOwner();\n    }\n\n    public void CheckOwner()\n    {\n        if (panelToDestroy == null)\n            return;\n\n        VRCPlayerApi owner = Networking.GetOwner(panelToDestroy);\n        if (owner == null)\n            return;\n\n        if (owner.isInstanceOwner || owner.isModerator)\n        {\n            Destroy(panelToDestroy);\n        }\n    }\n}";

            worldModerationScriptButton.Text = "Generate Udon Guard";
            worldModerationCopyButton.Text = "Copy Script";
            worldModerationSaveButton.Text = "Save Script";
            worldModerationOpenFolderButton.Text = "Open Script Folder";
            worldModerationDetectButton.Text = "Detect Panel";
            worldModerationToggleButton.Text = "Toggle Staff Destroy";
            worldModerationClearButton.Text = "Clear";
            UiTheme.StyleButton(worldModerationScriptButton);
            UiTheme.StyleButton(worldModerationCopyButton);
            UiTheme.StyleButton(worldModerationSaveButton);
            UiTheme.StyleButton(worldModerationOpenFolderButton);
            UiTheme.StyleButton(worldModerationDetectButton);
            UiTheme.StyleButton(worldModerationToggleButton);
            UiTheme.StyleButton(worldModerationClearButton);
            UiTheme.StyleButton(logSizeDownButton);
            UiTheme.StyleButton(logSizeUpButton);
            logSizeDownButton.Text = "Log -";
            logSizeUpButton.Text = "Log +";
            logSizeDownButton.Click += (s, e) => AdjustLogFontSize(-1f);
            logSizeUpButton.Click += (s, e) => AdjustLogFontSize(1f);
            worldModerationScriptButton.Click += WorldModerationScriptButton_Click;
            worldModerationCopyButton.Click += WorldModerationCopyButton_Click;
            worldModerationSaveButton.Click += WorldModerationSaveButton_Click;
            worldModerationOpenFolderButton.Click += WorldModerationOpenFolderButton_Click;
            worldModerationDetectButton.Click += WorldModerationDetectButton_Click;
            worldModerationToggleButton.Click += WorldModerationToggleButton_Click;
            worldModerationClearButton.Click += WorldModerationClearButton_Click;
            worldModerationsTab.Controls.Add(worldModerationsHeaderLabel);
            worldModerationsTab.Controls.Add(worldModerationsBox);
            worldInfoHeaderLabel.Text = "World Information";
            worldInfoHeaderLabel.ForeColor = UiTheme.Accent;
            worldInfoHeaderLabel.BackColor = System.Drawing.Color.Transparent;
            worldInfoHeaderLabel.AutoSize = true;
            try { worldInfoHeaderLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 12f, System.Drawing.FontStyle.Bold); }
            catch { /* keep default font */ }
            worldInfoBox.ReadOnly = true;
            worldInfoBox.BorderStyle = BorderStyle.None;
            worldInfoBox.BackColor = UiTheme.Surface;
            worldInfoBox.ForeColor = UiTheme.TextPrimary;
            worldInfoBox.Text = "Start logging and join an instance to load world information.";
            worldInfoTab.Controls.Add(worldInfoHeaderLabel);
            worldInfoTab.Controls.Add(worldInfoBox);

            // Rows-based lobby list so each player can carry action buttons.
            lobbyPlayersPanel.AutoScroll = true;
            lobbyPlayersPanel.WrapContents = false;
            lobbyPlayersPanel.FlowDirection = FlowDirection.TopDown;
            lobbyPlayersPanel.BackColor = UiTheme.Surface;
            lobbyPlayersPanel.Resize += (s, e) => SyncLobbyRowWidths();
            lobbyPlayersTab.Controls.Add(lobbyPlayersLabel);
            lobbyPlayersTab.Controls.Add(lobbyPlayersPanel);

            // Per-tab framed borders around the list "windows".
            dashboardTab.Paint += (s, e) => DrawBoxBorders(e.Graphics, new Control[]
            {
                dashboardUserInfoBox
            });
            moderationsTab.Paint += (s, e) => DrawBoxBorders(e.Graphics, new Control[]
            {
                banReasonBox, bannedPlayersPanel
            });
            groupRelatedTab.Paint += (s, e) => DrawBoxBorders(e.Graphics, new Control[]
            {
                ownedGroupsBox, staffGroupsBox, inInstanceStaffBox, whitelistedGroupsBox
            });
            worldModerationsTab.Paint += (s, e) => DrawBoxBorders(e.Graphics, new Control[]
            {
                worldModerationsBox, worldModerationScriptBox
            });
            blacklistTab.Paint += (s, e) => DrawBoxBorders(e.Graphics, new Control[]
            {
                blacklistedGroupsBox, blacklistedAvatarsBox, flaggedWordsBox
            });
            logsTab.Paint += (s, e) => DrawBoxBorders(e.Graphics, new Control[] { logCardsPanel });
            udonLogTab.Paint += (s, e) => DrawBoxBorders(e.Graphics, new Control[] { udonLogBox, udonFloodBox });
            worldInfoTab.Paint += (s, e) => DrawBoxBorders(e.Graphics, new Control[] { worldInfoBox });
            lobbyPlayersTab.Paint += (s, e) => DrawBoxBorders(e.Graphics, new Control[] { lobbyPlayersPanel });
            webhooksTab.Paint += (s, e) => DrawBoxBorders(e.Graphics, new Control[] { alertWebhookBox, forumWebhookBox });

            // Hidden tab pages only get their true ClientSize once shown, so re-layout the
            // page whenever the selected tab changes.
            bodyTabs.SelectedIndexChanged += (s, e) =>
            {
                LayoutAllTabs();
                if (bodyTabs.SelectedTab == dashboardTab && IsLoggedIn)
                    _ = PopulateUserInfoAsync();
            };

            this.Controls.Add(bodyTabs);
            bodyTabs.BringToFront();
        }

        private void LayoutAllTabs()
        {
            if (bodyTabs == null)
                return;

            int tabsTop = instanceBanCountLabel.Bottom + 6;
            const int logSizeButtonWidth = 58;
            const int logSizeButtonGap = 8;
            int logSizeUpX = ClientSize.Width - 12 - logSizeButtonWidth;
            logSizeUpButton.SetBounds(logSizeUpX, instanceBanCountLabel.Top - 2, logSizeButtonWidth, 24);
            logSizeDownButton.SetBounds(logSizeUpX - logSizeButtonGap - logSizeButtonWidth, instanceBanCountLabel.Top - 2, logSizeButtonWidth, 24);
            bodyTabs.Location = new System.Drawing.Point(12, tabsTop);
            bodyTabs.Size = new System.Drawing.Size(
                Math.Max(200, ClientSize.Width - 24),
                Math.Max(200, ClientSize.Height - tabsTop - 12));

            LayoutDashboardTab();
            LayoutModerationsTab();
            LayoutWorldModerationsTab();
            LayoutGroupRelatedTab();
            LayoutBlacklistTab();
            LayoutWebhooksTab();
            LayoutLogsTab();
            LayoutUdonLogsTab();
            LayoutWorldInfoTab();
            LayoutLobbyPlayersTab();
        }

        private void AdjustLogFontSize(float delta)
        {
            var controls = new[] { logBox, udonLogBox, udonFloodBox };
            foreach (var box in controls)
            {
                if (box == null || box.Font == null)
                    continue;

                float newSize = Math.Max(8f, Math.Min(20f, box.Font.Size + delta));
                box.Font = new System.Drawing.Font(box.Font.FontFamily, newSize, box.Font.Style);
            }

            LayoutLogsTab();
            LayoutUdonLogsTab();
        }

        private void LayoutWebhooksTab()
        {
            if (webhooksTab == null)
                return;

            const int pad = 18;
            int width = Math.Max(220, webhooksTab.ClientSize.Width - pad * 2);
            const int sectionGap = 52;

            alertWebhookExplanationLabel.SetBounds(pad, pad, width, 42);
            alertWebhookBox.SetBounds(pad, pad + 48, width, 30);
            int forumTop = alertWebhookBox.Bottom + sectionGap;
            forumWebhookExplanationLabel.SetBounds(pad, forumTop, width, 42);
            forumWebhookBox.SetBounds(pad, forumTop + 48, width, 30);
            saveWebhooksButton.SetBounds(
                Math.Max(pad, webhooksTab.ClientSize.Width - pad - 130),
                forumWebhookBox.Bottom + 10,
                130,
                30);
        }

        private void LayoutLogsTab()
        {
            if (logsTab == null)
                return;

            const int pad = 12;
            const int labelHeight = 24;

            logsLabel.SetBounds(pad, pad, Math.Max(logsLabel.Width, 60), labelHeight);
            clearLogsButton.SetBounds(
                Math.Max(pad, logsTab.ClientSize.Width - pad - 110),
                pad,
                110,
                labelHeight);

            int top = pad + labelHeight + 4;
            logCardsPanel.SetBounds(pad, top,
                Math.Max(100, logsTab.ClientSize.Width - pad * 2),
                Math.Max(100, logsTab.ClientSize.Height - top - pad));
            ResizeLogCards();
        }

        private void ClearLogsButton_Click(object? sender, EventArgs e)
        {
            logBox.Clear();
            logCardsPanel.Controls.Clear();
        }

        private void ResizeLogCards()
        {
            if (logCardsPanel == null)
                return;

            int cardWidth = Math.Max(180, logCardsPanel.ClientSize.Width - logCardsPanel.Padding.Horizontal - 8);
            foreach (Control card in logCardsPanel.Controls)
                card.Width = cardWidth;
        }

        private void LayoutUdonLogsTab()
        {
            if (udonLogTab == null)
                return;

            const int pad = 12;
            const int labelHeight = 24;
            const int columnGap = 14;

            int clientW = udonLogTab.ClientSize.Width;
            int clientH = udonLogTab.ClientSize.Height;

            int leftWidth = (clientW - pad * 2 - columnGap) / 2;
            if (leftWidth < 120)
                leftWidth = 120;

            int rightWidth = clientW - pad * 2 - columnGap - leftWidth;
            if (rightWidth < 120)
                rightWidth = 120;

            udonLogsLabel.SetBounds(pad, pad, Math.Max(udonLogsLabel.Width, 130), labelHeight);
            udonFloodLabel.SetBounds(pad + leftWidth + columnGap, pad, Math.Max(udonFloodLabel.Width, 110), labelHeight);

            int top = pad + labelHeight + 4;
            int boxHeight = Math.Max(100, clientH - top - pad);
            udonLogBox.SetBounds(pad, top, leftWidth, boxHeight);
            udonFloodBox.SetBounds(pad + leftWidth + columnGap, top, rightWidth, boxHeight);
        }

        private void LayoutWorldInfoTab()
        {
            if (worldInfoTab == null)
                return;

            const int pad = 12;
            int clientH = worldInfoTab.ClientSize.Height;

            worldInfoHeaderLabel.SetBounds(pad, pad, Math.Max(worldInfoHeaderLabel.Width, 120), 28);
            int top = pad + 36;
            worldInfoBox.SetBounds(
                pad,
                top,
                Math.Max(100, worldInfoTab.ClientSize.Width - pad * 2),
                Math.Max(100, clientH - top - pad));
        }

        private void LayoutLobbyPlayersTab()
        {
            if (lobbyPlayersTab == null)
                return;

            const int pad = 12;
            const int labelHeight = 24;

            lobbyPlayersLabel.SetBounds(pad, pad, Math.Max(lobbyPlayersLabel.Width, 220), labelHeight);
            int top = pad + labelHeight + 4;
            lobbyPlayersPanel.SetBounds(
                pad,
                top,
                Math.Max(100, lobbyPlayersTab.ClientSize.Width - pad * 2),
                Math.Max(100, lobbyPlayersTab.ClientSize.Height - top - pad));
            SyncLobbyRowWidths();
        }

        // Draws a single, consistent outline around each list window on a tab page.
        private void DrawBoxBorders(System.Drawing.Graphics g, Control[] boxes)
        {
            using var pen = new System.Drawing.Pen(UiTheme.BorderLight);
            foreach (var box in boxes)
            {
                if (box == null || !box.Visible)
                    continue;
                g.DrawRectangle(pen, box.Left - 1, box.Top - 1, box.Width + 1, box.Height + 1);
            }
        }

        // Lays out the Blacklist tab: three full-height columns (groups, avatars, flagged
        // words) each with an add row and a remove button.
        private void LayoutBlacklistTab()
        {
            if (blacklistTab == null)
                return;

            const int left = 15;
            const int bottomMargin = 12;
            const int labelHeight = 24;
            const int addRowHeight = 34;
            const int addRowGap = 8;
            const int removeRowGap = 6;
            const int topSplitGap = 10;
            const int addButtonWidth = 110;
            const int addControlsGap = 6;

            int clientW = blacklistTab.ClientSize.Width;
            int clientH = blacklistTab.ClientSize.Height;

            int topLabel = 12;
            int top = topLabel + labelHeight + 2;

            int width = clientW - left * 2;
            if (width < 300)
                width = 300;

            int columnWidth = (width - topSplitGap * 2) / 3;
            if (columnWidth < 140)
                columnWidth = 140;

            int groupsLeft = left;
            int avatarsLeft = left + columnWidth + topSplitGap;
            int flaggedLeft = left + (columnWidth + topSplitGap) * 2;

            int boxHeight = clientH - top - bottomMargin - addRowGap - addRowHeight - removeRowGap - addRowHeight;
            if (boxHeight < 100)
                boxHeight = 100;

            blacklistedGroupsLabel.SetBounds(groupsLeft, topLabel, blacklistedGroupsLabel.Width, labelHeight);
            blacklistedAvatarsLabel.SetBounds(avatarsLeft, topLabel, blacklistedAvatarsLabel.Width, labelHeight);
            flaggedWordsLabel.SetBounds(flaggedLeft, topLabel, Math.Max(flaggedWordsLabel.Width, 120), labelHeight);
            blacklistedGroupsBox.SetBounds(groupsLeft, top, columnWidth, boxHeight);
            blacklistedAvatarsBox.SetBounds(avatarsLeft, top, columnWidth, boxHeight);
            flaggedWordsBox.SetBounds(flaggedLeft, top, columnWidth, boxHeight);

            int addRowTop = top + boxHeight + addRowGap;
            int addIdWidth = columnWidth - addButtonWidth - addControlsGap;
            if (addIdWidth < 60)
                addIdWidth = 60;
            // Never let the add/remove buttons spill outside their column.
            int effAddButtonWidth = Math.Min(addButtonWidth, Math.Max(60, columnWidth - addIdWidth - addControlsGap));

            addGroupIdBox.SetBounds(groupsLeft, addRowTop + 3, addIdWidth, addRowHeight - 6);
            addGroupButton.SetBounds(groupsLeft + addIdWidth + addControlsGap, addRowTop, effAddButtonWidth, addRowHeight);
            addAvatarIdBox.SetBounds(avatarsLeft, addRowTop + 3, addIdWidth, addRowHeight - 6);
            addAvatarButton.SetBounds(avatarsLeft + addIdWidth + addControlsGap, addRowTop, effAddButtonWidth, addRowHeight);
            addFlaggedWordBox.SetBounds(flaggedLeft, addRowTop + 3, addIdWidth, addRowHeight - 6);
            addFlaggedWordButton.SetBounds(flaggedLeft + addIdWidth + addControlsGap, addRowTop, effAddButtonWidth, addRowHeight);

            int removeRowTop = addRowTop + addRowHeight + removeRowGap;
            int clearButtonWidth = Math.Max(60, (effAddButtonWidth - addControlsGap) / 2);
            int removeButtonWidth = effAddButtonWidth - addControlsGap - clearButtonWidth;
            int removeLeft = groupsLeft + addIdWidth + addControlsGap;
            int avatarRemoveLeft = avatarsLeft + addIdWidth + addControlsGap;
            int flaggedRemoveLeft = flaggedLeft + addIdWidth + addControlsGap;
            removeGroupButton.SetBounds(removeLeft, removeRowTop, removeButtonWidth, addRowHeight);
            clearGroupBlacklistButton.SetBounds(removeLeft + removeButtonWidth + addControlsGap, removeRowTop, clearButtonWidth, addRowHeight);
            removeAvatarButton.SetBounds(avatarRemoveLeft, removeRowTop, removeButtonWidth, addRowHeight);
            clearAvatarBlacklistButton.SetBounds(avatarRemoveLeft + removeButtonWidth + addControlsGap, removeRowTop, clearButtonWidth, addRowHeight);
            removeFlaggedWordButton.SetBounds(flaggedRemoveLeft, removeRowTop, removeButtonWidth, addRowHeight);
            clearFlaggedWordsButton.SetBounds(flaggedRemoveLeft + removeButtonWidth + addControlsGap, removeRowTop, clearButtonWidth, addRowHeight);
        }

        // Lays out the Dashboard tab: the logged-in account panel filling the page.
        private void LayoutDashboardTab()
        {
            if (dashboardTab == null)
                return;

            const int pad = 15;
            const int labelHeight = 24;

            int clientW = dashboardTab.ClientSize.Width;
            int clientH = dashboardTab.ClientSize.Height;

            int headerTop = 12;
            int top = headerTop + labelHeight + 4;

            dashboardUserHeaderLabel.SetBounds(pad, headerTop, Math.Max(dashboardUserHeaderLabel.Width, 120), labelHeight);
            dashboardUserInfoBox.SetBounds(pad, top,
                Math.Max(150, clientW - pad * 2),
                Math.Max(120, clientH - top - pad));
        }

        // Lays out the Moderations tab as two centered, equal-width windows.
        private void LayoutModerationsTab()
        {
            if (moderationsTab == null)
                return;

            const int pad = 15;
            const int labelHeight = 24;
            const int columnGap = 14;

            int clientW = moderationsTab.ClientSize.Width;
            int clientH = moderationsTab.ClientSize.Height;

            int topLabel = 12;
            int top = topLabel + labelHeight + 4;
            int boxHeight = Math.Max(100, clientH - top - pad);

            int contentWidth = Math.Max(200, clientW - pad * 2);
            int contentLeft = Math.Max(pad, (clientW - contentWidth) / 2);
            int columnWidth = Math.Max(100, (contentWidth - columnGap) / 2);
            int rightLeft = contentLeft + columnWidth + columnGap;

            banReasonLabel.SetBounds(contentLeft, topLabel, Math.Max(banReasonLabel.Width, 120), labelHeight);
            banReasonBox.SetBounds(contentLeft, top, columnWidth, boxHeight);

            bannedPlayersLabel.SetBounds(rightLeft, topLabel, Math.Max(bannedPlayersLabel.Width, 120), labelHeight);
            bannedPlayersPanel.SetBounds(rightLeft, top, columnWidth, boxHeight);
            SyncBannedRowWidths();
        }

        // Lays out the Group Related tab: the owned-group column and the staff-groups column.
        private void LayoutWorldModerationsTab()
        {
            if (worldModerationsTab == null)
                return;

            const int pad = 15;
            const int labelHeight = 24;
            const int buttonHeight = 32;
            const int buttonGap = 10;
            const int topGap = 10;

            int clientW = worldModerationsTab.ClientSize.Width;
            int clientH = worldModerationsTab.ClientSize.Height;

            worldModerationsHeaderLabel.SetBounds(pad, pad, Math.Max(worldModerationsHeaderLabel.Width, 170), labelHeight);

            int top = pad + labelHeight + topGap;
            int leftPaneWidth = clientW - pad * 2;
            int infoHeight = Math.Max(120, (clientH - top - buttonHeight - buttonGap * 4 - pad) / 2);
            int scriptHeight = Math.Max(120, clientH - top - infoHeight - buttonHeight - buttonGap * 4 - pad);

            worldModerationsBox.SetBounds(pad, top, leftPaneWidth, infoHeight);
            worldModerationScriptBox.SetBounds(pad, top + infoHeight + buttonGap, leftPaneWidth, scriptHeight);

            int buttonY = top + infoHeight + scriptHeight + buttonGap * 2;
            int buttonWidth = (leftPaneWidth - buttonGap * 6) / 7;
            if (buttonWidth < 85)
                buttonWidth = 85;

            worldModerationScriptButton.SetBounds(pad, buttonY, buttonWidth, buttonHeight);
            worldModerationCopyButton.SetBounds(pad + buttonWidth + buttonGap, buttonY, buttonWidth, buttonHeight);
            worldModerationSaveButton.SetBounds(pad + (buttonWidth + buttonGap) * 2, buttonY, buttonWidth, buttonHeight);
            worldModerationOpenFolderButton.SetBounds(pad + (buttonWidth + buttonGap) * 3, buttonY, buttonWidth, buttonHeight);
            worldModerationDetectButton.SetBounds(pad + (buttonWidth + buttonGap) * 4, buttonY, buttonWidth, buttonHeight);
            worldModerationToggleButton.SetBounds(pad + (buttonWidth + buttonGap) * 5, buttonY, buttonWidth, buttonHeight);
            worldModerationClearButton.SetBounds(pad + (buttonWidth + buttonGap) * 6, buttonY, buttonWidth, buttonHeight);
        }

        private void LayoutGroupRelatedTab()
        {
            if (groupRelatedTab == null)
                return;

            // Keep Group Related as a four-panel 2x2 grid:
            // Owned / Current Staff
            // Staff Groups / Group Whitelist

            const int left = 15;
            const int bottomMargin = 12;
            const int labelHeight = 24;
            const int addRowHeight = 34;
            const int addRowGap = 8;
            const int removeRowGap = 6;
            const int colGap = 14;
            const int managedButtonWidth = 110;
            const int managedControlsGap = 6;
            const int staffPanelGap = 10;

            int clientW = groupRelatedTab.ClientSize.Width;
            int clientH = groupRelatedTab.ClientSize.Height;

            int topLabel = 12;
            int width = clientW - left * 2;
            if (width < 200)
                width = 200;

            int columnWidth = (width - colGap) / 2;
            if (columnWidth < 150)
                columnWidth = 150;

            int ownedLeft = left;
            int staffLeft = left + columnWidth + colGap;

            int topRowTop = topLabel;
            int sectionHeight = (clientH - topLabel - bottomMargin - staffPanelGap) / 2;
            int boxHeight = sectionHeight - labelHeight - addRowGap - addRowHeight - removeRowGap - addRowHeight;
            if (boxHeight < 80)
                boxHeight = 80;
            int bottomRowTop = topRowTop + sectionHeight + staffPanelGap;

            // Left-top: Owned Groups.
            LayoutManagedGroupColumn(
                ownedGroupsLabel, ownedGroupsBox, addOwnedGroupIdBox, addOwnedGroupButton, removeOwnedGroupButton,
                ownedLeft, columnWidth, topRowTop, labelHeight, boxHeight,
                addRowHeight, addRowGap, removeRowGap, managedButtonWidth, managedControlsGap);

            // Left-bottom: Staff Groups.
            LayoutManagedGroupColumn(
                staffGroupsLabel, staffGroupsBox, addStaffGroupIdBox, addStaffGroupButton, removeStaffGroupButton,
                ownedLeft, columnWidth, bottomRowTop, labelHeight, boxHeight,
                addRowHeight, addRowGap, removeRowGap, managedButtonWidth, managedControlsGap);

            // Right-bottom: Group Whitelist.
            LayoutManagedGroupColumn(
                whitelistedGroupsLabel, whitelistedGroupsBox, addWhitelistedGroupIdBox, addWhitelistedGroupButton, removeWhitelistedGroupButton,
                staffLeft, columnWidth, bottomRowTop, labelHeight, boxHeight,
                addRowHeight, addRowGap, removeRowGap, managedButtonWidth, managedControlsGap);

            // Right-top: Current staff in instance.
            inInstanceStaffLabel.SetBounds(staffLeft, topRowTop, columnWidth, labelHeight);
            inInstanceStaffBox.SetBounds(staffLeft, topRowTop + labelHeight, columnWidth, boxHeight);
        }

        // Positions one managed-group column (label, list box, add textbox+button, remove
        // button). Used for both the Owned Group and Staff Groups columns so they share an
        // identical layout.
        private static void LayoutManagedGroupColumn(
            Control label, Control box, Control addBox, Control addButton, Control removeButton,
            int columnLeft, int columnWidth, int topY, int labelHeight, int boxHeight,
            int addRowHeight, int addRowGap, int removeRowGap, int buttonWidth, int controlsGap)
        {
            label.SetBounds(columnLeft, topY, label.Width, labelHeight);
            box.SetBounds(columnLeft, topY + labelHeight, columnWidth, boxHeight);

            int idWidth = columnWidth - buttonWidth - controlsGap;
            if (idWidth < 60)
                idWidth = 60;

            // Never let the button spill outside its column into the neighbouring one.
            int effButtonWidth = Math.Min(buttonWidth, Math.Max(60, columnWidth - idWidth - controlsGap));

            int addRowTop = topY + labelHeight + boxHeight + addRowGap;
            addBox.SetBounds(columnLeft, addRowTop + 3, idWidth, addRowHeight - 6);
            addButton.SetBounds(columnLeft + idWidth + controlsGap, addRowTop, effButtonWidth, addRowHeight);

            int removeRowTop = addRowTop + addRowHeight + removeRowGap;
            removeButton.SetBounds(columnLeft + idWidth + controlsGap, removeRowTop, effButtonWidth, addRowHeight);
        }

        private void LoadSavedLogin()
        {
            if (LoginForm.TryLoadSavedCredentials(out var savedEmail, out var savedPassword, out var savedCookies, out var savedUserId, out var savedDisplayName))
            {
                email = savedEmail;
                password = savedPassword;
                userId = savedUserId;
                displayName = savedDisplayName;
                authCookies = savedCookies;
                UpdateLoggedInLabel();
                SetAuthSessionStatus(authCookies != null ? "Session: Saved (pending validation)" : "Session: Credentials only", authCookies != null);
                BindWebhookForCurrentUser();
                EnsureUsernameBlacklistFile();
            }
            else
            {
                SetAuthSessionStatus("Session: None", false);
            }

            SetOwnedGroupLabelFromConfig();
            UpdateBlacklistControlsEnabled();
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool isLoggedIn = !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password);
            loginButton.Enabled = !isLoggedIn;
            logoutButton.Enabled = isLoggedIn;
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            if (updatePending)
            {
                AppendLog("[UPDATE] Cannot start logging — a newer version is available. Click 'Check for Updates' to install it.");
                MessageBox.Show(
                    "This app needs to be updated before you can start logging.\n\nClick \"Check for Updates\" to install the latest version.",
                    "Update required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!IsLoggedIn)
            {
                AppendLog("[AUTH] You must log in before you can start logging.");
                MessageBox.Show(
                    "Please log in before starting logging.\n\nClick \"Login\" and sign in with your VRChat account first.",
                    "Login required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (logger == null)
            {
                if (!await EnsureAuthenticatedSessionAsync())
                    return;

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    AppendLog("[INFO] You are not logged in. Instance logging will still work, but user details may be limited.");
                }

                api = new VRChatAPI(email ?? "", password ?? "", authCookies);
                api.OnApiInfo += AppendLog;

                await EnsureUserIdAsync();
                var accountBlacklist = string.IsNullOrWhiteSpace(userId)
                    ? new List<string>()
                    : AccountBlacklist.Load(userId);
                var accountAvatarBlacklist = string.IsNullOrWhiteSpace(userId)
                    ? new List<string>()
                    : AccountAvatarBlacklist.Load(userId);
                logger = new LoggerEngine(
                    api,
                    accountBlacklist,
                    accountAvatarBlacklist,
                    ownedGroupItems.Select(item => item.GroupId),
                    LoadWhitelistedGroupIdsFromFile());
                UpdateWebhookControlsEnabled();

                logger.OnLog += AppendLog;
                logger.OnLog += RouteLogToWebhook;
                logger.OnPlayerJoined += (playerName, playerId) => _ = SendPlayerPresenceWebhookAsync(playerName, playerId, true);
                logger.OnPlayerLeft += (playerName, playerId) => _ = SendPlayerPresenceWebhookAsync(playerName, playerId, false);
                logger.OnAvatarObserved += observation => _ = SendAvatarObservationToBotAsync(observation);
                logger.OnAvatarObserved += observation => _ = QueueAvatarObservationWebhookAsync(observation);
                logger.OnStatus += status =>
                {
                    Invoke(new Action(() =>
                    {
                        instanceStatusLabel.Text = status;
                        instanceStatusLabel.ForeColor = System.Drawing.Color.DarkOrange;
                    }));
                };
                logger.OnPlayerBanned += (playerName, playerId, reason) =>
                {
                    RememberAutoBanWebhook(playerId);
                    AppendBanReason($"{playerName} ({playerId})\nPlayer was banned for {reason}");
                    AppendBannedPlayer(playerName, playerId, logger?.BanTargetGroupId ?? "");
                    IncrementInstanceBanCount();
                    var banningStaffName = string.IsNullOrWhiteSpace(displayName) ? "Unknown staff member" : displayName;
                    _ = discordWebhook.PostModerationForumAsync(playerName, playerId, reason, "Banned from group", banningStaffName);
                    _ = discordWebhook.SendEmbedAsync(
                        "\uD83D\uDD28 Player banned",
                        $"**{playerName}** was banned from your group.",
                        DiscordWebhook.ColorBan,
                        new[]
                        {
                            ("Player", playerName, true),
                            ("User ID", playerId, true),
                            ("Banned by", banningStaffName, true),
                            ("Reason", reason, false),
                            ("World", currentWorldName, false)
                        });
                };
                logger.OnBlacklistedGroupMatched += (playerName, groupId, groupName) =>
                {
                    _ = HighlightBlacklistedGroupAsync(groupId);
                    _ = discordWebhook.SendEmbedAsync(
                        "\u26D4 Blacklisted group match",
                        $"**{playerName}** is in a blacklisted group.",
                        DiscordWebhook.ColorGroupMatch,
                        new[]
                        {
                            ("Player", playerName, true),
                            ("Group", groupName, true),
                            ("Group ID", groupId, false)
                        });
                };
                logger.OnBlacklistedAvatarMatched += (playerName, playerId, avatar) =>
                {
                    _ = HighlightBlacklistedAvatarAsync(avatar);
                    _ = discordWebhook.SendEmbedAsync(
                        "\u26D4 Blacklisted avatar match",
                        $"**{playerName}** is wearing a blacklisted avatar.",
                        DiscordWebhook.ColorAvatarMatch,
                        new[]
                        {
                            ("Player", playerName, true),
                            ("Avatar", avatar, true),
                            ("User ID", playerId, false)
                        });
                };
                logger.OnRestrictedAreaDetected += details =>
                {
                    _ = discordWebhook.SendEmbedAsync(
                        "\uD83D\uDEA7 Restricted area detected",
                        details,
                        DiscordWebhook.ColorRestricted,
                        new[]
                        {
                            ("World", currentWorldName, false)
                        });
                };
                logger.OnStaffModerationDetected += evt =>
                {
                    if (string.Equals(evt.EventType, "group.user.ban", StringComparison.OrdinalIgnoreCase) &&
                        WasRecentlyAutoBannedByThisApp(evt.TargetId))
                    {
                        AppendLog($"[WEBHOOK] Suppressed duplicate audit-log Group Ban webhook for {evt.TargetId}.");
                        return;
                    }

                    var fields = new List<(string, string, bool)>
                    {
                        ("Staff", evt.Actor, true),
                        ("Action", evt.ActionLabel, true)
                    };
                    if (!string.IsNullOrWhiteSpace(evt.TargetId) &&
                        evt.TargetId.StartsWith("usr_", StringComparison.OrdinalIgnoreCase))
                        fields.Add(("Target profile", $"https://vrchat.com/home/user/{evt.TargetId}", false));
                    _ = discordWebhook.SendEmbedAsync(
                        $"\uD83D\uDEE1\uFE0F Staff action \u2014 {evt.ActionLabel}",
                        string.IsNullOrWhiteSpace(evt.Description) ? null : evt.Description,
                        DiscordWebhook.ColorStaffAction,
                        fields);
                };
                logger.OnRipSuspicionDetected += (susName, susUserId, marker) =>
                {
                    var who = string.IsNullOrWhiteSpace(susName)
                        ? (string.IsNullOrWhiteSpace(susUserId) ? "An unidentified user" : susUserId)
                        : susName;
                    _ = discordWebhook.SendEmbedAsync(
                        "\u26A0\uFE0F Possible ripper / asset theft",
                        $"{who} triggered a rip-suspicion marker. Manual review recommended.",
                        DiscordWebhook.ColorRip,
                        new[]
                        {
                            ("Player", string.IsNullOrWhiteSpace(susName) ? "Unknown" : susName, true),
                            ("User ID", string.IsNullOrWhiteSpace(susUserId) ? "Unknown" : susUserId, true),
                            ("Marker", marker, false)
                        });
                };
                logger.OnUdonExploitDetected += (udonName, udonUserId, description) =>
                {
                    var who = string.IsNullOrWhiteSpace(udonName)
                        ? (string.IsNullOrWhiteSpace(udonUserId) ? "An unidentified source" : udonUserId)
                        : (!string.IsNullOrWhiteSpace(udonUserId) ? $"{udonName} ({udonUserId})" : udonName);
                    _ = discordWebhook.SendEmbedAsync(
                        "\uD83D\uDCA5 Massive Udon detection",
                        $"{who} triggered a massive Udon detection.",
                        DiscordWebhook.ColorRip,
                        new[]
                        {
                            ("Player", string.IsNullOrWhiteSpace(udonName) ? "Unknown" : udonName, true),
                            ("User ID", string.IsNullOrWhiteSpace(udonUserId) ? "Unknown" : udonUserId, true),
                            ("Detection", description, false)
                        });
                };
                logger.OnModernUiPanelDetected += (panelName, panelUserId, details) =>
                {
                    var who = string.IsNullOrWhiteSpace(panelName)
                        ? (string.IsNullOrWhiteSpace(panelUserId) ? "An unidentified source" : panelUserId)
                        : (!string.IsNullOrWhiteSpace(panelUserId) ? $"{panelName} ({panelUserId})" : panelName);
                    UpdateWorldModerationSummary($"Status: Modern UI panel detected.\nSource: {who}\nDetails: {details}");
                    _ = discordWebhook.SendEmbedAsync(
                        "\uD83D\uDD12 Modern UI panel ownership signal",
                        $"{who} triggered a Modern UI panel ownership signal in the instance.",
                        DiscordWebhook.ColorBan,
                        new[]
                        {
                            ("Player", string.IsNullOrWhiteSpace(panelName) ? "Unknown" : panelName, true),
                            ("User ID", string.IsNullOrWhiteSpace(panelUserId) ? "Unknown" : panelUserId, true),
                            ("Details", details, false)
                        });
                };
                logger.OnForceTeleportDetected += (tpName, tpUserId) =>
                {
                    var who = string.IsNullOrWhiteSpace(tpName) ? "An unidentified player" : tpName;
                    _ = discordWebhook.SendEmbedAsync(
                        "\uD83C\uDF00 Force-teleport to ban zone",
                        $"{who} force-teleported a member to a world ban/out-of-bounds area.",
                        DiscordWebhook.ColorBan,
                        new[]
                        {
                            ("Player", string.IsNullOrWhiteSpace(tpName) ? "Unknown" : tpName, true),
                            ("User ID", string.IsNullOrWhiteSpace(tpUserId) ? "Unknown (not banned)" : tpUserId, true),
                            ("World", currentWorldName, false)
                        });
                };
                logger.OnProfileFlagged += (playerName, matchedWord, worldName, groupName) =>
                {
                    _ = discordWebhook.SendEmbedAsync(
                        "\u26A0\uFE0F Potential troublemaker",
                        $"Potential troublemaker spotted in {worldName}, inside {groupName}.",
                        DiscordWebhook.ColorRestricted,
                        new[]
                        {
                            ("Player", string.IsNullOrWhiteSpace(playerName) ? "Unknown" : playerName, true),
                            ("Flagged word", matchedWord, true)
                        });
                };
                logger.OnLobbyPlayersDetailed += players => UpdateLobbyPlayers(players);
                logger.OnForcedStop += HandleForcedStop;
                logger.OnWorldChanged += worldId =>
                {
                    Invoke(new Action(() =>
                    {
                        currentWorldName = worldId;
                        worldNameLabel.Text = $"World: {worldId}";
                        instanceStatusLabel.Text = "Instance: Active";
                        instanceStatusLabel.ForeColor = System.Drawing.Color.Green;
                        SetInstanceBanCount(0);
                        ClearBannedPlayers();
                        banReasonBox.Clear();
                    }));
                    _ = PopulateWorldInfoAsync(logger?.CurrentWorldId);
                };

                _ = LoadBlacklistedGroupsDisplayAsync();
                _ = LoadBlacklistedAvatarsDisplayAsync();
                LoadWhitelistedGroupsDisplay();
                StartAvatarBlacklistWatcher();
                UpdateBlacklistControlsEnabled();
                if (!await DiscordBotProcess.EnsureRunningAsync(AppendLog))
                {
                    AppendLog("[BOT] Discord bot did not become ready. Avatar review alerts will be unavailable.");
                }
                await logger.Start();
                SetGroupModerationStatus(logger.IsGroupModerationActive);
                startCooldownTimer.Stop();
                startCooldownRemainingSeconds = 0;
                startButton.Text = "Logging...";
                startButton.Enabled = false;
                stopButton.Enabled = true;
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            if (logger == null)
            {
                stopButton.Enabled = false;
                return;
            }

            if (!string.IsNullOrWhiteSpace(userId))
                _ = BotAvatarBridge.SendLoggingStoppedAsync(userId);

            logger.Stop();
            StopAvatarBlacklistWatcher();
            if (api != null)
                api.OnApiInfo -= AppendLog;
            logger = null;
            api = null;
            UpdateWebhookControlsEnabled();

            startCooldownTimer.Stop();
            startCooldownRemainingSeconds = 0;
            startButton.Text = "Start Logging";
            startButton.Enabled = true;
            stopButton.Enabled = false;
            SetGroupModerationStatus(false);
            instanceStatusLabel.Text = "Instance: Stopped";
            instanceStatusLabel.ForeColor = UiTheme.TextMuted;
            AppendLog("[INFO] Logging stopped by user.");
        }

        private async Task SendAvatarObservationToBotAsync(LoggerEngine.AvatarObservation observation)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;

            var result = await BotAvatarBridge.SendAvatarObservedAsync(userId, observation);
            switch (result)
            {
                case AvatarAlertDeliveryResult.Posted:
                    AppendLog($"[BOT] Posted {observation.DisplayName}'s avatar for staff review.");
                    break;
                case AvatarAlertDeliveryResult.Suppressed:
                    AppendLog($"[BOT] Skipped {observation.DisplayName}'s duplicate avatar event.");
                    break;
                default:
                    AppendLog($"[BOT] Could not post {observation.DisplayName}'s avatar alert. Ensure the Discord bot is running.");
                    break;
            }
        }

        private async Task QueueAvatarObservationWebhookAsync(LoggerEngine.AvatarObservation observation)
        {
            var avatarKey = !string.IsNullOrWhiteSpace(observation.AvatarId)
                ? observation.AvatarId.Trim()
                : observation.AvatarName.Trim();
            if (string.IsNullOrWhiteSpace(avatarKey))
                return;

            PendingAvatarWebhook pending;
            CancellationToken token;
            lock (avatarWebhookLock)
            {
                if (!pendingAvatarWebhooks.TryGetValue(avatarKey, out pending!))
                {
                    pending = new PendingAvatarWebhook
                    {
                        AvatarName = observation.AvatarName,
                        AvatarId = observation.AvatarId,
                        ImageUrl = observation.ThumbnailUrl
                    };
                    pendingAvatarWebhooks[avatarKey] = pending;
                }

                pending.AvatarName = string.IsNullOrWhiteSpace(observation.AvatarName)
                    ? pending.AvatarName
                    : observation.AvatarName;
                pending.AvatarId = string.IsNullOrWhiteSpace(observation.AvatarId)
                    ? pending.AvatarId
                    : observation.AvatarId;
                pending.ImageUrl = string.IsNullOrWhiteSpace(observation.ThumbnailUrl)
                    ? pending.ImageUrl
                    : observation.ThumbnailUrl;
                var player = string.IsNullOrWhiteSpace(observation.DisplayName)
                    ? "Unknown"
                    : observation.DisplayName;
                if (!string.IsNullOrWhiteSpace(observation.UserId))
                    player += $" ({observation.UserId})";
                pending.Players.Add(player);

                pending.Cancellation.Cancel();
                pending.Cancellation.Dispose();
                pending.Cancellation = new CancellationTokenSource();
                token = pending.Cancellation.Token;
            }

            try
            {
                await Task.Delay(avatarWebhookQuietPeriod, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            string[] players;
            string avatarName;
            string avatarId;
            string? imageUrl;
            lock (avatarWebhookLock)
            {
                if (!pendingAvatarWebhooks.Remove(avatarKey) || token.IsCancellationRequested)
                    return;

                players = pending.Players.ToArray();
                avatarName = string.IsNullOrWhiteSpace(pending.AvatarName) ? "Unknown" : pending.AvatarName;
                avatarId = string.IsNullOrWhiteSpace(pending.AvatarId) ? "Unknown" : pending.AvatarId;
                imageUrl = pending.ImageUrl;
            }

            await discordWebhook.SendEmbedAsync(
                "\uD83C\uDFA8 Avatar change detected",
                $"The following {players.Length} player(s) changed into this avatar.",
                DiscordWebhook.ColorInfo,
                new[]
                {
                    ("Players", string.Join("\n", players), false),
                    ("Avatar", avatarName, true),
                    ("Avatar ID", avatarId, true)
                },
                imageUrl);
        }

        private void StartAvatarBlacklistWatcher()
        {
            StopAvatarBlacklistWatcher();
            if (logger == null || string.IsNullOrWhiteSpace(userId))
                return;

            var blacklistPath = AccountAvatarBlacklist.GetStorePath(userId);
            var folder = Path.GetDirectoryName(blacklistPath);
            if (string.IsNullOrWhiteSpace(folder))
                return;

            Directory.CreateDirectory(folder);
            avatarBlacklistWatcher = new FileSystemWatcher(folder, Path.GetFileName(blacklistPath))
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            avatarBlacklistWatcher.Changed += AvatarBlacklistFileChanged;
            avatarBlacklistWatcher.Created += AvatarBlacklistFileChanged;
            avatarBlacklistWatcher.Renamed += AvatarBlacklistFileChanged;
        }

        private void StopAvatarBlacklistWatcher()
        {
            if (avatarBlacklistWatcher == null)
                return;

            avatarBlacklistWatcher.EnableRaisingEvents = false;
            avatarBlacklistWatcher.Changed -= AvatarBlacklistFileChanged;
            avatarBlacklistWatcher.Created -= AvatarBlacklistFileChanged;
            avatarBlacklistWatcher.Renamed -= AvatarBlacklistFileChanged;
            avatarBlacklistWatcher.Dispose();
            avatarBlacklistWatcher = null;
        }

        private void AvatarBlacklistFileChanged(object sender, FileSystemEventArgs eventArgs)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new Action(SynchronizeAvatarBlacklistFromDisk));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void SynchronizeAvatarBlacklistFromDisk()
        {
            if (logger == null || string.IsNullOrWhiteSpace(userId))
                return;

            var savedEntries = AccountAvatarBlacklist.Load(userId);
            foreach (var currentEntry in logger.ConfiguredBlacklistedAvatars.ToList())
            {
                if (!savedEntries.Contains(currentEntry, StringComparer.OrdinalIgnoreCase))
                    logger.RemoveBlacklistedAvatar(currentEntry);
            }
            foreach (var savedEntry in savedEntries)
                logger.AddBlacklistedAvatar(savedEntry);

            _ = LoadBlacklistedAvatarsDisplayAsync();
            AppendLog("[BOT] Synced avatar blacklist change from Discord staff action.");
        }

        private void HandleForcedStop(string reason, int cooldownSeconds)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, int>(HandleForcedStop), reason, cooldownSeconds);
                return;
            }

            if (api != null)
                api.OnApiInfo -= AppendLog;

            StopAvatarBlacklistWatcher();
            logger = null;
            api = null;

            stopButton.Enabled = false;
            startCooldownRemainingSeconds = Math.Max(0, cooldownSeconds);
            if (startCooldownRemainingSeconds > 0)
            {
                startButton.Enabled = false;
                startButton.Text = $"Cooldown ({startCooldownRemainingSeconds}s)";
                startCooldownTimer.Start();
            }
            else
            {
                startCooldownTimer.Stop();
                startButton.Text = "Start Logging";
                startButton.Enabled = true;
            }

            instanceStatusLabel.Text = "Instance: Stopped (cooldown)";
            instanceStatusLabel.ForeColor = System.Drawing.Color.DarkOrange;
            AppendLog($"[INFO] {reason}");
        }

        private void StartCooldownTimer_Tick(object? sender, EventArgs e)
        {
            if (startCooldownRemainingSeconds <= 1)
            {
                startCooldownTimer.Stop();
                startCooldownRemainingSeconds = 0;
                startButton.Text = "Start Logging";
                startButton.Enabled = true;
                return;
            }

            startCooldownRemainingSeconds--;
            startButton.Text = $"Cooldown ({startCooldownRemainingSeconds}s)";
            startButton.Enabled = false;
        }

        private async Task<bool> EnsureAuthenticatedSessionAsync()
        {
            // Credentials can be loaded from disk, but instance endpoints often require
            // a live authenticated cookie session (and 2FA verification when enabled).
            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                if (authCookies != null)
                {
                    if (await ValidateSessionAsync(authCookies))
                    {
                        AppendLog("[AUTH] Restored saved authenticated session.");
                        SetAuthSessionStatus("Session: Restored", true);
                        // Persist the freshest cookies (VRChat may have rotated the auth
                        // cookie during validation) so the next launch stays logged in.
                        LoginForm.UpdateSavedCookies(authCookies);
                        return true;
                    }

                    authCookies = null;
                    AppendLog("[AUTH] Saved session expired. Please log in again.");
                    SetAuthSessionStatus("Session: Expired", false);
                }

                using (var loginForm = new LoginForm())
                {
                    var result = loginForm.ShowDialog();
                    if (result != DialogResult.OK)
                    {
                        AppendLog("[AUTH] Session authentication was cancelled.");
                        SetAuthSessionStatus("Session: Login cancelled", false);
                        return false;
                    }

                    email = loginForm.Email;
                    password = loginForm.Password;
                    authCookies = loginForm.AuthCookieContainer;
                    userId = loginForm.UserId;
                    displayName = loginForm.DisplayName;
                    UpdateLoggedInLabel();
                    AppendLog(loginForm.IsTwoFactorAccount
                        ? "[AUTH] Session authenticated with 2FA."
                        : "[AUTH] Session authenticated.");
                    SetAuthSessionStatus(loginForm.IsTwoFactorAccount ? "Session: Authenticated (2FA)" : "Session: Authenticated", true);
                    BindWebhookForCurrentUser();
                    EnsureUsernameBlacklistFile();
                    await RefreshOwnedGroupNameLabelAsync();
                    // The constructor's preloads may have run against an expired saved
                    // session (showing raw ids). Now that we hold a fresh authenticated
                    // session, re-resolve the owned/staff group names so the columns show
                    // display names instead of grp_ ids.
                    _ = PreloadBlacklistedGroupsAsync();
                    _ = PreloadBlacklistedAvatarsAsync();
                    _ = PreloadOwnedGroupsAsync();
                    _ = PreloadStaffGroupsAsync();
                }
            }

            return true;
        }

        private static async Task<bool> ValidateSessionAsync(CookieContainer cookies)
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    CookieContainer = cookies
                };

                using (handler)
                using (var client = new HttpClient(handler))
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("VRChat Group Auto Moderation/1.0");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await client.GetAsync("https://api.vrchat.cloud/api/1/auth/user");
                    if (!response.IsSuccessStatusCode)
                        return false;

                    var body = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(body))
                        return false;

                    var json = JObject.Parse(body);

                    // A valid HTTP 200 can still mean session is not fully authenticated yet
                    // (for example, account requires 2FA verification for this session).
                    if (json["requiresTwoFactorAuth"] != null)
                        return false;

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            using (var loginForm = new LoginForm())
            {
                if (loginForm.ShowDialog() == DialogResult.OK)
                {
                    email = loginForm.Email;
                    password = loginForm.Password;
                    authCookies = loginForm.AuthCookieContainer;
                    userId = loginForm.UserId;
                    displayName = loginForm.DisplayName;
                    UpdateLoggedInLabel();
                    AppendLog(loginForm.IsTwoFactorAccount ? "[AUTH] Email/password set with 2FA session." : "[AUTH] Email/password set.");
                    SetAuthSessionStatus(loginForm.IsTwoFactorAccount ? "Session: Authenticated (2FA)" : "Session: Authenticated", true);
                    BindWebhookForCurrentUser();
                    EnsureUsernameBlacklistFile();
                    _ = RefreshOwnedGroupNameLabelAsync();
                    _ = PreloadBlacklistedGroupsAsync();
                    _ = PreloadBlacklistedAvatarsAsync();
                    _ = PreloadOwnedGroupsAsync();
                    _ = PreloadStaffGroupsAsync();
                    UpdateButtonStates();
                }
            }
        }

        // Makes sure the username blacklist file (blacklist.txt) is present next to the app
        // as soon as someone logs in, so a freshly downloaded copy shows the editable file
        // without having to start logging first. Blacklist.Load() creates it if missing.
        private void EnsureUsernameBlacklistFile()
        {
            try { Blacklist.Load(); }
            catch { /* non-fatal: the file will still be created when logging starts */ }
        }

        // Binds the Discord webhook to the account that just logged in and tells the user
        // whether alerts are active or how to turn them on for their staff-log channel.
        private void BindWebhookForCurrentUser()
        {
            var accountKey = !string.IsNullOrWhiteSpace(userId) ? userId : email;
            var result = discordWebhook.SetActiveUser(accountKey);
            alertWebhookBox.Text = discordWebhook.ActiveUrl;
            forumWebhookBox.Text = discordWebhook.ActiveForumUrl;
            UpdateWebhookControlsEnabled();
            switch (result)
            {
                case WebhookBindResult.Existing:
                    AppendLog("[WEBHOOK] Discord alerts enabled for your account.");
                    break;
                case WebhookBindResult.Adopted:
                    AppendLog("[WEBHOOK] Discord webhook saved to your account. Alerts are now enabled.");
                    break;
                default:
                    AppendLog("[WEBHOOK] No Discord webhook set for your account. Add one in the Webhooks tab and press Save webhooks.");
                    break;
            }

            _ = PopulateUserInfoAsync();
        }

        private void UpdateWebhookControlsEnabled()
        {
            bool canEdit = IsLoggedIn && logger == null;
            alertWebhookBox.Enabled = canEdit;
            forumWebhookBox.Enabled = canEdit;
            saveWebhooksButton.Enabled = canEdit;
        }

        private void SaveWebhooksButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn || logger != null)
                return;

            if (!discordWebhook.UpdateActiveUrls(alertWebhookBox.Text, forumWebhookBox.Text, out var error))
            {
                MessageBox.Show(error, "Webhook links not saved", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AppendLog("[WEBHOOK] Webhook links saved for this account.");
            MessageBox.Show("Webhook links saved.", "Webhooks", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string BuildWorldModerationUdonScript()
        {
            return "using UdonSharp;\nusing UnityEngine;\nusing VRC.SDKBase;\n\n[UdonBehaviourSyncMode(BehaviourSyncMode.None)]\npublic class DestroyModernUiIfStaffOwnsIt : UdonSharpBehaviour\n{\n    [Header(\"World Moderation\")]\n    [SerializeField] private string banRoomLabel = \"Teleport Area\";\n    [SerializeField] private GameObject panelToDestroy;\n    [SerializeField] private Transform teleportArea;\n    [SerializeField] private bool destroyPanelWhenStaffIsOwner = true;\n\n    private void Start()\n    {\n        CheckLobbyStaff();\n        CheckOwner();\n    }\n\n    private void OnEnable()\n    {\n        CheckLobbyStaff();\n    }\n\n    public void CheckLobbyStaff()\n    {\n        VRCPlayerApi local = Networking.LocalPlayer;\n        if (local == null || teleportArea == null)\n            return;\n\n        if (!local.isInstanceOwner && !local.isModerator)\n            return;\n\n        // If staff are already in the lobby, send them to the configured ban room / teleport area.\n        if (local.isInstanceOwner || local.isModerator)\n        {\n            local.TeleportTo(teleportArea.position, teleportArea.rotation);\n        }\n    }\n\n    public void CheckOwner()\n    {\n        if (panelToDestroy == null)\n            return;\n\n        VRCPlayerApi owner = Networking.GetOwner(panelToDestroy);\n        if (owner == null)\n            return;\n\n        if (destroyPanelWhenStaffIsOwner && (owner.isInstanceOwner || owner.isModerator))\n        {\n            Destroy(panelToDestroy);\n        }\n    }\n}";
        }

        private void DrawWorldModerationPreview(string title, string subtitle, string details)
        {
            var width = 76;
            var titleText = title.Length > width - 4 ? title.Substring(0, width - 7) + "..." : title;
            var subtitleText = subtitle.Length > width - 4 ? subtitle.Substring(0, width - 7) + "..." : subtitle;
            var detailLines = details.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            var lines = new List<string>
            {
                "┌" + new string('─', width) + "┐",
                "│ " + titleText.PadRight(width - 3) + "│",
                "│ " + subtitleText.PadRight(width - 3) + "│",
                "├" + new string('─', width) + "┤",
                "│ Player List      Rank     Status      Ban     │",
                "│ [Admin]         Owner    Staff       Active  │",
                "│ [Target]        User     Suspicious  Watch  │",
                "│ [Ban] [TP] [View] [Kick] [Freeze]           │",
                "├" + new string('─', width) + "┤"
            };

            foreach (var line in detailLines)
            {
                var safeLine = line.Replace("\r", string.Empty);
                if (string.IsNullOrWhiteSpace(safeLine))
                {
                    lines.Add("│ " + new string(' ', width - 2) + "│");
                    continue;
                }

                var trimmed = safeLine.Length > width - 4 ? safeLine.Substring(0, width - 7) + "..." : safeLine;
                lines.Add("│ " + trimmed.PadRight(width - 3) + "│");
            }

            lines.Add("└" + new string('─', width) + "┘");
            worldModerationsBox.Text = string.Join(Environment.NewLine, lines);
        }

        private void UpdateWorldModerationSummary(string extraStatus)
        {
            var enabled = worldModerationToggleButton.Text.Contains("On", StringComparison.OrdinalIgnoreCase);
            var currentSummary = enabled
                ? "Staff-owner destroy protection is enabled.\n\nChecks:\n- Networking.GetOwner(panelToDestroy)\n- owner.isInstanceOwner || owner.isModerator\n- Destroy(panelToDestroy) when a staff owner is detected\n- On spawn, teleport staff to the configured teleportArea"
                : "Staff-owner destroy protection is currently disabled for this session.\n\nChecks are defined but the toggle is off until re-enabled.";

            if (string.IsNullOrWhiteSpace(extraStatus))
            {
                worldModerationsBox.Text = currentSummary;
                return;
            }

            if (extraStatus.Contains("Modern UI", StringComparison.OrdinalIgnoreCase)
                || extraStatus.Contains("SimpleUI", StringComparison.OrdinalIgnoreCase)
                || extraStatus.Contains("Hash Studios", StringComparison.OrdinalIgnoreCase)
                || extraStatus.Contains("Reimajo", StringComparison.OrdinalIgnoreCase)
                || extraStatus.Contains("panel detection matched", StringComparison.OrdinalIgnoreCase)
                || extraStatus.Contains("panel detected", StringComparison.OrdinalIgnoreCase))
            {
                var title = extraStatus.Contains("Hash Studios", StringComparison.OrdinalIgnoreCase)
                    ? "Hash Studios Admin Ban Menu"
                    : extraStatus.Contains("SimpleUI", StringComparison.OrdinalIgnoreCase)
                        ? "SimpleUI Panel Detected"
                        : extraStatus.Contains("Reimajo", StringComparison.OrdinalIgnoreCase)
                            ? "Reimajo Admin Panel Detected"
                            : "Modern UI Panel Detected";
                DrawWorldModerationPreview(title, "Admin panel signal active", extraStatus);
                return;
            }

            worldModerationsBox.Text = $"{currentSummary}\n\n{extraStatus}";
        }

        private void WorldModerationScriptButton_Click(object? sender, EventArgs e)
        {
            string script = BuildWorldModerationUdonScript();
            worldModerationScriptBox.Text = script;
            UpdateWorldModerationSummary("Status: script generated for the panel guard and spawn redirect.\nFile: " + RuntimePaths.GetFile("world_staff_destroy_guard.cs") + "\nBehavior: staff spawn -> teleportArea, staff-owned panel -> destroy");
            AppendLog("[WORLD-MOD] Generated the staff-owner destroy Udon guard script with a spawn redirect to the teleport area.");
        }

        private void WorldModerationCopyButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(worldModerationScriptBox.Text))
            {
                UpdateWorldModerationSummary("Status: no script generated yet. Use Generate Udon Guard first.");
                return;
            }

            try
            {
                Clipboard.SetText(worldModerationScriptBox.Text);
                UpdateWorldModerationSummary("Status: script copied to the clipboard.\nReady to paste into a UdonSharp behaviour.");
                AppendLog("[WORLD-MOD] Copied the staff-owner destroy Udon script to the clipboard.");
            }
            catch (Exception ex)
            {
                UpdateWorldModerationSummary("Status: clipboard failed.\nReason: " + ex.Message);
                AppendLog("[WORLD-MOD] Could not copy the Udon script to the clipboard: " + ex.Message);
            }
        }

        private void WorldModerationSaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                string script = string.IsNullOrWhiteSpace(worldModerationScriptBox.Text)
                    ? BuildWorldModerationUdonScript()
                    : worldModerationScriptBox.Text;

                string scriptPath = RuntimePaths.GetFile("world_staff_destroy_guard.cs");
                File.WriteAllText(scriptPath, script);
                worldModerationScriptBox.Text = script;
                UpdateWorldModerationSummary("Status: script saved to disk.\nPath: " + scriptPath);
                AppendLog("[WORLD-MOD] Saved the Udon staff-owner guard script at " + scriptPath + ".");
            }
            catch (Exception ex)
            {
                UpdateWorldModerationSummary("Status: save failed.\nReason: " + ex.Message);
                AppendLog("[WORLD-MOD] Could not save the Udon script file: " + ex.Message);
            }
        }

        private void WorldModerationOpenFolderButton_Click(object? sender, EventArgs e)
        {
            try
            {
                string scriptDirectory = RuntimePaths.Root;
                if (!Directory.Exists(scriptDirectory))
                    Directory.CreateDirectory(scriptDirectory);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = scriptDirectory,
                    UseShellExecute = true,
                    Verb = "open"
                });

                UpdateWorldModerationSummary("Status: script folder opened.\nDirectory: " + scriptDirectory);
                AppendLog("[WORLD-MOD] Opened the app-data folder containing the saved Udon script.");
            }
            catch (Exception ex)
            {
                UpdateWorldModerationSummary("Status: could not open script folder.\nReason: " + ex.Message);
                AppendLog("[WORLD-MOD] Could not open the script folder: " + ex.Message);
            }
        }

        private void WorldModerationDetectButton_Click(object? sender, EventArgs e)
        {
            var markers = new[]
            {
                "simpleui",
                "simple ui",
                "modernui",
                "modern ui",
                "reimajo",
                "reimajo admin panel",
                "reimajo panel",
                "hash studios admin ban menu",
                "admin ban menu",
                "hash studios",
                "panel owner",
                "panel ownership",
                "staff-owned panel",
                "owner of the panel",
                "modern panel",
                "simple panel"
            };

            string? current = null;
            if (logger != null)
            {
                var statusText = worldModerationsBox.Text;
                foreach (var marker in markers)
                {
                    if (statusText.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    {
                        current = marker;
                        break;
                    }
                }
            }

            string logText = string.Empty;
            if (logBox != null)
                logText = logBox.Text;

            var findings = new List<string>();
            foreach (var panelMarker in markers)
            {
                if (logText.Contains(panelMarker, StringComparison.OrdinalIgnoreCase))
                    findings.Add(panelMarker);
            }

            if (findings.Count == 0)
            {
                UpdateWorldModerationSummary("Status: no SimpleUI or ModernUI panel markers were found in the recent log output.\nThis scan looks for simpleui, modernui, and panel ownership strings.");
                AppendLog("[WORLD-MOD] Panel detection scan found no SimpleUI or ModernUI markers in the recent log output.");
                return;
            }

            var matchText = string.Join(", ", findings.Distinct(StringComparer.OrdinalIgnoreCase));
            UpdateWorldModerationSummary($"Status: panel detection matched: {matchText}.\nReview the log output for staff-owned or owner-flagged panel activity.\nDetected panel type: {matchText}");
            AppendLog($"[WORLD-MOD] Panel detection scan matched: {matchText}.");
        }

        private void WorldModerationToggleButton_Click(object? sender, EventArgs e)
        {
            var enabled = worldModerationToggleButton.Text.Contains("On", StringComparison.OrdinalIgnoreCase);
            worldModerationToggleButton.Text = enabled ? "Toggle Staff Destroy: Off" : "Toggle Staff Destroy: On";
            worldModerationToggleButton.BackColor = enabled ? UiTheme.LogDanger : UiTheme.LogJoin;
            UpdateWorldModerationSummary(enabled
                ? "Status: staff-owner destroy protection disabled for this session."
                : "Status: staff-owner destroy protection enabled.\nIf a world owner or moderator owns the Modern UI panel, it will be destroyed.\nStaff spawns are redirected to the configured teleportArea.");
            AppendLog($"[WORLD-MOD] Staff-owner destroy protection {(enabled ? "disabled" : "enabled")}. Staff spawn redirect is {(enabled ? "off" : "on")}.");
        }

        private void WorldModerationClearButton_Click(object? sender, EventArgs e)
        {
            worldModerationsBox.Clear();
            worldModerationScriptBox.Clear();
            worldModerationToggleButton.Text = "Toggle Staff Destroy";
            worldModerationToggleButton.BackColor = UiTheme.LogDanger;
            AppendLog("[WORLD-MOD] World moderation panel cleared.");
        }

        private void LogoutButton_Click(object sender, EventArgs e)
        {
            logger?.Stop();
            email = null;
            password = null;
            userId = null;
            displayName = null;
            authCookies = null;
            logger = null;
            api = null;

            discordWebhook.ClearActiveUser();
            alertWebhookBox.Clear();
            forumWebhookBox.Clear();
            UpdateWebhookControlsEnabled();
            LoginForm.ClearSavedCredentials();
            loggedInLabel.Text = "Logged in: (none)";
            SetUserInfoText("Log in to see your account information.");
            SetAuthSessionStatus("Session: Logged out", false);
            instanceStatusLabel.Text = "Instance: Waiting...";
            instanceStatusLabel.ForeColor = System.Drawing.Color.Red;
            worldNameLabel.Text = "World: (none)";
            currentWorldName = "(none)";
            SetGroupModerationStatus(false);
            SetInstanceBanCount(0);
            banReasonBox.Text = "No ban yet.";
            ClearBannedPlayers();
            lobbyPlayersPanel.Controls.Clear();
            lobbyPlayersLabel.Text = "Current lobby players (0)";
            logBox.Clear();
            logCardsPanel.Controls.Clear();
            udonLogBox.Clear();
            udonFloodBox.Clear();
            blacklistedGroupItems.Clear();
            blacklistedGroupsBox.Clear();
            blacklistedAvatarItems.Clear();
            blacklistedAvatarsBox.Clear();
            ownedGroupItems.Clear();
            RenderOwnedGroups();
            whitelistedGroupItems.Clear();
            RenderWhitelistedGroups();
            inInstanceStaffBox.Clear();
            inInstanceStaffBox.SelectionColor = UiTheme.TextMuted;
            inInstanceStaffBox.AppendText("No approved owned-group staff detected in this instance.\n");
            staffGroupItems.Clear();
            RenderStaffGroups();
            UpdateBlacklistControlsEnabled();
            RenderBlacklistedGroups(null);
            RenderBlacklistedAvatars(null);
            AppendLog("[AUTH] Logged out and cleared saved login.");
            SetOwnedGroupLabelFromConfig();
            startCooldownTimer.Stop();
            startCooldownRemainingSeconds = 0;
            startButton.Text = "Start Logging";
            startButton.Enabled = true;
            stopButton.Enabled = false;
            UpdateButtonStates();
        }

        private async void AddGroupButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show(
                    "Please log in first to add blacklisted groups.",
                    "Login required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var input = addGroupIdBox.Text?.Trim() ?? "";

            var match = Regex.Match(input, "((?:grp|gmem)_[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                MessageBox.Show(
                    "Please enter a valid group ID (it should start with \"grp_\").",
                    "Invalid group ID",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var groupId = match.Groups[1].Value;

            if (blacklistedGroupItems.Any(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    "That group is already on the blacklist.",
                    "Already added",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                addGroupIdBox.Clear();
                return;
            }

            if (blacklistedGroupItems.Count >= AccountBlacklist.MaxEntries)
            {
                MessageBox.Show(
                    $"You can have up to {AccountBlacklist.MaxEntries} blacklisted groups per account.",
                    "Blacklist limit reached",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                addGroupIdBox.Clear();
                return;
            }

            try
            {
                // Save the group id to this account's private blacklist so it never
                // appears for any other account.
                await EnsureUserIdAsync();

                if (string.IsNullOrWhiteSpace(userId))
                {
                    MessageBox.Show(
                        "Could not verify your account yet. Please try again in a moment.",
                        "Not ready",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (!AccountBlacklist.Add(userId, groupId))
                {
                    MessageBox.Show(
                        $"You can have up to {AccountBlacklist.MaxEntries} blacklisted groups per account.",
                        "Blacklist limit reached",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not save the group ID to your blacklist.\n\n" + ex.Message,
                    "Save failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Apply to the live logger if logging is already running.
            logger?.AddBlacklistedGroupId(groupId);

            addGroupIdBox.Clear();

            // Show immediately, then resolve the friendly name if possible.
            blacklistedGroupItems.Add((groupId, groupId));
            RenderBlacklistedGroups(null);

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return;

            try
            {
                var nameApi = api ?? new VRChatAPI(email ?? "", password ?? "", authCookies);
                var name = await nameApi.GetGroupNameAsync(groupId);

                if (IsDisposed || string.IsNullOrWhiteSpace(name))
                    return;

                var index = blacklistedGroupItems.FindIndex(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    blacklistedGroupItems[index] = (groupId, name);
                    RenderBlacklistedGroups(null);
                }
            }
            catch
            {
                // Keep the id on screen if the name can't be resolved.
            }
        }

        private bool IsLoggedIn => !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password);

        // Shows the VRChat display name when known, falling back to the email so there is
        // always something useful on screen.
        private void UpdateLoggedInLabel()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateLoggedInLabel));
                return;
            }

            if (!IsLoggedIn)
            {
                loggedInLabel.Text = "Logged in: (none)";
                return;
            }

            var who = !string.IsNullOrWhiteSpace(displayName) ? displayName : email;
            loggedInLabel.Text = $"Logged in: {who}";
        }

        // Makes sure we know which VRChat account is logged in so the correct per-account
        // blacklist is used. Saved logins from older versions may not have the id stored,
        // so it is fetched from the API once and then persisted.
        private async Task EnsureUserIdAsync()
        {
            // Resolve whatever is still missing — an older saved login may already have
            // the user id but not the display name.
            if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(displayName))
                return;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return;

            try
            {
                var idApi = api ?? new VRChatAPI(email ?? "", password ?? "", authCookies);
                var user = await idApi.GetAuthenticatedUserAsync();
                var resolvedId = user?["id"]?.ToString();
                var resolvedName = user?["displayName"]?.ToString();

                if (!string.IsNullOrWhiteSpace(resolvedId))
                    userId = resolvedId;
                if (!string.IsNullOrWhiteSpace(resolvedName))
                    displayName = resolvedName;

                if (!string.IsNullOrWhiteSpace(resolvedId) || !string.IsNullOrWhiteSpace(resolvedName))
                {
                    LoginForm.UpdateSavedUserId(resolvedId ?? "", resolvedName);
                    UpdateLoggedInLabel();
                }
            }
            catch
            {
                // If the lookup fails we simply keep the blacklist empty for safety.
            }
        }

        private void UpdateBlacklistControlsEnabled()
        {
            bool loggedIn = IsLoggedIn;
            addGroupIdBox.Enabled = loggedIn;
            addGroupButton.Enabled = loggedIn;
            removeGroupButton.Enabled = loggedIn;
            clearGroupBlacklistButton.Enabled = loggedIn;
            addAvatarIdBox.Enabled = loggedIn;
            addAvatarButton.Enabled = loggedIn;
            removeAvatarButton.Enabled = loggedIn;
            clearAvatarBlacklistButton.Enabled = loggedIn;
            addOwnedGroupIdBox.Enabled = loggedIn;
            addOwnedGroupButton.Enabled = loggedIn;
            removeOwnedGroupButton.Enabled = loggedIn;
            addStaffGroupIdBox.Enabled = loggedIn;
            addStaffGroupButton.Enabled = loggedIn;
            removeStaffGroupButton.Enabled = loggedIn;
            addWhitelistedGroupIdBox.Enabled = loggedIn;
            addWhitelistedGroupButton.Enabled = loggedIn;
            removeWhitelistedGroupButton.Enabled = loggedIn;

            addFlaggedWordBox.Enabled = loggedIn;
            addFlaggedWordButton.Enabled = loggedIn;
            removeFlaggedWordButton.Enabled = loggedIn;
            clearFlaggedWordsButton.Enabled = loggedIn;

            if (loggedIn)
                LoadFlaggedWordsDisplay();
            else
            {
                flaggedWordItems.Clear();
                flaggedWordsBox.Clear();
                flaggedWordsBox.SelectionColor = UiTheme.TextMuted;
                flaggedWordsBox.AppendText("Log in to view flagged words.\n");
                addFlaggedWordBox.Clear();
            }
        }

        private async Task PreloadBlacklistedGroupsAsync()
        {
            UpdateBlacklistControlsEnabled();

            // The blacklist can only be viewed while logged in.
            if (!IsLoggedIn)
            {
                blacklistedGroupItems.Clear();
                RenderBlacklistedGroups(null);
                return;
            }

            await EnsureUserIdAsync();

            // Without a confirmed account we show nothing rather than risk displaying
            // another account's list.
            if (string.IsNullOrWhiteSpace(userId))
            {
                blacklistedGroupItems.Clear();
                RenderBlacklistedGroups(null);
                return;
            }

            // Show this account's blacklist as soon as the app opens, before logging
            // starts. Group ids appear immediately; if we have a saved session we then
            // resolve and display the friendly group names.
            var ids = AccountBlacklist.Load(userId);

            blacklistedGroupItems.Clear();
            foreach (var groupId in ids)
                blacklistedGroupItems.Add((groupId, groupId));

            RenderBlacklistedGroups(null);

            if (ids.Count == 0 || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return;

            try
            {
                var displayApi = api ?? new VRChatAPI(email ?? "", password ?? "", authCookies);

                var resolved = new List<(string GroupId, string GroupName)>();
                foreach (var groupId in ids)
                {
                    var name = await displayApi.GetGroupNameAsync(groupId);
                    resolved.Add((groupId, string.IsNullOrWhiteSpace(name) ? groupId : name));
                }

                if (IsDisposed)
                    return;

                blacklistedGroupItems.Clear();
                blacklistedGroupItems.AddRange(resolved);
                RenderBlacklistedGroups(null);
            }
            catch
            {
                // Leave the group ids on screen if name resolution fails.
            }
        }

        private async Task LoadBlacklistedGroupsDisplayAsync()
        {
            if (logger == null || api == null)
                return;

            var items = new List<(string GroupId, string GroupName)>();
            foreach (var groupId in logger.ConfiguredBlacklistedGroupIds)
            {
                var groupName = await api.GetGroupNameAsync(groupId) ?? groupId;
                items.Add((groupId, groupName));
            }

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    blacklistedGroupItems.Clear();
                    blacklistedGroupItems.AddRange(items);
                    RenderBlacklistedGroups(null);
                }));
                return;
            }

            blacklistedGroupItems.Clear();
            blacklistedGroupItems.AddRange(items);
            RenderBlacklistedGroups(null);
        }

        private async Task HighlightBlacklistedGroupAsync(string groupId)
        {
            int generation = InvokeRequired
                ? (int)Invoke(new Func<int>(() => StartBlacklistedGroupHighlight(groupId)))
                : StartBlacklistedGroupHighlight(groupId);

            await Task.Delay(4000);

            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    ClearBlacklistedGroupHighlight(generation);
                }));
                return;
            }

            ClearBlacklistedGroupHighlight(generation);
        }

        private int StartBlacklistedGroupHighlight(string groupId)
        {
            blacklistHighlightGeneration++;
            RenderBlacklistedGroups(groupId);
            return blacklistHighlightGeneration;
        }

        private void ClearBlacklistedGroupHighlight(int generation)
        {
            if (generation == blacklistHighlightGeneration)
                RenderBlacklistedGroups(null);
        }

        private void RenderBlacklistedGroups(string? highlightedGroupId)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string?>(RenderBlacklistedGroups), highlightedGroupId);
                return;
            }

            blacklistedGroupsBox.Clear();
            var defaultColor = UiTheme.TextPrimary;
            var highlightColor = UiTheme.LogDanger;

            if (!IsLoggedIn)
            {
                blacklistedGroupsBox.SelectionColor = UiTheme.TextMuted;
                blacklistedGroupsBox.AppendText("Log in to view blacklisted groups.\n");
                return;
            }

            if (blacklistedGroupItems.Count == 0)
            {
                blacklistedGroupsBox.SelectionColor = UiTheme.TextMuted;
                blacklistedGroupsBox.AppendText("No blacklisted groups loaded.\n");
                return;
            }

            foreach (var item in blacklistedGroupItems)
            {
                blacklistedGroupsBox.SelectionStart = blacklistedGroupsBox.TextLength;
                blacklistedGroupsBox.SelectionLength = 0;
                blacklistedGroupsBox.SelectionColor = string.Equals(item.GroupId, highlightedGroupId, StringComparison.OrdinalIgnoreCase)
                    ? highlightColor
                    : defaultColor;
                blacklistedGroupsBox.AppendText($"{item.GroupName}\n");
            }
            blacklistedGroupsBox.SelectionColor = defaultColor;
        }

        private async void AddAvatarButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show(
                    "Please log in first to add blacklisted avatars.",
                    "Login required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var input = addAvatarIdBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show(
                    "Please enter an avatar name or avatar ID (starting with \"avtr_\").",
                    "Nothing to add",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (blacklistedAvatarItems.Any(item => string.Equals(item, input, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    "That avatar is already on the blacklist.",
                    "Already added",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                addAvatarIdBox.Clear();
                return;
            }

            try
            {
                // Save to this account's private avatar blacklist so it never appears
                // for any other account.
                await EnsureUserIdAsync();

                if (string.IsNullOrWhiteSpace(userId))
                {
                    MessageBox.Show(
                        "Could not verify your account yet. Please try again in a moment.",
                        "Not ready",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                AccountAvatarBlacklist.Add(userId, input);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not save the avatar to your blacklist.\n\n" + ex.Message,
                    "Save failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Apply to the live logger if logging is already running.
            logger?.AddBlacklistedAvatar(input);

            addAvatarIdBox.Clear();

            blacklistedAvatarItems.Add(input);
            RenderBlacklistedAvatars(null);
        }

        private async Task PreloadBlacklistedAvatarsAsync()
        {
            UpdateBlacklistControlsEnabled();

            // The avatar blacklist can only be viewed while logged in.
            if (!IsLoggedIn)
            {
                blacklistedAvatarItems.Clear();
                RenderBlacklistedAvatars(null);
                return;
            }

            await EnsureUserIdAsync();

            // Without a confirmed account we show nothing rather than risk displaying
            // another account's list.
            if (string.IsNullOrWhiteSpace(userId))
            {
                blacklistedAvatarItems.Clear();
                RenderBlacklistedAvatars(null);
                return;
            }

            var entries = AccountAvatarBlacklist.Load(userId);

            blacklistedAvatarItems.Clear();
            blacklistedAvatarItems.AddRange(entries);
            RenderBlacklistedAvatars(null);
        }

        private Task LoadBlacklistedAvatarsDisplayAsync()
        {
            if (logger == null)
                return Task.CompletedTask;

            var items = logger.ConfiguredBlacklistedAvatars.ToList();

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    blacklistedAvatarItems.Clear();
                    blacklistedAvatarItems.AddRange(items);
                    RenderBlacklistedAvatars(null);
                }));
                return Task.CompletedTask;
            }

            blacklistedAvatarItems.Clear();
            blacklistedAvatarItems.AddRange(items);
            RenderBlacklistedAvatars(null);
            return Task.CompletedTask;
        }

        private async Task HighlightBlacklistedAvatarAsync(string avatar)
        {
            int generation = InvokeRequired
                ? (int)Invoke(new Func<int>(() => StartBlacklistedAvatarHighlight(avatar)))
                : StartBlacklistedAvatarHighlight(avatar);

            await Task.Delay(4000);

            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    ClearBlacklistedAvatarHighlight(generation);
                }));
                return;
            }

            ClearBlacklistedAvatarHighlight(generation);
        }

        private int StartBlacklistedAvatarHighlight(string avatar)
        {
            avatarHighlightGeneration++;
            RenderBlacklistedAvatars(avatar);
            return avatarHighlightGeneration;
        }

        private void ClearBlacklistedAvatarHighlight(int generation)
        {
            if (generation == avatarHighlightGeneration)
                RenderBlacklistedAvatars(null);
        }

        private void RenderBlacklistedAvatars(string? highlightedAvatar)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string?>(RenderBlacklistedAvatars), highlightedAvatar);
                return;
            }

            blacklistedAvatarsBox.Clear();
            var defaultColor = UiTheme.TextPrimary;
            var highlightColor = UiTheme.LogDanger;

            if (!IsLoggedIn)
            {
                blacklistedAvatarsBox.SelectionColor = UiTheme.TextMuted;
                blacklistedAvatarsBox.AppendText("Log in to view blacklisted avatars.\n");
                return;
            }

            if (blacklistedAvatarItems.Count == 0)
            {
                blacklistedAvatarsBox.SelectionColor = UiTheme.TextMuted;
                blacklistedAvatarsBox.AppendText("No blacklisted avatars loaded.\n");
                return;
            }

            foreach (var item in blacklistedAvatarItems)
            {
                blacklistedAvatarsBox.SelectionStart = blacklistedAvatarsBox.TextLength;
                blacklistedAvatarsBox.SelectionLength = 0;
                blacklistedAvatarsBox.SelectionColor = string.Equals(item, highlightedAvatar, StringComparison.OrdinalIgnoreCase)
                    ? highlightColor
                    : defaultColor;
                blacklistedAvatarsBox.AppendText($"{item}\n");
            }
            blacklistedAvatarsBox.SelectionColor = defaultColor;
        }

        private async void RemoveGroupButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show(
                    "Please log in first to remove blacklisted groups.",
                    "Login required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (blacklistedGroupItems.Count == 0)
            {
                MessageBox.Show(
                    "There are no blacklisted groups to remove.",
                    "Nothing to remove",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            int line = blacklistedGroupsBox.GetLineFromCharIndex(blacklistedGroupsBox.SelectionStart);
            if (line < 0 || line >= blacklistedGroupItems.Count)
            {
                MessageBox.Show(
                    "Click the group you want to remove in the list above, then press Remove.",
                    "Select a group",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var item = blacklistedGroupItems[line];

            var confirm = MessageBox.Show(
                $"Remove \"{item.GroupName}\" from the blacklist?",
                "Remove group",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                await EnsureUserIdAsync();
                if (!string.IsNullOrWhiteSpace(userId))
                    AccountBlacklist.Remove(userId, item.GroupId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not update your blacklist file.\n\n" + ex.Message,
                    "Remove failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            logger?.RemoveBlacklistedGroupId(item.GroupId);

            if (line < blacklistedGroupItems.Count &&
                string.Equals(blacklistedGroupItems[line].GroupId, item.GroupId, StringComparison.OrdinalIgnoreCase))
            {
                blacklistedGroupItems.RemoveAt(line);
            }
            else
            {
                blacklistedGroupItems.RemoveAll(g => string.Equals(g.GroupId, item.GroupId, StringComparison.OrdinalIgnoreCase));
            }

            RenderBlacklistedGroups(null);
        }

        private async void RemoveAvatarButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show(
                    "Please log in first to remove blacklisted avatars.",
                    "Login required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (blacklistedAvatarItems.Count == 0)
            {
                MessageBox.Show(
                    "There are no blacklisted avatars to remove.",
                    "Nothing to remove",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            int line = blacklistedAvatarsBox.GetLineFromCharIndex(blacklistedAvatarsBox.SelectionStart);
            if (line < 0 || line >= blacklistedAvatarItems.Count)
            {
                MessageBox.Show(
                    "Click the avatar you want to remove in the list above, then press Remove.",
                    "Select an avatar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var item = blacklistedAvatarItems[line];

            var confirm = MessageBox.Show(
                $"Remove \"{item}\" from the blacklist?",
                "Remove avatar",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                await EnsureUserIdAsync();
                if (!string.IsNullOrWhiteSpace(userId))
                    AccountAvatarBlacklist.Remove(userId, item);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not update your avatar blacklist file.\n\n" + ex.Message,
                    "Remove failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            logger?.RemoveBlacklistedAvatar(item);

            blacklistedAvatarItems.RemoveAll(a => string.Equals(a, item, StringComparison.OrdinalIgnoreCase));
            RenderBlacklistedAvatars(null);
        }

        private async void ClearGroupBlacklistButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn || blacklistedGroupItems.Count == 0)
                return;

            var confirm = MessageBox.Show("Clear every group from the blacklist?", "Clear group blacklist",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            await EnsureUserIdAsync();
            if (string.IsNullOrWhiteSpace(userId))
                return;

            try
            {
                AccountBlacklist.Clear(userId);
                logger?.ClearBlacklistedGroups();
                blacklistedGroupItems.Clear();
                RenderBlacklistedGroups(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not clear your group blacklist.\n\n" + ex.Message,
                    "Clear failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ClearAvatarBlacklistButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn || blacklistedAvatarItems.Count == 0)
                return;

            var confirm = MessageBox.Show("Clear every avatar from the blacklist?", "Clear avatar blacklist",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            await EnsureUserIdAsync();
            if (string.IsNullOrWhiteSpace(userId))
                return;

            try
            {
                AccountAvatarBlacklist.Clear(userId);
                logger?.ClearBlacklistedAvatars();
                blacklistedAvatarItems.Clear();
                RenderBlacklistedAvatars(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not clear your avatar blacklist.\n\n" + ex.Message,
                    "Clear failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddFlaggedWordButton_Click(object? sender, EventArgs e)
        {
            var input = addFlaggedWordBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Enter a word or phrase to flag.", "Nothing to add",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!FlaggedWords.Add(input))
            {
                MessageBox.Show("That word is already on the flagged list.", "Already added",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                addFlaggedWordBox.Clear();
                return;
            }

            addFlaggedWordBox.Clear();
            LoadFlaggedWordsDisplay();
        }

        private void RemoveFlaggedWordButton_Click(object? sender, EventArgs e)
        {
            if (flaggedWordItems.Count == 0)
            {
                MessageBox.Show("There are no flagged words to remove.", "Nothing to remove",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int line = flaggedWordsBox.GetLineFromCharIndex(flaggedWordsBox.SelectionStart);
            if (line < 0 || line >= flaggedWordItems.Count)
            {
                MessageBox.Show("Click the word you want to remove in the list above, then press Remove.",
                    "Select a word", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var item = flaggedWordItems[line];
            var confirm = MessageBox.Show($"Remove \"{item}\" from the flagged words?", "Remove word",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            FlaggedWords.Remove(item);
            LoadFlaggedWordsDisplay();
        }

        private void ClearFlaggedWordsButton_Click(object? sender, EventArgs e)
        {
            if (flaggedWordItems.Count == 0)
                return;

            var confirm = MessageBox.Show("Clear every word from the flagged list?", "Clear flagged words",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            FlaggedWords.Clear();
            LoadFlaggedWordsDisplay();
        }

        private void LoadFlaggedWordsDisplay()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(LoadFlaggedWordsDisplay));
                return;
            }

            flaggedWordItems.Clear();
            foreach (var raw in FlaggedWords.Entries)
            {
                var word = raw?.Trim();
                if (string.IsNullOrWhiteSpace(word) || word.StartsWith("#"))
                    continue;
                flaggedWordItems.Add(word);
            }

            flaggedWordsBox.Text = string.Join(Environment.NewLine, flaggedWordItems);
        }

        private async Task PreloadOwnedGroupsAsync()
        {
            UpdateBlacklistControlsEnabled();

            if (!IsLoggedIn)
            {
                ownedGroupItems.Clear();
                RenderOwnedGroups();
                return;
            }

            var ids = LoadOwnedGroupIds();

            ownedGroupItems.Clear();
            foreach (var id in ids)
                ownedGroupItems.Add((id, id));
            RenderOwnedGroups();

            if (ids.Count == 0 || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return;

            try
            {
                var displayApi = api ?? new VRChatAPI(email ?? "", password ?? "", authCookies);

                var resolved = new List<(string GroupId, string GroupName)>();
                foreach (var id in ids)
                {
                    var name = await displayApi.GetGroupNameAsync(id);
                    resolved.Add((id, string.IsNullOrWhiteSpace(name) ? id : name));
                }

                if (IsDisposed)
                    return;

                ownedGroupItems.Clear();
                ownedGroupItems.AddRange(resolved);
                RenderOwnedGroups();
            }
            catch
            {
                // Leave the group ids on screen if name resolution fails.
            }
        }

        private void RenderOwnedGroups()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(RenderOwnedGroups));
                return;
            }

            ownedGroupsBox.Clear();

            if (!IsLoggedIn)
            {
                ownedGroupsBox.SelectionColor = UiTheme.TextMuted;
                ownedGroupsBox.AppendText("Log in to view owned groups.\n");
                return;
            }

            if (ownedGroupItems.Count == 0)
            {
                ownedGroupsBox.SelectionColor = UiTheme.TextMuted;
                ownedGroupsBox.AppendText("No owned groups set.\n");
                return;
            }

            foreach (var item in ownedGroupItems)
            {
                ownedGroupsBox.SelectionStart = ownedGroupsBox.TextLength;
                ownedGroupsBox.SelectionLength = 0;
                ownedGroupsBox.SelectionColor = UiTheme.TextPrimary;
                ownedGroupsBox.AppendText($"{item.GroupName}\n");
            }
        }

        private void LoadWhitelistedGroupsDisplay()
        {
            whitelistedGroupItems.Clear();
            foreach (var groupId in LoadWhitelistedGroupIdsFromFile())
                whitelistedGroupItems.Add((groupId, groupId));
            RenderWhitelistedGroups();
        }

        private void RenderWhitelistedGroups()
        {
            whitelistedGroupsBox.Clear();
            if (!IsLoggedIn)
            {
                whitelistedGroupsBox.SelectionColor = UiTheme.TextMuted;
                whitelistedGroupsBox.AppendText("Log in to view group whitelist.\n");
                return;
            }

            if (whitelistedGroupItems.Count == 0)
            {
                whitelistedGroupsBox.SelectionColor = UiTheme.TextMuted;
                whitelistedGroupsBox.AppendText("No whitelisted groups loaded.\n");
                return;
            }

            foreach (var item in whitelistedGroupItems)
            {
                whitelistedGroupsBox.SelectionColor = UiTheme.TextPrimary;
                whitelistedGroupsBox.AppendText($"{item.GroupName}\n");
            }
        }

        private async void AddWhitelistedGroupButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show("Please log in first to add a whitelisted group.", "Login required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var groupIds = Regex.Matches(addWhitelistedGroupIdBox.Text?.Trim() ?? "", "grp_[A-Za-z0-9_-]+", RegexOptions.IgnoreCase)
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (groupIds.Count == 0)
            {
                MessageBox.Show("Please enter a valid group ID (it should start with \"grp_\").", "Invalid group ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            addWhitelistedGroupButton.Enabled = false;
            var originalText = addWhitelistedGroupButton.Text;
            addWhitelistedGroupButton.Text = "Checking...";

            try
            {
                await EnsureUserIdAsync();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    MessageBox.Show("Could not verify your account yet. Please try again in a moment.", "Not ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var checkApi = api ?? new VRChatAPI(email ?? "", password ?? "", authCookies);
                foreach (var groupId in groupIds)
                {
                    if (whitelistedGroupItems.Any(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    bool owns = await checkApi.IsGroupOwnedByUserAsync(groupId, userId);
                    bool isApprovedStaff = !owns && await checkApi.IsUserGroupStaffAsync(groupId, userId);
                    if (!owns && !isApprovedStaff)
                    {
                        MessageBox.Show($"Group {groupId} was skipped because you do not own it and are not approved staff for it.", "Group access not approved", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }

                    AddWhitelistedGroupIdToFile(groupId);
                    logger?.AddWhitelistedGroupId(groupId);
                    var groupName = await checkApi.GetGroupNameAsync(groupId) ?? groupId;
                    whitelistedGroupItems.Add((groupId, groupName));
                }

                RenderWhitelistedGroups();
                addWhitelistedGroupIdBox.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not add the whitelisted group(s).\n\n" + ex.Message, "Whitelist update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed)
                {
                    addWhitelistedGroupButton.Text = originalText;
                    addWhitelistedGroupButton.Enabled = IsLoggedIn;
                }
            }
        }

        private void RemoveWhitelistedGroupButton_Click(object? sender, EventArgs e)
        {
            int line = whitelistedGroupsBox.GetLineFromCharIndex(whitelistedGroupsBox.SelectionStart);
            if (line < 0 || line >= whitelistedGroupItems.Count)
                return;

            var item = whitelistedGroupItems[line];
            RemoveWhitelistedGroupIdFromFile(item.GroupId);
            logger?.RemoveWhitelistedGroupId(item.GroupId);
            whitelistedGroupItems.RemoveAt(line);
            RenderWhitelistedGroups();
        }

        private static List<string> LoadWhitelistedGroupIdsFromFile()
        {
            var path = RuntimePaths.GetFile("group_whitelist.txt");
            if (!File.Exists(path))
                return new List<string>();

            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("grp_", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddWhitelistedGroupIdToFile(string groupId)
        {
            var path = RuntimePaths.GetFile("group_whitelist.txt");
            var ids = LoadWhitelistedGroupIdsFromFile();
            if (!ids.Contains(groupId, StringComparer.OrdinalIgnoreCase))
                ids.Add(groupId);
            File.WriteAllLines(path, ids);
        }

        private static void RemoveWhitelistedGroupIdFromFile(string groupId)
        {
            var path = RuntimePaths.GetFile("group_whitelist.txt");
            var ids = LoadWhitelistedGroupIdsFromFile();
            ids.RemoveAll(id => string.Equals(id, groupId, StringComparison.OrdinalIgnoreCase));
            File.WriteAllLines(path, ids);
        }

        private async void AddOwnedGroupButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show(
                    "Please log in first to add an owned group.",
                    "Login required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var input = addOwnedGroupIdBox.Text?.Trim() ?? "";

            var match = Regex.Match(input, "(grp_[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                MessageBox.Show(
                    "Please enter a valid group ID (it should start with \"grp_\").",
                    "Invalid group ID",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var groupId = match.Groups[1].Value;

            if (ownedGroupItems.Any(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    "That group is already in your owned groups.",
                    "Already added",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                addOwnedGroupIdBox.Clear();
                return;
            }

            await EnsureUserIdAsync();
            if (string.IsNullOrWhiteSpace(userId))
            {
                MessageBox.Show(
                    "Could not verify your account yet. Please try again in a moment.",
                    "Not ready",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Show it immediately while we confirm ownership, then remove it again if the
            // logged-in user is not the owner of that group.
            addOwnedGroupButton.Enabled = false;
            var originalText = addOwnedGroupButton.Text;
            addOwnedGroupButton.Text = "Checking...";
            ownedGroupItems.Add((groupId, groupId));
            RenderOwnedGroups();

            try
            {
                var checkApi = api ?? new VRChatAPI(email ?? "", password ?? "", authCookies);
                bool owns = await checkApi.IsGroupOwnedByUserAsync(groupId, userId);
                bool isApprovedStaff = !owns && await checkApi.IsUserGroupStaffAsync(groupId, userId);

                if (IsDisposed)
                    return;

                if (!owns && !isApprovedStaff)
                {
                    ownedGroupItems.RemoveAll(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
                    RenderOwnedGroups();
                    MessageBox.Show(
                        "You are not the owner or an approved staff member of that group.",
                        "Group access not approved",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                AddOwnedGroupIdToFile(groupId);
                logger?.AddOwnedGroupId(groupId);
                addOwnedGroupIdBox.Clear();

                if (isApprovedStaff)
                {
                    AppendLog($"[GROUP] Added owned group {groupId} because this account is approved staff for it.");
                    MessageBox.Show(
                        "You are approved staff for this group, so it was added to your owned groups.",
                        "Staff access approved",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                var name = await checkApi.GetGroupNameAsync(groupId);
                if (!IsDisposed && !string.IsNullOrWhiteSpace(name))
                {
                    var index = ownedGroupItems.FindIndex(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0)
                    {
                        ownedGroupItems[index] = (groupId, name);
                        RenderOwnedGroups();
                    }
                }

                SetOwnedGroupLabelFromConfig();
                _ = RefreshOwnedGroupNameLabelAsync();
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    ownedGroupItems.RemoveAll(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
                    RenderOwnedGroups();
                    MessageBox.Show(
                        "Could not verify group ownership.\n\n" + ex.Message,
                        "Check failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    addOwnedGroupButton.Text = originalText;
                    addOwnedGroupButton.Enabled = IsLoggedIn;
                }
            }
        }

        private void RemoveOwnedGroupButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show(
                    "Please log in first to remove an owned group.",
                    "Login required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (ownedGroupItems.Count == 0)
            {
                MessageBox.Show(
                    "There are no owned groups to remove.",
                    "Nothing to remove",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            int line = ownedGroupsBox.GetLineFromCharIndex(ownedGroupsBox.SelectionStart);
            if (line < 0 || line >= ownedGroupItems.Count)
            {
                MessageBox.Show(
                    "Click the group you want to remove in the list above, then press Remove.",
                    "Select a group",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var item = ownedGroupItems[line];

            var confirm = MessageBox.Show(
                $"Remove \"{item.GroupName}\" from your owned groups?",
                "Remove owned group",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                RemoveOwnedGroupIdFromFile(item.GroupId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not update owned_group.txt.\n\n" + ex.Message,
                    "Remove failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            logger?.RemoveOwnedGroupId(item.GroupId);
            ownedGroupItems.RemoveAll(g => string.Equals(g.GroupId, item.GroupId, StringComparison.OrdinalIgnoreCase));
            RenderOwnedGroups();
            SetOwnedGroupLabelFromConfig();
            _ = RefreshOwnedGroupNameLabelAsync();
        }

        private static List<string> LoadOwnedGroupIds()
        {
            var result = new List<string>();
            var path = RuntimePaths.GetFile("owned_group.txt");
            if (!File.Exists(path))
                return result;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var match = Regex.Match(line, "(grp_[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;

                var id = match.Groups[1].Value;
                if (!result.Contains(id, StringComparer.OrdinalIgnoreCase))
                    result.Add(id);
            }

            return result;
        }

        private static bool AddOwnedGroupIdToFile(string groupId)
        {
            if (LoadOwnedGroupIds().Contains(groupId, StringComparer.OrdinalIgnoreCase))
                return false;

            var path = RuntimePaths.GetFile("owned_group.txt");
            var needsNewline = File.Exists(path)
                && new FileInfo(path).Length > 0
                && !File.ReadAllText(path).EndsWith("\n");

            File.AppendAllText(path, (needsNewline ? Environment.NewLine : "") + groupId + Environment.NewLine);
            return true;
        }

        private static bool RemoveOwnedGroupIdFromFile(string groupId)
        {
            var path = RuntimePaths.GetFile("owned_group.txt");
            if (!File.Exists(path))
                return false;

            var lines = File.ReadAllLines(path).ToList();
            var remaining = lines.Where(raw =>
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    return true; // preserve comments and blank lines

                var match = Regex.Match(line, "(grp_[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
                return !(match.Success && string.Equals(match.Groups[1].Value, groupId, StringComparison.OrdinalIgnoreCase));
            }).ToList();

            if (remaining.Count == lines.Count)
                return false;

            File.WriteAllLines(path, remaining);
            return true;
        }

        private async Task PreloadStaffGroupsAsync()
        {
            UpdateBlacklistControlsEnabled();

            if (!IsLoggedIn)
            {
                staffGroupItems.Clear();
                RenderStaffGroups();
                return;
            }

            var ids = LoadStaffGroupIds();

            staffGroupItems.Clear();
            foreach (var id in ids)
                staffGroupItems.Add((id, id));
            RenderStaffGroups();

            if (ids.Count == 0 || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return;

            try
            {
                var displayApi = api ?? new VRChatAPI(email ?? "", password ?? "", authCookies);

                var resolved = new List<(string GroupId, string GroupName)>();
                foreach (var id in ids)
                {
                    var name = await displayApi.GetGroupNameAsync(id);
                    resolved.Add((id, string.IsNullOrWhiteSpace(name) ? id : name));
                }

                if (IsDisposed)
                    return;

                staffGroupItems.Clear();
                staffGroupItems.AddRange(resolved);
                RenderStaffGroups();
            }
            catch
            {
                // Leave the group ids on screen if name resolution fails.
            }
        }

        private void RenderStaffGroups()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(RenderStaffGroups));
                return;
            }

            staffGroupsBox.Clear();

            if (!IsLoggedIn)
            {
                staffGroupsBox.SelectionColor = UiTheme.TextMuted;
                staffGroupsBox.AppendText("Log in to view staff groups.\n");
                return;
            }

            if (staffGroupItems.Count == 0)
            {
                staffGroupsBox.SelectionColor = UiTheme.TextMuted;
                staffGroupsBox.AppendText("No staff groups set.\n");
                return;
            }

            foreach (var item in staffGroupItems)
            {
                staffGroupsBox.SelectionStart = staffGroupsBox.TextLength;
                staffGroupsBox.SelectionLength = 0;
                staffGroupsBox.SelectionColor = UiTheme.TextPrimary;
                staffGroupsBox.AppendText($"{item.GroupName}\n");
            }
        }

        private async void AddStaffGroupButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show(
                    "Please log in first to add a staff group.",
                    "Login required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var input = addStaffGroupIdBox.Text?.Trim() ?? "";

            var match = Regex.Match(input, "(grp_[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                MessageBox.Show(
                    "Please enter a valid group ID (it should start with \"grp_\").",
                    "Invalid group ID",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var groupId = match.Groups[1].Value;

            if (staffGroupItems.Any(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    "That group is already in your staff groups.",
                    "Already added",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                addStaffGroupIdBox.Clear();
                return;
            }

            await EnsureUserIdAsync();
            if (string.IsNullOrWhiteSpace(userId))
            {
                MessageBox.Show(
                    "Could not verify your account yet. Please try again in a moment.",
                    "Not ready",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Show it immediately while we confirm ownership, then remove it again if the
            // logged-in user is not the owner of that group.
            addStaffGroupButton.Enabled = false;
            var originalText = addStaffGroupButton.Text;
            addStaffGroupButton.Text = "Checking...";
            staffGroupItems.Add((groupId, groupId));
            RenderStaffGroups();

            try
            {
                var checkApi = api ?? new VRChatAPI(email ?? "", password ?? "", authCookies);
                bool owns = await checkApi.IsGroupOwnedByUserAsync(groupId, userId);

                if (IsDisposed)
                    return;

                if (!owns)
                {
                    staffGroupItems.RemoveAll(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
                    RenderStaffGroups();
                    MessageBox.Show(
                        "You do not own that group, so it was not added.\n\nOnly groups you are the owner of can be added.",
                        "Not your group",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                AddStaffGroupIdToFile(groupId);
                addStaffGroupIdBox.Clear();

                // Apply to the live logger if logging is already running.
                logger?.AddStaffGroupId(groupId);

                var name = await checkApi.GetGroupNameAsync(groupId);
                if (!IsDisposed && !string.IsNullOrWhiteSpace(name))
                {
                    var index = staffGroupItems.FindIndex(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0)
                    {
                        staffGroupItems[index] = (groupId, name);
                        RenderStaffGroups();
                    }
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    staffGroupItems.RemoveAll(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
                    RenderStaffGroups();
                    MessageBox.Show(
                        "Could not verify group ownership.\n\n" + ex.Message,
                        "Check failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    addStaffGroupButton.Text = originalText;
                    addStaffGroupButton.Enabled = IsLoggedIn;
                }
            }
        }

        private void RemoveStaffGroupButton_Click(object? sender, EventArgs e)
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show(
                    "Please log in first to remove a staff group.",
                    "Login required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (staffGroupItems.Count == 0)
            {
                MessageBox.Show(
                    "There are no staff groups to remove.",
                    "Nothing to remove",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            int line = staffGroupsBox.GetLineFromCharIndex(staffGroupsBox.SelectionStart);
            if (line < 0 || line >= staffGroupItems.Count)
            {
                MessageBox.Show(
                    "Click the group you want to remove in the list above, then press Remove.",
                    "Select a group",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var item = staffGroupItems[line];

            var confirm = MessageBox.Show(
                $"Remove \"{item.GroupName}\" from your staff groups?",
                "Remove staff group",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                RemoveStaffGroupIdFromFile(item.GroupId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not update staff_groups.txt.\n\n" + ex.Message,
                    "Remove failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            logger?.RemoveStaffGroupId(item.GroupId);

            staffGroupItems.RemoveAll(g => string.Equals(g.GroupId, item.GroupId, StringComparison.OrdinalIgnoreCase));
            RenderStaffGroups();
        }

        private static List<string> LoadStaffGroupIds()
        {
            var result = new List<string>();
            var path = RuntimePaths.GetFile("staff_groups.txt");
            if (!File.Exists(path))
                return result;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var match = Regex.Match(line, "(grp_[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;

                var id = match.Groups[1].Value;
                if (!result.Contains(id, StringComparer.OrdinalIgnoreCase))
                    result.Add(id);
            }

            return result;
        }

        private static bool AddStaffGroupIdToFile(string groupId)
        {
            if (LoadStaffGroupIds().Contains(groupId, StringComparer.OrdinalIgnoreCase))
                return false;

            var path = RuntimePaths.GetFile("staff_groups.txt");
            var needsNewline = File.Exists(path)
                && new FileInfo(path).Length > 0
                && !File.ReadAllText(path).EndsWith("\n");

            File.AppendAllText(path, (needsNewline ? Environment.NewLine : "") + groupId + Environment.NewLine);
            return true;
        }

        private static bool RemoveStaffGroupIdFromFile(string groupId)
        {
            var path = RuntimePaths.GetFile("staff_groups.txt");
            if (!File.Exists(path))
                return false;

            var lines = File.ReadAllLines(path).ToList();
            var remaining = lines.Where(raw =>
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    return true; // preserve comments and blank lines

                var match = Regex.Match(line, "(grp_[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
                return !(match.Success && string.Equals(match.Groups[1].Value, groupId, StringComparison.OrdinalIgnoreCase));
            }).ToList();

            if (remaining.Count == lines.Count)
                return false;

            File.WriteAllLines(path, remaining);
            return true;
        }

        private static string LoadOwnedGroupId()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "owned_group.txt");
            if (!File.Exists(path))
                return string.Empty;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("grp_", StringComparison.OrdinalIgnoreCase))
                    return line;
            }

            return string.Empty;
        }

        private void SetOwnedGroupLabelFromConfig()
        {
            if (!IsLoggedIn)
            {
                ownedGroupNameLabel.Text = "Owned group: (log in to view)";
                return;
            }

            var groupId = LoadOwnedGroupId();
            if (string.IsNullOrWhiteSpace(groupId))
            {
                ownedGroupNameLabel.Text = "Owned group: (not set)";
                return;
            }

            ownedGroupNameLabel.Text = $"Owned group: {groupId}";
        }

        private async Task RefreshOwnedGroupNameLabelAsync()
        {
            if (!IsLoggedIn)
            {
                SetOwnedGroupLabelFromConfig();
                return;
            }

            var groupId = LoadOwnedGroupId();
            if (string.IsNullOrWhiteSpace(groupId))
            {
                SetOwnedGroupLabelFromConfig();
                return;
            }

            SetOwnedGroupLabelFromConfig();

            if (api == null)
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    return;

                api = new VRChatAPI(email, password, authCookies);
            }

            try
            {
                var group = await api.GetGroupAsync(groupId);
                if (group["error"] != null)
                    return;

                var groupName = group["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(groupName))
                    ownedGroupNameLabel.Text = $"Owned group: {groupName}";
            }
            catch
            {
                // Keep id-only fallback label when name lookup fails.
            }
        }

        private void SetGroupModerationStatus(bool active)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(SetGroupModerationStatus), active);
                return;
            }

            if (active)
            {
                groupModerationStatusLabel.Text = "Group moderation status: Moderation is active.";
                groupModerationStatusLabel.ForeColor = System.Drawing.Color.SeaGreen;
            }
            else
            {
                groupModerationStatusLabel.Text = "Group moderation status: Moderation is not active";
                groupModerationStatusLabel.ForeColor = UiTheme.TextMuted;
            }
        }

        private void SetInstanceBanCount(int count)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int>(SetInstanceBanCount), count);
                return;
            }

            instanceBanCount = count;
            instanceBanCountLabel.Text = $"Instance Ban Count: {count}";
        }

        // Increments the ban counter on the UI thread so concurrent ban events (the
        // player scan allows a few in parallel) cannot lose a count via a racy read.
        private void IncrementInstanceBanCount()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(IncrementInstanceBanCount));
                return;
            }

            SetInstanceBanCount(instanceBanCount + 1);
        }

        private void SetAuthSessionStatus(string status, bool healthy)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, bool>(SetAuthSessionStatus), status, healthy);
                return;
            }

            authSessionLabel.Text = status;
            authSessionLabel.ForeColor = healthy ? System.Drawing.Color.SeaGreen : UiTheme.TextMuted;
        }

        // Sends compact join/leave alerts to Discord. Driven from the logger's text log
        // because those events already flow through OnLog with stable phrasing. Runs on a
        // background thread; DiscordWebhook serializes sends and honors rate limits.
        private void RouteLogToWebhook(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || !discordWebhook.IsEnabled)
                return;
        }

        private async Task SendPlayerPresenceWebhookAsync(string playerName, string playerId, bool joined)
        {
            var worldName = string.IsNullOrWhiteSpace(currentWorldName) ? "Unknown world" : currentWorldName;
            var worldId = logger?.CurrentWorldId ?? "Unknown";
            var instanceId = logger?.CurrentInstanceId ?? "Unknown";
            if (!string.IsNullOrWhiteSpace(userId) && await BotAvatarBridge.SendPlayerPresenceAsync(
                userId, playerName, playerId, joined, worldName, worldId, instanceId))
            {
                AppendLog($"[BOT] Posted player {(joined ? "join" : "leave")} alert for {playerName}.");
                return;
            }

            if (!discordWebhook.IsEnabled)
                return;

            var profileUrl = string.IsNullOrWhiteSpace(playerId) ? "Unknown" : $"https://vrchat.com/home/user/{playerId}";
            var action = joined ? "joined" : "left";

            _ = discordWebhook.SendEmbedAsync(
                joined ? "\uD83D\uDFE2 Player joined" : "\u26AA Player left",
                $"**{playerName}** {action} the instance.",
                joined ? DiscordWebhook.ColorJoin : DiscordWebhook.ColorLeave,
                new[]
                {
                    ("Player", playerName, true),
                    ("User ID", playerId, true),
                    ("Profile", profileUrl, false),
                    ("World", worldName, true),
                    ("World ID", worldId, false),
                    ("Instance", instanceId, false),
                    ("Event time (UTC)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"), false)
                });
        }

        private void RememberAutoBanWebhook(string? playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return;

            lock (autoBanWebhookLock)
            {
                var now = DateTime.UtcNow;
                recentAutoBanWebhookUsers[playerId] = now;
                foreach (var entry in recentAutoBanWebhookUsers
                    .Where(entry => now - entry.Value > autoBanWebhookAuditWindow)
                    .Select(entry => entry.Key)
                    .ToList())
                {
                    recentAutoBanWebhookUsers.Remove(entry);
                }
            }
        }

        private bool WasRecentlyAutoBannedByThisApp(string? playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return false;

            lock (autoBanWebhookLock)
            {
                return recentAutoBanWebhookUsers.TryGetValue(playerId, out var bannedAt) &&
                    DateTime.UtcNow - bannedAt <= autoBanWebhookAuditWindow;
            }
        }

        private static string GetLogIconPrefix(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return string.Empty;

            var normalized = Regex.Replace(message.TrimStart(), @"^\d{2}:\d{2}:\d{2}\s+", string.Empty);

            if (normalized.StartsWith("[GROUP-CHECK]", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("[GROUP-BL]", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("group check", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("blacklist", StringComparison.OrdinalIgnoreCase))
                return "🛡";

            if (normalized.StartsWith("[AUTH]", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("[WEBHOOK]", StringComparison.OrdinalIgnoreCase))
                return "🔐";

            if (normalized.StartsWith("[AUTO-BAN]", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("banned", StringComparison.OrdinalIgnoreCase))
                return "✖";

            if (normalized.StartsWith("[INSTANCE]", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("[RESTRICTED-SCAN]", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("instance", StringComparison.OrdinalIgnoreCase))
                return "📍";

            if (normalized.StartsWith("[INFO]", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("[DETECTOR]", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("started", StringComparison.OrdinalIgnoreCase))
                return "ℹ";

            if (normalized.StartsWith("[WORLD-MOD]", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("panel", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("moderation", StringComparison.OrdinalIgnoreCase))
                return "🧩";

            if (normalized.StartsWith("JOIN:", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Player joined:", StringComparison.OrdinalIgnoreCase))
                return "✓";

            if (normalized.Contains(" left", StringComparison.OrdinalIgnoreCase))
                return "⎋";

            if (normalized.StartsWith("[UDON-DETECT]", StringComparison.OrdinalIgnoreCase))
                return "⚠";

            return "•";
        }

        private static string ApplyLogIconPrefix(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return message;

            var icon = GetLogIconPrefix(message);
            var normalized = Regex.Replace(message.TrimStart(), @"^\d{2}:\d{2}:\d{2}\s+", string.Empty);
            return $"{icon} {normalized}";
        }

        private void AppendLog(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendLog), message);
                return;
            }

            // Keep the log empty until a user is logged in.
            if (!IsLoggedIn)
            {
                return;
            }

            var isUdonActivity = message.StartsWith("[UDON-DETECT]", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("[UDON-BUTTON]", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("[WORLD-UDON]", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("[PICKUP]", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("[PICKUP-FLOOD]", StringComparison.OrdinalIgnoreCase);
            if (isUdonActivity)
            {
                var normalized = Regex.Replace(message.TrimStart(), @"^\d{2}:\d{2}:\d{2}\s+", string.Empty);
                var isFlood = normalized.Contains("burst of Udon interaction activity", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("massive Udon exception flood", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("malicious Udon indicator", StringComparison.OrdinalIgnoreCase)
                    || normalized.StartsWith("[PICKUP-FLOOD]", StringComparison.OrdinalIgnoreCase);

                if (isFlood)
                    AppendUdonFloodLog(message);
                else
                    AppendUdonLog(message);
                return;
            }

            if (ShouldHideFromMainLog(message))
            {
                return;
            }

            if (message.StartsWith("[RIP-SUS]", StringComparison.OrdinalIgnoreCase))
            {
                // RIP suspicion logging is disabled for now.
                return;
            }

            var logLine = ApplyLogIconPrefix(message);
            var originalColor = logBox.SelectionColor;
            var originalBackColor = logBox.SelectionBackColor;

            // Color coding by log type
            System.Drawing.Color textColor;
            if (message.StartsWith("JOIN:", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("Player joined:", StringComparison.OrdinalIgnoreCase))
                textColor = UiTheme.LogJoin;
            else if (message.Contains(" left"))
                textColor = UiTheme.LogLeave;
            else if (message.StartsWith("[AUTO-BAN]", StringComparison.OrdinalIgnoreCase))
                textColor = UiTheme.LogDanger;
            else if (message.StartsWith("[GROUP-CHECK]", StringComparison.OrdinalIgnoreCase))
                textColor = UiTheme.LogGroupCheck;
            else if (message.StartsWith("[GROUP-BL]", StringComparison.OrdinalIgnoreCase))
                textColor = UiTheme.LogGroupMatch;
            else if (message.StartsWith("[RESTRICTED-SCAN]", StringComparison.OrdinalIgnoreCase))
                textColor = UiTheme.LogRestricted;
            else if (message.StartsWith("[INSTANCE]", StringComparison.OrdinalIgnoreCase))
                textColor = UiTheme.LogInstance;
            else if (message.StartsWith("[AUTH]", StringComparison.OrdinalIgnoreCase))
                textColor = UiTheme.LogAuth;
            else if (message.StartsWith("[ERROR]", StringComparison.OrdinalIgnoreCase))
                textColor = UiTheme.LogDanger;
            else if (message.StartsWith("[INFO]", StringComparison.OrdinalIgnoreCase))
                textColor = UiTheme.TextMuted;
            else if (message.StartsWith("[DETECTOR]", StringComparison.OrdinalIgnoreCase))
                textColor = UiTheme.TextMuted;
            else
                textColor = UiTheme.TextPrimary;

            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.SelectionColor = textColor;
            logBox.AppendText($"{DateTime.Now:HH:mm:ss} {logLine}\n");
            logBox.SelectionColor = originalColor;
            logBox.SelectionBackColor = originalBackColor;
            AddLogCard(message, textColor);
            logBox.ScrollToCaret();
        }

        private void AddLogCard(string message, System.Drawing.Color accentColor)
        {
            var card = new Panel
            {
                Height = 64,
                BackColor = System.Drawing.Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(4, 4, 4, 2),
                Padding = new Padding(8)
            };

            var icon = new Label
            {
                Text = GetLogIconPrefix(message),
                ForeColor = accentColor,
                BackColor = System.Drawing.Color.Transparent,
                Font = new System.Drawing.Font("Segoe UI Symbol", 18f, System.Drawing.FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var content = new Label
            {
                Text = $"{DateTime.Now:HH:mm:ss}\n{message.Trim()}",
                ForeColor = UiTheme.TextPrimary,
                BackColor = System.Drawing.Color.Transparent,
                Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Regular),
                AutoEllipsis = true
            };

            card.Controls.Add(icon);
            card.Controls.Add(content);
            card.Resize += (s, e) =>
            {
                icon.SetBounds(8, 8, 38, Math.Max(30, card.ClientSize.Height - 16));
                content.SetBounds(52, 8, Math.Max(100, card.ClientSize.Width - 62), Math.Max(30, card.ClientSize.Height - 16));
            };

            logCardsPanel.Controls.Add(card);
            ResizeLogCards();
            logCardsPanel.ScrollControlIntoView(card);
        }

        private void AppendUdonLog(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendUdonLog), message);
                return;
            }

            if (!IsLoggedIn)
                return;

            if (string.IsNullOrWhiteSpace(message))
                return;

            if (string.Equals(udonLogBox.Text, "Waiting for Udon, button, or item pickup activity.", StringComparison.Ordinal))
                udonLogBox.Clear();

            var normalized = ApplyLogIconPrefix(message);
            var originalColor = udonLogBox.SelectionColor;
            var originalBackColor = udonLogBox.SelectionBackColor;

            udonLogBox.SelectionStart = udonLogBox.TextLength;
            udonLogBox.SelectionLength = 0;
            udonLogBox.SelectionColor = UiTheme.LogDanger;
            udonLogBox.AppendText($"{DateTime.Now:HH:mm:ss} {normalized}\n");
            udonLogBox.SelectionColor = originalColor;
            udonLogBox.SelectionBackColor = originalBackColor;
            udonLogBox.ScrollToCaret();
        }

        private void AppendUdonFloodLog(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendUdonFloodLog), message);
                return;
            }

            if (!IsLoggedIn)
                return;

            if (string.IsNullOrWhiteSpace(message))
                return;

            if (string.Equals(udonFloodBox.Text, "Waiting for suspicious Udon or pickup bursts.", StringComparison.Ordinal))
                udonFloodBox.Clear();

            var normalized = ApplyLogIconPrefix(message);
            var originalColor = udonFloodBox.SelectionColor;
            var originalBackColor = udonFloodBox.SelectionBackColor;

            udonFloodBox.SelectionStart = udonFloodBox.TextLength;
            udonFloodBox.SelectionLength = 0;
            udonFloodBox.SelectionColor = UiTheme.LogFloodWarning;
            udonFloodBox.AppendText($"{DateTime.Now:HH:mm:ss} {normalized}\n");
            udonFloodBox.SelectionColor = originalColor;
            udonFloodBox.SelectionBackColor = originalBackColor;
            udonFloodBox.ScrollToCaret();
        }

        private static bool ShouldHideFromMainLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            // Handle both raw messages and messages that may already include a timestamp prefix.
            var normalized = Regex.Replace(message.TrimStart(), @"^\d{2}:\d{2}:\d{2}\s+", string.Empty);

            if (string.Equals(normalized, "[AUTH] Restored saved authenticated session.", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(normalized, "Instance detector started.", StringComparison.OrdinalIgnoreCase))
                return true;

            if (normalized.StartsWith("[GROUP-CHECK] Loaded blacklist file:", StringComparison.OrdinalIgnoreCase))
                return true;

            if (normalized.StartsWith("[GROUP-CHECK] Loaded ", StringComparison.OrdinalIgnoreCase) &&
                normalized.Contains(" blacklisted groups.", StringComparison.OrdinalIgnoreCase))
                return true;

            if (normalized.StartsWith("[DETECTOR] Watching ", StringComparison.OrdinalIgnoreCase))
                return true;

            if (normalized.StartsWith("[UDON-DETECT]", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private void AppendBannedPlayer(string playerName, string playerId, string groupId)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, string, string>(AppendBannedPlayer), playerName, playerId, groupId);
                return;
            }

            var row = BuildBannedPlayerRow(playerName, playerId, groupId);
            bannedPlayersPanel.Controls.Add(row);
            bannedPlayersPanel.ScrollControlIntoView(row);
        }

        // Builds one row for the banned-players list: the player's name plus an "Unban"
        // button that lifts the group ban straight from the app.
        private Panel BuildBannedPlayerRow(string playerName, string playerId, string groupId)
        {
            var safeName = string.IsNullOrWhiteSpace(playerName) ? "Unknown" : playerName;

            var row = new Panel
            {
                Height = 30,
                Margin = new System.Windows.Forms.Padding(0, 0, 0, 4),
                BackColor = UiTheme.Surface,
                Width = BannedRowWidth()
            };

            var unbanButton = new System.Windows.Forms.Button
            {
                Text = "Unban",
                Width = 70,
                Dock = System.Windows.Forms.DockStyle.Right
            };
            UiTheme.StyleButton(unbanButton);

            var nameLabel = new System.Windows.Forms.Label
            {
                Text = $"{DateTime.Now:HH:mm:ss} {safeName}",
                Dock = System.Windows.Forms.DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextPrimary,
                BackColor = System.Drawing.Color.Transparent,
                AutoEllipsis = true,
                Padding = new System.Windows.Forms.Padding(6, 0, 0, 0)
            };

            // Fill control is added first so it keeps the space left of the docked button.
            row.Controls.Add(nameLabel);
            row.Controls.Add(unbanButton);

            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(groupId))
            {
                unbanButton.Enabled = false;
            }
            else
            {
                unbanButton.Click += async (s, e) =>
                    await UnbanPlayerAsync(safeName, playerId, groupId, row, unbanButton);
            }

            return row;
        }

        // Lifts the group ban for a single player and removes their row on success.
        private async Task UnbanPlayerAsync(
            string playerName, string playerId, string groupId,
            Panel row, System.Windows.Forms.Button unbanButton)
        {
            var apiToUse = api ?? new VRChatAPI(email ?? "", password ?? "", authCookies);

            unbanButton.Enabled = false;
            unbanButton.Text = "...";

            (bool success, string error) result;
            try
            {
                result = await apiToUse.UnbanUserFromGroup(groupId, playerId);
            }
            catch (Exception ex)
            {
                result = (false, ex.Message);
            }

            if (result.success)
            {
                AppendLog($"[UNBAN] {playerName} has been unbanned from your group.");
                bannedPlayersPanel.Controls.Remove(row);
                row.Dispose();
            }
            else
            {
                AppendLog($"[UNBAN] Failed to unban {playerName}. {result.error}");
                unbanButton.Text = "Unban";
                unbanButton.Enabled = true;
            }
        }

        private void ClearBannedPlayers()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ClearBannedPlayers));
                return;
            }

            var rows = bannedPlayersPanel.Controls.Cast<Control>().ToArray();
            bannedPlayersPanel.Controls.Clear();
            foreach (var row in rows)
                row.Dispose();
        }

        private int BannedRowWidth()
        {
            var width = bannedPlayersPanel.ClientSize.Width - 6;
            return width < 40 ? 40 : width;
        }

        private void SyncBannedRowWidths()
        {
            var width = BannedRowWidth();
            foreach (Control row in bannedPlayersPanel.Controls)
                row.Width = width;
        }

        // Loads the current world's details into the World Information tab. Uses the
        // authenticated API's single world fetch; runs off the world-change event.
        private async Task PopulateWorldInfoAsync(string? worldId)
        {
            if (api == null || string.IsNullOrWhiteSpace(worldId))
                return;

            try
            {
                var world = await api.GetWorldAsync(worldId);
                if (world == null || world["error"] != null)
                {
                    SetWorldInfoText($"Could not load world information for {worldId}.");
                    return;
                }

                string name = world["name"]?.ToString() ?? "(unknown)";
                string author = world["authorName"]?.ToString() ?? "(unknown)";
                string capacity = world["capacity"]?.ToString() ?? "?";
                string occupants = world["occupants"]?.ToString() ?? world["publicOccupants"]?.ToString() ?? "?";
                string description = world["description"]?.ToString() ?? "";
                string created = world["created_at"]?.ToString() ?? "";
                string updated = world["updated_at"]?.ToString() ?? "";
                string published = world["publicationDate"]?.ToString() ?? "";
                var tagsArr = world["tags"] as JArray;
                string tags = tagsArr != null
                    ? string.Join(", ", tagsArr.Select(t => t.ToString()).Where(t => !string.IsNullOrWhiteSpace(t)))
                    : "";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Name:        {name}");
                sb.AppendLine($"Author:      {author}");
                sb.AppendLine($"Capacity:    {capacity}    (public players: {occupants})");
                sb.AppendLine();
                sb.AppendLine("Description:");
                sb.AppendLine(string.IsNullOrWhiteSpace(description) ? "(none)" : description);
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(published))
                    sb.AppendLine($"Published:   {FormatWorldDate(published)}");
                if (!string.IsNullOrWhiteSpace(created))
                    sb.AppendLine($"Created:     {FormatWorldDate(created)}");
                if (!string.IsNullOrWhiteSpace(updated))
                    sb.AppendLine($"Updated:     {FormatWorldDate(updated)}");
                sb.AppendLine();
                sb.AppendLine($"World ID:    {worldId}");
                if (!string.IsNullOrWhiteSpace(tags))
                {
                    sb.AppendLine();
                    sb.AppendLine($"Tags: {tags}");
                }

                SetWorldInfoText(sb.ToString());
            }
            catch (Exception ex)
            {
                SetWorldInfoText($"Failed to load world information: {ex.Message}");
            }
        }

        private void SetWorldInfoText(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(SetWorldInfoText), text);
                return;
            }
            worldInfoBox.Text = text;
        }

        private static string FormatWorldDate(string raw)
            => DateTime.TryParse(raw, out var dt) ? dt.ToString("yyyy-MM-dd") : raw;

        // Loads the logged-in account's profile into the Dashboard's account panel. Works
        // even before logging starts by using a lightweight API instance if needed.
        private async Task PopulateUserInfoAsync()
        {
            if (!IsLoggedIn)
            {
                SetUserInfoText("Log in to see your account information.");
                return;
            }

            try
            {
                var idApi = api ?? new VRChatAPI(email ?? "", password ?? "", authCookies);
                var user = await idApi.GetAuthenticatedUserAsync();
                if (user == null || user["error"] != null)
                {
                    SetUserInfoText("Could not load your account information.");
                    return;
                }

                string name = user["displayName"]?.ToString() ?? displayName ?? "(unknown)";
                string id = user["id"]?.ToString() ?? userId ?? "(unknown)";
                string status = user["status"]?.ToString() ?? "";
                string statusDesc = user["statusDescription"]?.ToString() ?? "";
                string bio = ReadBio(user);
                if (string.IsNullOrWhiteSpace(bio))
                {
                    var profileId = string.IsNullOrWhiteSpace(userId) ? id : userId;
                    var profile = await idApi.GetUserAsync(profileId);
                    if (profile["error"] == null)
                        bio = ReadBio(profile);
                }
                string platform = user["last_platform"]?.ToString() ?? "";
                string dateJoined = user["date_joined"]?.ToString() ?? "";
                string trust = ResolveTrustRank(user["tags"] as JArray);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Display name:  {name}");
                sb.AppendLine($"User ID:       {id}");
                if (!string.IsNullOrWhiteSpace(trust))
                    sb.AppendLine($"Trust rank:    {trust}");
                if (!string.IsNullOrWhiteSpace(status))
                    sb.AppendLine($"Status:        {status}{(string.IsNullOrWhiteSpace(statusDesc) ? "" : $"  \u2014  {statusDesc}")}");
                if (!string.IsNullOrWhiteSpace(platform))
                    sb.AppendLine($"Last platform: {platform}");
                if (!string.IsNullOrWhiteSpace(dateJoined))
                    sb.AppendLine($"Date joined:   {FormatWorldDate(dateJoined)}");
                sb.AppendLine();
                sb.AppendLine("Bio:");
                sb.AppendLine(string.IsNullOrWhiteSpace(bio) ? "(none)" : bio);

                SetUserInfoText(sb.ToString());
            }
            catch (Exception ex)
            {
                SetUserInfoText($"Failed to load account information: {ex.Message}");
            }
        }

        private static string ReadBio(JObject user)
        {
            foreach (var property in user.Properties())
            {
                if (string.Equals(property.Name, "bio", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Name, "profileBio", StringComparison.OrdinalIgnoreCase))
                    return property.Value?.ToString() ?? "";
            }

            return "";
        }

        private void SetUserInfoText(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(SetUserInfoText), text);
                return;
            }
            dashboardUserInfoBox.Text = text;
        }

        // Maps VRChat trust tags to a friendly rank name.
        private static string ResolveTrustRank(JArray? tags)
        {
            if (tags == null)
                return "";

            var set = new HashSet<string>(tags.Select(t => t.ToString()));
            if (set.Contains("system_trust_veteran")) return "Trusted User";
            if (set.Contains("system_trust_trusted")) return "Known User";
            if (set.Contains("system_trust_known")) return "User";
            if (set.Contains("system_trust_basic")) return "New User";
            return "Visitor";
        }

        private void AppendBanReason(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendBanReason), message);
                return;
            }

            if (banReasonBox.TextLength > 0)
            {
                banReasonBox.AppendText("\n\n");
            }

            banReasonBox.AppendText($"{DateTime.Now:HH:mm:ss}\n{message}");
            banReasonBox.ScrollToCaret();
        }

        private void UpdateLobbyPlayers(IReadOnlyList<LoggerEngine.LobbyPlayer> players)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<IReadOnlyList<LoggerEngine.LobbyPlayer>>(UpdateLobbyPlayers), players);
                return;
            }

            lobbyPlayersPanel.SuspendLayout();
            lobbyPlayersPanel.Controls.Clear();

            inInstanceStaffBox.Clear();
            var instanceStaff = players
                .Where(player => player.IsStaff && !string.IsNullOrWhiteSpace(player.StaffGroupName))
                .OrderBy(player => player.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (instanceStaff.Count == 0)
            {
                inInstanceStaffBox.SelectionColor = UiTheme.TextMuted;
                inInstanceStaffBox.AppendText("No approved owned-group staff detected in this instance.\n");
            }
            else
            {
                foreach (var player in instanceStaff)
                {
                    inInstanceStaffBox.SelectionColor = UiTheme.TextPrimary;
                    var role = string.IsNullOrWhiteSpace(player.StaffRole) ? "STAFF" : player.StaffRole;
                    inInstanceStaffBox.AppendText($"[{role}]  {player.DisplayName}  -  {player.StaffGroupName}\n");
                }
            }

            if (players == null || players.Count == 0)
            {
                lobbyPlayersLabel.Text = "Current lobby players (0)";
                var empty = new Label
                {
                    Text = "No players tracked yet.",
                    AutoSize = false,
                    Height = 24,
                    Width = LobbyRowWidth(),
                    ForeColor = UiTheme.TextMuted,
                    BackColor = System.Drawing.Color.Transparent,
                    Padding = new Padding(6, 4, 0, 0)
                };
                lobbyPlayersPanel.Controls.Add(empty);
                lobbyPlayersPanel.ResumeLayout();
                return;
            }

            lobbyPlayersLabel.Text = $"Current lobby players ({players.Count})";
            foreach (var player in players)
                lobbyPlayersPanel.Controls.Add(BuildLobbyPlayerRow(player));

            lobbyPlayersPanel.ResumeLayout();
        }

        // Builds one lobby row: the player's name plus a "Blacklist avatar" button. Extra
        // action buttons can be docked right here as needed.
        private Panel BuildLobbyPlayerRow(LoggerEngine.LobbyPlayer player)
        {
            var safeName = string.IsNullOrWhiteSpace(player.DisplayName) ? "Unknown" : player.DisplayName;
            var avatar = !string.IsNullOrWhiteSpace(player.AvatarId) ? player.AvatarId : player.AvatarName;

            var row = new Panel
            {
                Height = 30,
                Margin = new Padding(0, 0, 0, 4),
                BackColor = UiTheme.Surface,
                Width = LobbyRowWidth()
            };

            var blacklistAvatarButton = new Button
            {
                Text = "Blacklist avatar",
                Width = 130,
                Dock = DockStyle.Right
            };
            UiTheme.StyleButton(blacklistAvatarButton);

            var nameText = player.IsStaff ? $"{safeName}  \u2014  [GROUP STAFF]" : safeName;
            var nameLabel = new Label
            {
                Text = nameText,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextPrimary,
                BackColor = System.Drawing.Color.Transparent,
                AutoEllipsis = true,
                Padding = new Padding(6, 0, 0, 0)
            };

            row.Controls.Add(nameLabel);
            row.Controls.Add(blacklistAvatarButton);

            if (string.IsNullOrWhiteSpace(avatar))
            {
                blacklistAvatarButton.Enabled = false;
            }
            else
            {
                blacklistAvatarButton.Click += async (s, e) =>
                    await BlacklistPlayerAvatarAsync(safeName, avatar!, blacklistAvatarButton);
            }

            return row;
        }

        private int LobbyRowWidth()
        {
            var width = lobbyPlayersPanel.ClientSize.Width - 6;
            return width < 40 ? 40 : width;
        }

        private void SyncLobbyRowWidths()
        {
            var width = LobbyRowWidth();
            foreach (Control row in lobbyPlayersPanel.Controls)
                row.Width = width;
        }

        // Adds a lobby player's current avatar to this account's avatar blacklist and the
        // live logger, so the automod will act on anyone wearing it.
        private async Task BlacklistPlayerAvatarAsync(string playerName, string avatar, Button button)
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show("Please log in first to blacklist avatars.", "Login required",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Blacklist {playerName}'s current avatar?\n\n{avatar}",
                "Blacklist avatar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            button.Enabled = false;
            try
            {
                await EnsureUserIdAsync();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    MessageBox.Show("Could not verify your account yet. Please try again in a moment.",
                        "Not ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    button.Enabled = true;
                    return;
                }

                var added = AccountAvatarBlacklist.Add(userId, avatar);
                logger?.AddBlacklistedAvatar(avatar);

                if (!added)
                    AppendLog($"[AVATAR] '{avatar}' is already on your avatar blacklist.");
                else
                    AppendLog($"[AVATAR] Blacklisted {playerName}'s avatar '{avatar}'.");

                _ = PreloadBlacklistedAvatarsAsync();
                button.Text = "Blacklisted";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not blacklist the avatar.\n\n" + ex.Message, "Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                button.Enabled = true;
            }
        }
    }
}
