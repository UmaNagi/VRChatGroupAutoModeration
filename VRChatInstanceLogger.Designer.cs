namespace VRChatInstanceLogger
{
    partial class VRChatInstanceLogger
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.startButton = new System.Windows.Forms.Button();
            this.stopButton = new System.Windows.Forms.Button();
            this.madeByLabel = new System.Windows.Forms.Label();
            this.loginButton = new System.Windows.Forms.Button();
            this.logoutButton = new System.Windows.Forms.Button();
            this.updateButton = new System.Windows.Forms.Button();
            this.loggedInLabel = new System.Windows.Forms.Label();
            this.authSessionLabel = new System.Windows.Forms.Label();
            this.instanceStatusLabel = new System.Windows.Forms.Label();
            this.worldNameLabel = new System.Windows.Forms.Label();
            this.ownedGroupNameLabel = new System.Windows.Forms.Label();
            this.updateStatusLabel = new System.Windows.Forms.Label();
            this.groupModerationStatusLabel = new System.Windows.Forms.Label();
            this.instanceBanCountLabel = new System.Windows.Forms.Label();
            this.logsLabel = new System.Windows.Forms.Label();
            this.blacklistedGroupsLabel = new System.Windows.Forms.Label();
            this.logBox = new System.Windows.Forms.RichTextBox();
            this.blacklistedGroupsBox = new System.Windows.Forms.RichTextBox();
            this.addGroupIdBox = new System.Windows.Forms.TextBox();
            this.addGroupButton = new System.Windows.Forms.Button();
            this.removeGroupButton = new System.Windows.Forms.Button();
            this.blacklistedAvatarsLabel = new System.Windows.Forms.Label();
            this.blacklistedAvatarsBox = new System.Windows.Forms.RichTextBox();
            this.addAvatarIdBox = new System.Windows.Forms.TextBox();
            this.addAvatarButton = new System.Windows.Forms.Button();
            this.removeAvatarButton = new System.Windows.Forms.Button();
            this.banReasonLabel = new System.Windows.Forms.Label();
            this.banReasonBox = new System.Windows.Forms.RichTextBox();
            this.bannedPlayersLabel = new System.Windows.Forms.Label();
            this.bannedPlayersPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.lobbyPlayersLabel = new System.Windows.Forms.Label();
            this.lobbyPlayersBox = new System.Windows.Forms.RichTextBox();
            this.ownedGroupsLabel = new System.Windows.Forms.Label();
            this.ownedGroupsBox = new System.Windows.Forms.RichTextBox();
            this.addOwnedGroupIdBox = new System.Windows.Forms.TextBox();
            this.addOwnedGroupButton = new System.Windows.Forms.Button();
            this.removeOwnedGroupButton = new System.Windows.Forms.Button();
            this.staffGroupsLabel = new System.Windows.Forms.Label();
            this.staffGroupsBox = new System.Windows.Forms.RichTextBox();
            this.addStaffGroupIdBox = new System.Windows.Forms.TextBox();
            this.addStaffGroupButton = new System.Windows.Forms.Button();
            this.removeStaffGroupButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // startButton
            // 
            this.startButton.Location = new System.Drawing.Point(15, 15);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(160, 40);
            this.startButton.TabIndex = 0;
            this.startButton.Text = "Start Logging";
            this.startButton.UseVisualStyleBackColor = true;
            this.startButton.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // stopButton
            // 
            this.stopButton.Enabled = false;
            this.stopButton.Location = new System.Drawing.Point(185, 15);
            this.stopButton.Name = "stopButton";
            this.stopButton.Size = new System.Drawing.Size(160, 40);
            this.stopButton.TabIndex = 1;
            this.stopButton.Text = "Stop Logging";
            this.stopButton.UseVisualStyleBackColor = true;
            this.stopButton.Click += new System.EventHandler(this.StopButton_Click);
            // 
            // loginButton
            // 
            this.loginButton.Location = new System.Drawing.Point(355, 15);
            this.loginButton.Name = "loginButton";
            this.loginButton.Size = new System.Drawing.Size(160, 40);
            this.loginButton.TabIndex = 2;
            this.loginButton.Text = "Login";
            this.loginButton.UseVisualStyleBackColor = true;
            this.loginButton.Click += new System.EventHandler(this.LoginButton_Click);
            // 
            // logoutButton
            // 
            this.logoutButton.Location = new System.Drawing.Point(525, 15);
            this.logoutButton.Name = "logoutButton";
            this.logoutButton.Size = new System.Drawing.Size(160, 40);
            this.logoutButton.TabIndex = 3;
            this.logoutButton.Text = "Logout";
            this.logoutButton.UseVisualStyleBackColor = true;
            this.logoutButton.Click += new System.EventHandler(this.LogoutButton_Click);
            // 
            // updateButton
            // 
            this.updateButton.Location = new System.Drawing.Point(695, 15);
            this.updateButton.Name = "updateButton";
            this.updateButton.Size = new System.Drawing.Size(180, 40);
            this.updateButton.TabIndex = 4;
            this.updateButton.Text = "Check for Updates";
            this.updateButton.UseVisualStyleBackColor = true;
            this.updateButton.Click += new System.EventHandler(this.UpdateButton_Click);
            // 
            // 
            // updateStatusLabel
            // 
            this.updateStatusLabel.AutoSize = true;
            this.updateStatusLabel.ForeColor = System.Drawing.Color.DimGray;
            this.updateStatusLabel.Location = new System.Drawing.Point(890, 27);
            this.updateStatusLabel.Name = "updateStatusLabel";
            this.updateStatusLabel.Size = new System.Drawing.Size(120, 20);
            this.updateStatusLabel.TabIndex = 5;
            this.updateStatusLabel.Text = "Update: checking...";
            // 
            // madeByLabel
            // 
            this.madeByLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.madeByLabel.AutoSize = true;
            this.madeByLabel.Location = new System.Drawing.Point(1120, 70);
            this.madeByLabel.Name = "madeByLabel";
            this.madeByLabel.Size = new System.Drawing.Size(200, 20);
            this.madeByLabel.TabIndex = 6;
            this.madeByLabel.Text = "AutoMod made by: Loppy The Bunny";
            // 
            // loggedInLabel
            // 
            this.loggedInLabel.AutoSize = true;
            this.loggedInLabel.Location = new System.Drawing.Point(15, 70);
            this.loggedInLabel.Name = "loggedInLabel";
            this.loggedInLabel.Size = new System.Drawing.Size(140, 20);
            this.loggedInLabel.TabIndex = 2;
            this.loggedInLabel.Text = "Logged in: (none)";
            // 
            // authSessionLabel
            // 
            this.authSessionLabel.AutoSize = true;
            this.authSessionLabel.ForeColor = System.Drawing.Color.DimGray;
            this.authSessionLabel.Location = new System.Drawing.Point(15, 90);
            this.authSessionLabel.Name = "authSessionLabel";
            this.authSessionLabel.Size = new System.Drawing.Size(152, 20);
            this.authSessionLabel.TabIndex = 3;
            this.authSessionLabel.Text = "Session: Not checked";
            // 
            // instanceStatusLabel
            // 
            this.instanceStatusLabel.AutoSize = true;
            this.instanceStatusLabel.ForeColor = System.Drawing.Color.Red;
            this.instanceStatusLabel.Location = new System.Drawing.Point(15, 115);
            this.instanceStatusLabel.Name = "instanceStatusLabel";
            this.instanceStatusLabel.Size = new System.Drawing.Size(140, 20);
            this.instanceStatusLabel.TabIndex = 4;
            this.instanceStatusLabel.Text = "Instance: Waiting...";
            // 
            // worldNameLabel
            // 
            this.worldNameLabel.AutoSize = true;
            this.worldNameLabel.Location = new System.Drawing.Point(15, 145);
            this.worldNameLabel.Name = "worldNameLabel";
            this.worldNameLabel.Size = new System.Drawing.Size(120, 20);
            this.worldNameLabel.TabIndex = 5;
            this.worldNameLabel.Text = "World: (none)";
            // 
            // ownedGroupNameLabel
            // 
            this.ownedGroupNameLabel.AutoSize = true;
            this.ownedGroupNameLabel.Location = new System.Drawing.Point(15, 165);
            this.ownedGroupNameLabel.Name = "ownedGroupNameLabel";
            this.ownedGroupNameLabel.Size = new System.Drawing.Size(173, 20);
            this.ownedGroupNameLabel.TabIndex = 6;
            this.ownedGroupNameLabel.Text = "Owned group: (not set)";
            // 
            // groupModerationStatusLabel
            // 
            this.groupModerationStatusLabel.AutoSize = true;
            this.groupModerationStatusLabel.Location = new System.Drawing.Point(15, 185);
            this.groupModerationStatusLabel.Name = "groupModerationStatusLabel";
            this.groupModerationStatusLabel.Size = new System.Drawing.Size(173, 20);
            this.groupModerationStatusLabel.TabIndex = 6;
            this.groupModerationStatusLabel.Text = "Group moderation status: Moderation is not active";
            // 
            // instanceBanCountLabel
            // 
            this.instanceBanCountLabel.AutoSize = true;
            this.instanceBanCountLabel.Location = new System.Drawing.Point(15, 205);
            this.instanceBanCountLabel.Name = "instanceBanCountLabel";
            this.instanceBanCountLabel.Size = new System.Drawing.Size(173, 20);
            this.instanceBanCountLabel.TabIndex = 6;
            this.instanceBanCountLabel.Text = "Instance Ban Count: 0";
            // 
            // logsLabel
            // 
            this.logsLabel.AutoSize = true;
            this.logsLabel.Location = new System.Drawing.Point(15, 205);
            this.logsLabel.Name = "logsLabel";
            this.logsLabel.Size = new System.Drawing.Size(44, 20);
            this.logsLabel.TabIndex = 7;
            this.logsLabel.Text = "Logs:";
            // 
            // blacklistedGroupsLabel
            // 
            this.blacklistedGroupsLabel.AutoSize = true;
            this.blacklistedGroupsLabel.Location = new System.Drawing.Point(320, 205);
            this.blacklistedGroupsLabel.Name = "blacklistedGroupsLabel";
            this.blacklistedGroupsLabel.Size = new System.Drawing.Size(123, 20);
            this.blacklistedGroupsLabel.TabIndex = 8;
            this.blacklistedGroupsLabel.Text = "Blacklisted groups";
            // 
            // logBox
            //
            this.logBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.logBox.Location = new System.Drawing.Point(15, 195);
            this.logBox.Name = "logBox";
            this.logBox.Size = new System.Drawing.Size(610, 180);
            this.logBox.TabIndex = 7;
            this.logBox.Text = "";
            this.logBox.BackColor = System.Drawing.Color.White;
            // 
            // blacklistedGroupsBox
            // 
            this.blacklistedGroupsBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.blacklistedGroupsBox.Location = new System.Drawing.Point(320, 230);
            this.blacklistedGroupsBox.Name = "blacklistedGroupsBox";
            this.blacklistedGroupsBox.ReadOnly = true;
            this.blacklistedGroupsBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.blacklistedGroupsBox.Size = new System.Drawing.Size(305, 145);
            this.blacklistedGroupsBox.TabIndex = 8;
            this.blacklistedGroupsBox.Text = "";
            this.blacklistedGroupsBox.BackColor = System.Drawing.Color.White;
            // 
            // addGroupIdBox
            // 
            this.addGroupIdBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.addGroupIdBox.Location = new System.Drawing.Point(320, 383);
            this.addGroupIdBox.Name = "addGroupIdBox";
            this.addGroupIdBox.PlaceholderText = "Enter group ID (grp_...)";
            this.addGroupIdBox.Size = new System.Drawing.Size(195, 27);
            this.addGroupIdBox.TabIndex = 9;
            // 
            // addGroupButton
            // 
            this.addGroupButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.addGroupButton.Location = new System.Drawing.Point(521, 381);
            this.addGroupButton.Name = "addGroupButton";
            this.addGroupButton.Size = new System.Drawing.Size(104, 31);
            this.addGroupButton.TabIndex = 10;
            this.addGroupButton.Text = "Add group ID";
            this.addGroupButton.UseVisualStyleBackColor = true;
            this.addGroupButton.Click += new System.EventHandler(this.AddGroupButton_Click);
            // 
            // removeGroupButton
            // 
            this.removeGroupButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.removeGroupButton.Location = new System.Drawing.Point(521, 415);
            this.removeGroupButton.Name = "removeGroupButton";
            this.removeGroupButton.Size = new System.Drawing.Size(104, 31);
            this.removeGroupButton.TabIndex = 15;
            this.removeGroupButton.Text = "Remove";
            this.removeGroupButton.UseVisualStyleBackColor = true;
            this.removeGroupButton.Click += new System.EventHandler(this.RemoveGroupButton_Click);
            // 
            // blacklistedAvatarsLabel
            // 
            this.blacklistedAvatarsLabel.AutoSize = true;
            this.blacklistedAvatarsLabel.Location = new System.Drawing.Point(640, 205);
            this.blacklistedAvatarsLabel.Name = "blacklistedAvatarsLabel";
            this.blacklistedAvatarsLabel.Size = new System.Drawing.Size(123, 20);
            this.blacklistedAvatarsLabel.TabIndex = 11;
            this.blacklistedAvatarsLabel.Text = "Blacklisted avatars";
            // 
            // blacklistedAvatarsBox
            // 
            this.blacklistedAvatarsBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.blacklistedAvatarsBox.Location = new System.Drawing.Point(640, 230);
            this.blacklistedAvatarsBox.Name = "blacklistedAvatarsBox";
            this.blacklistedAvatarsBox.ReadOnly = true;
            this.blacklistedAvatarsBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.blacklistedAvatarsBox.Size = new System.Drawing.Size(305, 145);
            this.blacklistedAvatarsBox.TabIndex = 12;
            this.blacklistedAvatarsBox.Text = "";
            this.blacklistedAvatarsBox.BackColor = System.Drawing.Color.White;
            // 
            // addAvatarIdBox
            // 
            this.addAvatarIdBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.addAvatarIdBox.Location = new System.Drawing.Point(640, 383);
            this.addAvatarIdBox.Name = "addAvatarIdBox";
            this.addAvatarIdBox.PlaceholderText = "Enter avatar name or ID (avtr_...)";
            this.addAvatarIdBox.Size = new System.Drawing.Size(195, 27);
            this.addAvatarIdBox.TabIndex = 13;
            // 
            // addAvatarButton
            // 
            this.addAvatarButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.addAvatarButton.Location = new System.Drawing.Point(841, 381);
            this.addAvatarButton.Name = "addAvatarButton";
            this.addAvatarButton.Size = new System.Drawing.Size(104, 31);
            this.addAvatarButton.TabIndex = 14;
            this.addAvatarButton.Text = "Add avatar";
            this.addAvatarButton.UseVisualStyleBackColor = true;
            this.addAvatarButton.Click += new System.EventHandler(this.AddAvatarButton_Click);
            // 
            // removeAvatarButton
            // 
            this.removeAvatarButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.removeAvatarButton.Location = new System.Drawing.Point(841, 415);
            this.removeAvatarButton.Name = "removeAvatarButton";
            this.removeAvatarButton.Size = new System.Drawing.Size(104, 31);
            this.removeAvatarButton.TabIndex = 16;
            this.removeAvatarButton.Text = "Remove";
            this.removeAvatarButton.UseVisualStyleBackColor = true;
            this.removeAvatarButton.Click += new System.EventHandler(this.RemoveAvatarButton_Click);
            // 
            // banReasonLabel
            // 
            this.banReasonLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.banReasonLabel.AutoSize = true;
            this.banReasonLabel.Location = new System.Drawing.Point(15, 385);
            this.banReasonLabel.Name = "banReasonLabel";
            this.banReasonLabel.Size = new System.Drawing.Size(103, 20);
            this.banReasonLabel.TabIndex = 6;
            this.banReasonLabel.Text = "Ban reason:";
            // 
            // banReasonBox
            // 
            this.banReasonBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.banReasonBox.Location = new System.Drawing.Point(15, 410);
            this.banReasonBox.Name = "banReasonBox";
            this.banReasonBox.ReadOnly = true;
            this.banReasonBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.banReasonBox.Size = new System.Drawing.Size(610, 175);
            this.banReasonBox.TabIndex = 6;
            this.banReasonBox.Text = "No ban yet.";
            this.banReasonBox.BackColor = System.Drawing.Color.White;
            // 
            // bannedPlayersLabel
            // 
            this.bannedPlayersLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.bannedPlayersLabel.AutoSize = true;
            this.bannedPlayersLabel.Location = new System.Drawing.Point(645, 190);
            this.bannedPlayersLabel.Name = "bannedPlayersLabel";
            this.bannedPlayersLabel.Size = new System.Drawing.Size(130, 20);
            this.bannedPlayersLabel.TabIndex = 6;
            this.bannedPlayersLabel.Text = "Banned players";
            // 
            // bannedPlayersPanel
            // 
            this.bannedPlayersPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.bannedPlayersPanel.AutoScroll = true;
            this.bannedPlayersPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.bannedPlayersPanel.Location = new System.Drawing.Point(645, 215);
            this.bannedPlayersPanel.Name = "bannedPlayersPanel";
            this.bannedPlayersPanel.Size = new System.Drawing.Size(280, 370);
            this.bannedPlayersPanel.TabIndex = 6;
            this.bannedPlayersPanel.WrapContents = false;
            // 
            // lobbyPlayersLabel
            // 
            this.lobbyPlayersLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lobbyPlayersLabel.AutoSize = true;
            this.lobbyPlayersLabel.Location = new System.Drawing.Point(645, 320);
            this.lobbyPlayersLabel.Name = "lobbyPlayersLabel";
            this.lobbyPlayersLabel.Size = new System.Drawing.Size(143, 20);
            this.lobbyPlayersLabel.TabIndex = 10;
            this.lobbyPlayersLabel.Text = "Current lobby players";
            // 
            // lobbyPlayersBox
            // 
            this.lobbyPlayersBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lobbyPlayersBox.Location = new System.Drawing.Point(645, 345);
            this.lobbyPlayersBox.Name = "lobbyPlayersBox";
            this.lobbyPlayersBox.ReadOnly = true;
            this.lobbyPlayersBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.lobbyPlayersBox.Size = new System.Drawing.Size(280, 110);
            this.lobbyPlayersBox.TabIndex = 11;
            this.lobbyPlayersBox.Text = "";
            this.lobbyPlayersBox.BackColor = System.Drawing.Color.White;
            // 
            // ownedGroupsLabel
            // 
            this.ownedGroupsLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.ownedGroupsLabel.AutoSize = true;
            this.ownedGroupsLabel.Location = new System.Drawing.Point(320, 410);
            this.ownedGroupsLabel.Name = "ownedGroupsLabel";
            this.ownedGroupsLabel.Size = new System.Drawing.Size(100, 20);
            this.ownedGroupsLabel.TabIndex = 20;
            this.ownedGroupsLabel.Text = "Owned Group:";
            // 
            // ownedGroupsBox
            // 
            this.ownedGroupsBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.ownedGroupsBox.Location = new System.Drawing.Point(320, 435);
            this.ownedGroupsBox.Name = "ownedGroupsBox";
            this.ownedGroupsBox.ReadOnly = true;
            this.ownedGroupsBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.ownedGroupsBox.Size = new System.Drawing.Size(305, 100);
            this.ownedGroupsBox.TabIndex = 21;
            this.ownedGroupsBox.Text = "";
            this.ownedGroupsBox.BackColor = System.Drawing.Color.White;
            // 
            // addOwnedGroupIdBox
            // 
            this.addOwnedGroupIdBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.addOwnedGroupIdBox.Location = new System.Drawing.Point(320, 540);
            this.addOwnedGroupIdBox.Name = "addOwnedGroupIdBox";
            this.addOwnedGroupIdBox.PlaceholderText = "Enter group ID (grp_...)";
            this.addOwnedGroupIdBox.Size = new System.Drawing.Size(195, 27);
            this.addOwnedGroupIdBox.TabIndex = 22;
            // 
            // addOwnedGroupButton
            // 
            this.addOwnedGroupButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.addOwnedGroupButton.Location = new System.Drawing.Point(521, 538);
            this.addOwnedGroupButton.Name = "addOwnedGroupButton";
            this.addOwnedGroupButton.Size = new System.Drawing.Size(104, 31);
            this.addOwnedGroupButton.TabIndex = 23;
            this.addOwnedGroupButton.Text = "Add group";
            this.addOwnedGroupButton.UseVisualStyleBackColor = true;
            this.addOwnedGroupButton.Click += new System.EventHandler(this.AddOwnedGroupButton_Click);
            // 
            // removeOwnedGroupButton
            // 
            this.removeOwnedGroupButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.removeOwnedGroupButton.Location = new System.Drawing.Point(521, 572);
            this.removeOwnedGroupButton.Name = "removeOwnedGroupButton";
            this.removeOwnedGroupButton.Size = new System.Drawing.Size(104, 31);
            this.removeOwnedGroupButton.TabIndex = 24;
            this.removeOwnedGroupButton.Text = "Remove";
            this.removeOwnedGroupButton.UseVisualStyleBackColor = true;
            this.removeOwnedGroupButton.Click += new System.EventHandler(this.RemoveOwnedGroupButton_Click);
            // 
            // staffGroupsLabel
            // 
            this.staffGroupsLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.staffGroupsLabel.AutoSize = true;
            this.staffGroupsLabel.Location = new System.Drawing.Point(640, 410);
            this.staffGroupsLabel.Name = "staffGroupsLabel";
            this.staffGroupsLabel.Size = new System.Drawing.Size(100, 20);
            this.staffGroupsLabel.TabIndex = 25;
            this.staffGroupsLabel.Text = "Staff Groups:";
            // 
            // staffGroupsBox
            // 
            this.staffGroupsBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.staffGroupsBox.Location = new System.Drawing.Point(640, 435);
            this.staffGroupsBox.Name = "staffGroupsBox";
            this.staffGroupsBox.ReadOnly = true;
            this.staffGroupsBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.staffGroupsBox.Size = new System.Drawing.Size(305, 100);
            this.staffGroupsBox.TabIndex = 26;
            this.staffGroupsBox.Text = "";
            this.staffGroupsBox.BackColor = System.Drawing.Color.White;
            // 
            // addStaffGroupIdBox
            // 
            this.addStaffGroupIdBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.addStaffGroupIdBox.Location = new System.Drawing.Point(640, 540);
            this.addStaffGroupIdBox.Name = "addStaffGroupIdBox";
            this.addStaffGroupIdBox.PlaceholderText = "Enter group ID (grp_...)";
            this.addStaffGroupIdBox.Size = new System.Drawing.Size(195, 27);
            this.addStaffGroupIdBox.TabIndex = 27;
            // 
            // addStaffGroupButton
            // 
            this.addStaffGroupButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.addStaffGroupButton.Location = new System.Drawing.Point(841, 538);
            this.addStaffGroupButton.Name = "addStaffGroupButton";
            this.addStaffGroupButton.Size = new System.Drawing.Size(104, 31);
            this.addStaffGroupButton.TabIndex = 28;
            this.addStaffGroupButton.Text = "Add group";
            this.addStaffGroupButton.UseVisualStyleBackColor = true;
            this.addStaffGroupButton.Click += new System.EventHandler(this.AddStaffGroupButton_Click);
            // 
            // removeStaffGroupButton
            // 
            this.removeStaffGroupButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.removeStaffGroupButton.Location = new System.Drawing.Point(841, 572);
            this.removeStaffGroupButton.Name = "removeStaffGroupButton";
            this.removeStaffGroupButton.Size = new System.Drawing.Size(104, 31);
            this.removeStaffGroupButton.TabIndex = 29;
            this.removeStaffGroupButton.Text = "Remove";
            this.removeStaffGroupButton.UseVisualStyleBackColor = true;
            this.removeStaffGroupButton.Click += new System.EventHandler(this.RemoveStaffGroupButton_Click);
            // 
            // VRChatInstanceLogger
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1400, 760);
            this.Controls.Add(this.ownedGroupsLabel);
            this.Controls.Add(this.ownedGroupsBox);
            this.Controls.Add(this.addOwnedGroupIdBox);
            this.Controls.Add(this.addOwnedGroupButton);
            this.Controls.Add(this.removeOwnedGroupButton);
            this.Controls.Add(this.staffGroupsLabel);
            this.Controls.Add(this.staffGroupsBox);
            this.Controls.Add(this.addStaffGroupIdBox);
            this.Controls.Add(this.addStaffGroupButton);
            this.Controls.Add(this.removeStaffGroupButton);
            this.Controls.Add(this.lobbyPlayersLabel);
            this.Controls.Add(this.lobbyPlayersBox);
            this.Controls.Add(this.blacklistedGroupsLabel);
            this.Controls.Add(this.blacklistedGroupsBox);
            this.Controls.Add(this.addGroupIdBox);
            this.Controls.Add(this.addGroupButton);
            this.Controls.Add(this.removeGroupButton);
            this.Controls.Add(this.blacklistedAvatarsLabel);
            this.Controls.Add(this.blacklistedAvatarsBox);
            this.Controls.Add(this.addAvatarIdBox);
            this.Controls.Add(this.addAvatarButton);
            this.Controls.Add(this.removeAvatarButton);
            this.Controls.Add(this.logsLabel);
            this.Controls.Add(this.logBox);
            this.Controls.Add(this.banReasonLabel);
            this.Controls.Add(this.banReasonBox);
            this.Controls.Add(this.bannedPlayersLabel);
            this.Controls.Add(this.bannedPlayersPanel);
            this.Controls.Add(this.worldNameLabel);
            this.Controls.Add(this.instanceStatusLabel);
            this.Controls.Add(this.authSessionLabel);
            this.Controls.Add(this.loggedInLabel);
            this.Controls.Add(this.ownedGroupNameLabel);
            this.Controls.Add(this.groupModerationStatusLabel);
            this.Controls.Add(this.instanceBanCountLabel);
            this.Controls.Add(this.loginButton);
            this.Controls.Add(this.logoutButton);
            this.Controls.Add(this.updateButton);
            this.Controls.Add(this.updateStatusLabel);
            this.Controls.Add(this.startButton);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.madeByLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new System.Drawing.Size(1280, 720);
            this.Name = "VRChatInstanceLogger";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "VRChat Group Auto Moderation";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.Label madeByLabel;
        private System.Windows.Forms.Button loginButton;
        private System.Windows.Forms.Button logoutButton;
        private System.Windows.Forms.Button updateButton;
        private System.Windows.Forms.Label updateStatusLabel;
        private System.Windows.Forms.Label loggedInLabel;
        private System.Windows.Forms.Label authSessionLabel;
        private System.Windows.Forms.Label instanceStatusLabel;
        private System.Windows.Forms.Label worldNameLabel;
        private System.Windows.Forms.Label ownedGroupNameLabel;
        private System.Windows.Forms.Label groupModerationStatusLabel;
        private System.Windows.Forms.Label instanceBanCountLabel;
        private System.Windows.Forms.Label logsLabel;
        private System.Windows.Forms.Label blacklistedGroupsLabel;
        private System.Windows.Forms.RichTextBox logBox;
        private System.Windows.Forms.RichTextBox blacklistedGroupsBox;
        private System.Windows.Forms.TextBox addGroupIdBox;
        private System.Windows.Forms.Button addGroupButton;
        private System.Windows.Forms.Button removeGroupButton;
        private System.Windows.Forms.Label blacklistedAvatarsLabel;
        private System.Windows.Forms.RichTextBox blacklistedAvatarsBox;
        private System.Windows.Forms.TextBox addAvatarIdBox;
        private System.Windows.Forms.Button addAvatarButton;
        private System.Windows.Forms.Button removeAvatarButton;
        private System.Windows.Forms.Label banReasonLabel;
        private System.Windows.Forms.RichTextBox banReasonBox;
        private System.Windows.Forms.Label bannedPlayersLabel;
        private System.Windows.Forms.FlowLayoutPanel bannedPlayersPanel;
        private System.Windows.Forms.Label lobbyPlayersLabel;
        private System.Windows.Forms.RichTextBox lobbyPlayersBox;
        private System.Windows.Forms.Label ownedGroupsLabel;
        private System.Windows.Forms.RichTextBox ownedGroupsBox;
        private System.Windows.Forms.TextBox addOwnedGroupIdBox;
        private System.Windows.Forms.Button addOwnedGroupButton;
        private System.Windows.Forms.Button removeOwnedGroupButton;
        private System.Windows.Forms.Label staffGroupsLabel;
        private System.Windows.Forms.RichTextBox staffGroupsBox;
        private System.Windows.Forms.TextBox addStaffGroupIdBox;
        private System.Windows.Forms.Button addStaffGroupButton;
        private System.Windows.Forms.Button removeStaffGroupButton;
    }
}

