# Thumpers Discord Bot

This is a separate local Discord service. It reads `DISCORD_BOT_TOKEN` from the
Windows user environment and non-secret IDs from `botsettings.json`.

Run it from this folder:

```powershell
dotnet run --project .\ThumpersDiscordBot.csproj
```

The bot registers two staff-only commands in the configured server:

- `/automod-status` confirms the bot configuration.
- `/review-ban user_id:<usr_...> reason:<text>` posts a Confirm ban / Cancel
  review in the configured audit channel.

The Confirm button records Discord approval only. A later local moderation bridge
must perform the actual VRChat API ban, so the bot never contains VRChat account
credentials.