# 🐇 VRChat Group Auto Moderation  
A lightweight, safe, and ToS‑compliant **automoderation tool** for VRChat group owners.  
Created by **Loppy The Bunny** to help group moderators protect their communities inside group instances.

---

## 🧩 Overview  
This application automates moderation tasks for VRChat groups and integrates with exposed world‑side moderation systems (such as **ModernUI**) when permitted.  
It runs entirely within VRChat’s allowed systems — no client modification, no reverse‑engineering, and no platform interference.

---

## ⚙️ Features  

| Category | Description |
|-----------|-------------|
| **Logging** | Real‑time instance logging of joins, leaves, and moderation actions. |
| **Group Moderation** | Auto‑kick, auto‑ban, and raid detection using group rules. |
| **World Moderation** | Calls exposed Udon events (e.g., ForceTeleport, MoveToBanArea) when the world creator has granted permission. |
| **Blacklist System** | Blocks users, avatars, or groups listed in configurable text files. |
| **Flagged Words** | Warns moderators when flagged terms appear in chat or logs. |
| **Staff Management** | Recognizes staff groups and prevents accidental bans of authorized moderators. |
| **Dashboard** | Displays current world, group, and instance status. |
| **World Information Tab** | Shows world metadata (name, author, capacity, tags, and current players). |
| **Moderations Tab** | Lists banned players and reasons for moderation actions. |
| **Logs Tab** | Displays detailed event history with timestamps and color‑coded entries. |

---

## 🧾 Compliance  
This project complies with **VRChat Terms of Service §13.2** and **Community Guidelines**.  
It does **not**:
- Interfere with VRChat’s platform systems  
- Circumvent access controls  
- Modify the VRChat client  
- Collect personal data (see §8.10)  
- Access staff‑only Udon events  

It only uses:
- Group moderation APIs  
- Exposed world moderation Udon events  
- Instance‑level logging  
- Publicly accessible data  

---

## 🛡️ Safety  
- All moderation actions occur **inside your group’s instances**.  
- World moderation is used **only when intentionally exposed** by the world creator.  
- The app never touches VRChat’s platform‑level moderation or user data.  
- Designed for **group safety**, not for abuse.

---

## 🧠 Architecture  
| File | Purpose |
|------|----------|
| `VRChatInstanceLogger.cs` | Core logic for instance detection and logging. |
| `InstanceDetector.cs` | Monitors player joins/leaves and triggers automod checks. |
| `VRChatAPI.cs` | Handles group moderation calls and world‑side event triggers. |
| `LoggerEngine.cs` | Manages log formatting and output. |
| `Program.cs` | Application entry point. |
| `UiTheme.cs` | Defines the green/white interface styling. |
| `group_blacklist.txt`, `blacklist.txt` | Lists of banned groups and avatars. |
| `flagged_words.txt` | Words that trigger warnings. |
| `group_staff.txt` | Staff group definitions to prevent false bans. |
| `webhook_config.txt` | Optional Discord webhook integration for alerts. |
| `world_restricted_areas.txt` | Defines world zones for teleport moderation. |

---

## 🧰 Requirements  
- Windows 10/11  
- .NET Framework 4.8+  
- VRChat Group Ownership  
- Optional: a world exposing moderation Udon events (e.g., ModernUI)

---

## 🐾 Credits  
Developed by **Loppy The Bunny**  
- Twitch: twitch.tv/loppythebunny [(twitch.tv in Bing)](https://www.bing.com/search?q="https%3A%2F%2Ftwitch.tv%2Floppythebunny")  
- YouTube: youtube.com/@loppythebunny [(youtube.com in Bing)](https://www.bing.com/search?q="https%3A%2F%2Fyoutube.com%2F%40loppythebunny")  
- Kick: kick.com/loppythebunny [(kick.com in Bing)](https://www.bing.com/search?q="https%3A%2F%2Fkick.com%2Floppythebunny")

---

## 💬 Support  
Join the Discord community for suggestions and feedback.  
This project welcomes contributions that improve safety, performance, or UI clarity.
