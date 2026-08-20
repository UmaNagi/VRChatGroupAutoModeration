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

## 📦 Installation & Usage

### ⚙️ **Installation**

#### **1. Download the Latest Release**
- Go to the **Releases** page on GitHub.  
- Download the newest `.zip` or `.exe` build of **VRChat Group Auto Moderation**.

#### **2. Extract the Files**
Unzip the archive to a folder of your choice, for example:  
**C:\VRChatGroupAutoModeration\\**

#### **3. Run the Application**
Open **VRChatGroupAutoModeration.exe**.  
On first launch, the app automatically creates configuration files:

- group_blacklist.txt  
- blacklist.txt  
- flagged_words.txt  
- group_staff.txt  
- webhook_config.txt  
- world_restricted_areas.txt  

You can edit these files with any text editor.

#### **4. Configure Your Settings**
Customize each file to fit your group’s needs:

- **Blacklisted groups** – prevents users from banned communities.  
- **Blacklisted avatars** – blocks avatars flagged for inappropriate content.  
- **Flagged words** – warns moderators when these appear in chat or logs.  
- **Staff groups** – defines trusted moderators to avoid false bans.  
- **Webhook settings** – optional Discord integration for alerts.  
- **Restricted areas** – defines teleport zones for world moderation.

#### **5. Optional: ModernUI Integration**
If your world creator exposes moderation Udon events (like `ForceTeleport` or `MoveToBanArea`), the app will detect and use them automatically.  
No extra setup required. < will be updated next month.

---

### 🧠 **Usage**

#### **1. Start the App**
Open the program and wait for the dashboard to load.  
Your **owned group**, **current world**, and **instance status** will appear automatically.

#### **2. Start Logging**
Click **Start Logging** to begin monitoring your group instance.  
^You will be asked to login.
The app will:

- Detect players entering or leaving  
- Apply automoderation rules  
- Trigger exposed world moderation events  
- Log all actions in real time  

#### **3. Explore the Tabs**

| Tab | Description |
|-----|--------------|
| **Dashboard** | Overview of current session, world, and moderation status. |
| **World Information** | Displays world metadata (name, author, tags, player count). |
| **Group Related** | Shows owned group and staff groups. |
| **Blacklist** | Manage blacklisted groups, avatars, and flagged words. |
| **Moderations** | View banned players and reasons for moderation actions. |
| **Logs** | Displays join/leave events, moderation actions, and timestamps. |

#### **4. Automated Actions**
The app can automatically:

- ban users matching blacklist rules  
- Warn users for flagged words  
- Detect raid‑like behavior  
- Trigger exposed world moderation (ModernUI)  
- Teleport users to restricted areas (if exposed)  <function not added yet.
- Log all actions for transparency  

#### **5. Stop Logging**
Click **Stop Logging** to end the session safely.  
Logs are saved automatically in the app directory.

#### **6. Logout Behavior**
When you click **Logout**, the app **fully wipes all login data and temporary blacklist entries**.  
This ensures that no sensitive or session‑specific information is retained until you log back in again.  
Your permanent configuration files remain untouched. < meaning any of those blacklist related txt files will remain until you manually wipe them.

---

### 🛡️ **Safety Notes**
This tool:

- Operates only works for your group and can run outside your group. <I will be changing this next month to make sure it detect whether or not your outside the group instance. 
- Uses moderation functions VRChat explicitly allows  
- Triggers world moderation only when the world creator exposes it  
- Never modifies the VRChat client  
- Never accesses staff‑only Udon events  
- Never collects personal data  

Fully compliant with VRChat ToS §13.2 and §8.10.## 📦 Installation & Usage

### ⚙️ **Installation**

#### **1. Download the Latest Release**
- Go to the **Releases** page on GitHub.  
- Download the newest `.zip` or `.exe` build of **VRChat Group Auto Moderation**.

#### **2. Extract the Files**
Unzip the archive to a folder of your choice, for example:  
**C:\VRChatGroupAutoModeration\\**

#### **3. Run the Application**
Open **VRChatGroupAutoModeration.exe**.  
On first launch, the app automatically creates configuration files:

- group_blacklist.txt  
- blacklist.txt  
- flagged_words.txt  
- group_staff.txt  
- webhook_config.txt  
- world_restricted_areas.txt  

You can edit these files with any text editor.

#### **4. Configure Your Settings**
Customize each file to fit your group’s needs:

- **Blacklisted groups** – prevents users from banned communities.  
- **Blacklisted avatars** – blocks avatars flagged for inappropriate content.  
- **Flagged words** – warns moderators when these appear in chat or logs.  
- **Staff groups** – defines trusted moderators to avoid false bans.  
- **Webhook settings** – optional Discord integration for alerts.  
- **Restricted areas** – defines teleport zones for world moderation.

#### **5. Optional: ModernUI Integration**
If your world creator exposes moderation Udon events (like `ForceTeleport` or `MoveToBanArea`), the app will detect and use them automatically.  
No extra setup required. < will be updated next month.
