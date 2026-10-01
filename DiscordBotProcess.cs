using System.Diagnostics;
using System.Net.Sockets;

namespace VRChatInstanceLogger
{
    internal static class DiscordBotProcess
    {
        private const int BridgePort = 48125;
        private static readonly object syncRoot = new();
        private static Process? process;

        public static async Task<bool> EnsureRunningAsync(Action<string> log)
        {
            var botToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN", EnvironmentVariableTarget.User);
            if (string.IsNullOrWhiteSpace(botToken))
            {
                log("[BOT] Discord bot is not configured on this machine; skipping bot startup.");
                return true;
            }

            if (await IsBridgeListeningAsync())
                return true;

            lock (syncRoot)
            {
                if (process is { HasExited: false })
                    return false;

                process = StartProcess(log);
            }

            if (process == null)
                return false;

            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                if (await IsBridgeListeningAsync())
                    return true;

                if (process.HasExited)
                    return false;

                await Task.Delay(250);
            }

            return await IsBridgeListeningAsync();
        }

        private static Process? StartProcess(Action<string> log)
        {
            var botDirectory = FindBotDirectory();
            if (botDirectory == null)
            {
                log("[BOT] Discord bot project was not found near the application directory.");
                return null;
            }

            var projectPath = Path.Combine(botDirectory, "ThumpersDiscordBot.csproj");

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = botDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(projectPath);

            try
            {
                var startedProcess = Process.Start(startInfo);
                if (startedProcess == null)
                    return null;

                startedProcess.OutputDataReceived += (_, args) => ForwardOutput(log, args.Data);
                startedProcess.ErrorDataReceived += (_, args) => ForwardOutput(log, args.Data);
                startedProcess.BeginOutputReadLine();
                startedProcess.BeginErrorReadLine();
                log("[BOT] Starting the Discord bot...");
                return startedProcess;
            }
            catch (Exception exception)
            {
                log($"[BOT] Could not start the Discord bot: {exception.Message}");
                return null;
            }
        }

        private static string? FindBotDirectory()
        {
            var directories = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var path in directories)
            {
                var directory = new DirectoryInfo(path);
                while (directory != null)
                {
                    var botDirectory = Path.Combine(directory.FullName, "discord-bot");
                    if (File.Exists(Path.Combine(botDirectory, "ThumpersDiscordBot.csproj")))
                        return botDirectory;

                    directory = directory.Parent;
                }
            }

            return null;
        }

        private static void ForwardOutput(Action<string> log, string? line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                log("[BOT] " + line);
        }

        private static async Task<bool> IsBridgeListeningAsync()
        {
            try
            {
                using var client = new TcpClient();
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
                await client.ConnectAsync("127.0.0.1", BridgePort, cancellation.Token);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}