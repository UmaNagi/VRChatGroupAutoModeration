# 🐇 VRChat Group Auto Moderation

A colorful, lightweight, and ToS-conscious moderation assistant for VRChat group owners.

Protect your group instances with live logging, blacklist tools, moderation alerts, and optional Discord notifications, all from a focused Windows desktop app.

## 🌈 What is new in v1.0.11?

- ✨ Refreshed tabbed interface with Dashboard, World Information, Group Related, Blacklist, Moderations, and Logs tabs
- 🎨 Frutiger Aero-inspired visual refresh with glossy controls and a bright sky-blue theme
- 👤 Dashboard account details, including name, ID, trust rank, status, and bio
- 🌍 Expanded world information with metadata and live lobby players
- 🚫 Quick blacklist controls for groups, users, and avatars
- 🔤 Flagged words watchlist for usernames, bios, and statuses
- 🛡️ Staff moderation alerts for warnings, kicks, bans, and unbans
- ⚡ Udon exploit and crash detection
- 🌀 Force-teleport detection for exposed world moderation systems
- 🔒 Maximum of 100 blacklisted groups per account
- 🧹 Improved avatar blacklist behavior for players already wearing a blocked avatar
- 📐 Fixed layout and control overlap at smaller window sizes
- 💾 Runtime files are stored beside the executable so the app and its settings stay together

## ⚙️ Features

| Area | Functionality |
| --- | --- |
| Logging | Tracks player joins, leaves, avatars, and moderation events in real time. |
| Group moderation | Applies configured group rules and supports automatic bans where permitted. |
| World moderation | Uses exposed Udon moderation events only when the world makes them available. |
| Blacklists | Manage groups, users, avatars, and flagged words. |
| Staff protection | Recognizes configured staff groups to reduce accidental moderation. |
| Discord | Sends optional webhook notifications and can connect to a separately configured moderation bot. |
| Dashboard | Shows account, world, group, and instance status. |

## 📦 Installation

1. Open the [latest release](https://github.com/UmaNagi/VRChatGroupAutoModeration/releases).
2. Download `VRChat Group Auto Moderation.exe`.
3. Place it in a folder you can access.
4. Run the executable.

The public release contains only the self-contained Windows executable. The app creates writable settings beside the executable, so keep the app in a folder where it can write its configuration files.

## 🧭 Usage

1. Log in through the application.
2. Review your group, world, staff, blacklist, and webhook settings.
3. Click **Start Logging**.
4. Watch the **Logs** and **Moderations** tabs for live activity.
5. Use **Stop Logging** when the session is complete.

## 🔤 Flagged Words

Flagged words are warning-only. They can identify terms in player usernames, bios, and statuses, but they do not automatically ban users.

## 🤖 Optional Discord Integration

Discord notifications are optional. The main app can use configured webhooks for alerts. The separate Discord moderation bot requires its own local configuration and token; secrets are never included in public releases.

## 🛡️ Safety and compliance

This project is designed to operate within VRChat's allowed systems:

- It does not modify the VRChat client.
- It does not reverse-engineer or bypass access controls.
- It uses group moderation APIs and intentionally exposed world-side Udon events.
- World moderation actions are only used when supported by the world creator.
- Moderation settings remain under the group owner's control.

Always follow VRChat's Terms of Service and Community Guidelines when using the application.

## 🧰 Requirements

- Windows 10 or Windows 11
- A VRChat account with the permissions needed for your group
- Optional: a world exposing compatible moderation Udon events
- Optional: Discord webhook or separately configured Discord bot

## 🐾 Credits

Created by **Loppy The Bunny**.

- Twitch: [twitch.tv/loppythebunny](https://twitch.tv/loppythebunny)
- YouTube: [youtube.com/@loppythebunny](https://youtube.com/@loppythebunny)
- Kick: [kick.com/loppythebunny](https://kick.com/loppythebunny)

## 💬 Support

Open an issue on GitHub for bug reports, suggestions, and feedback.
