using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

class Program
{
    static string ServersFolder = "./servers";
    static string DatabaseFile = "./servers/servers.json";
    static Dictionary<string, Dictionary<string, string>> Database = new Dictionary<string, Dictionary<string, string>>();

    static void Main(string[] args)
    {
        Initialize();

        while (true)
        {
            ClearConsole();
            Console.WriteLine("=== Minecraft Server Manager ===");
            Console.WriteLine("1. Create MC Server");
            Console.WriteLine("2. Manage MC Servers");
            Console.WriteLine("3. Exit");
            Console.Write("Select an option: ");

            string input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    CreateMcServer().Wait();
                    break;
                case "2":
                    ManageServers();
                    break;
                case "3":
                    SaveDatabase();
                    return;
                default:
                    Console.WriteLine("Invalid option. Press any key to continue.");
                    Console.ReadKey();
                    break;
            }
        }
    }

    static void ManageServers()
    {
        while (true)
        {
            ClearConsole();
            Console.WriteLine("=== List of Minecraft Servers ===");
            if (Database.Count == 0)
            {
                Console.WriteLine("No servers found. Press any key to return.");
                Console.ReadKey();
                return;
            }

            var serverList = new List<string>(Database.Keys);
            for (int i = 0; i < serverList.Count; i++)
            {
                var data = Database[serverList[i]];
                bool online = IsServerOnline(serverList[i]);
                if (online)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("● Online ");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("● Offline ");
                }
                Console.ResetColor();
                Console.WriteLine($"{i + 1}. {serverList[i]} ({data["type"]} - {data["version"]})");
            }
            Console.Write("\nSelect server number or press Enter to return: ");
            string input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) return;

            if (int.TryParse(input, out int selected) && selected > 0 && selected <= serverList.Count)
            {
                string serverName = serverList[selected - 1];
                ServerMenu(serverName);
            }
            else
            {
                Console.WriteLine("Invalid selection. Press any key to continue.");
                Console.ReadKey();
            }
        }
    }

    static void ServerMenu(string serverName)
    {
        while (true)
        {
            ClearConsole();
            var data = Database[serverName];
            bool online = IsServerOnline(serverName);
            if (online)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("● Online ");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("● Offline ");
            }
            Console.ResetColor();
            Console.WriteLine($"=== {serverName} ({data["type"]} - {data["version"]}) ===");
            Console.WriteLine("1. Start Server");
            Console.WriteLine("2. Settings");
            Console.WriteLine("3. Update Server");
            Console.WriteLine("4. Delete Server");
            Console.WriteLine("5. Back");
            Console.Write("Select an option: ");
            string input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    StartServer(serverName);
                    break;
                case "2":
                    ServerSettings(serverName);
                    break;
                case "3":
                    UpdateServer(serverName).Wait();
                    break;
                case "4":
                    DeleteServer(serverName);
                    break;
                case "5":
                    return;
                default:
                    Console.WriteLine("Invalid option. Press any key to continue.");
                    Console.ReadKey();
                    break;
            }
        }
    }

    static async Task UpdateServer(string serverName)
    {
        var data = Database[serverName];
        string type = data["type"].ToLower();
        string serverPath = data["server_path"];
        string oldJar = data["jar"];
        string oldJarPath = Path.Combine(serverPath, oldJar);

        string newJarFile = "";
        string newJarPath = "";
        string newVersion = "";

        if (type == "vanilla" || type == "paper")
        {
            List<(string version, string url)> versions = null;
            if (type == "paper")
                versions = await GetPaperVersionsWithUrls();
            else
                versions = await GetVanillaVersionsWithUrls();

            if (versions == null || versions.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Could not fetch versions or no versions available. Press any key to return.");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"\nAvailable {data["type"]} Versions (release only):\n");
            int windowWidth = Console.WindowWidth;
            int colWidth2 = 18;
            int columns2 = Math.Max(1, windowWidth / colWidth2);
            int rows2 = (int)Math.Ceiling(versions.Count / (double)columns2);

            for (int row = 0; row < rows2; row++)
            {
                for (int col = 0; col < columns2; col++)
                {
                    int idx = row + col * rows2;
                    if (idx < versions.Count)
                    {
                        string label = $"{idx + 1}. {versions[idx].version}";
                        Console.Write(label.PadRight(colWidth2));
                    }
                }
                Console.WriteLine();
            }

            Console.Write("\nEnter version number: ");
            string vInput = Console.ReadLine();
            int vIndex;
            if (!int.TryParse(vInput, out vIndex) || vIndex < 1 || vIndex > versions.Count)
            {
                Console.WriteLine("Invalid version. Press any key to return.");
                Console.ReadKey();
                return;
            }
            var selectedVer = versions[vIndex - 1];

            string vJarUrl = selectedVer.url;
            newJarFile = $"{type}-{selectedVer.version}.jar";
            newJarPath = Path.Combine(serverPath, newJarFile);

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    Console.WriteLine($"\nDownloading server jar as {newJarFile}...\n");
                    using (var response = await client.GetAsync(vJarUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        var total = response.Content.Headers.ContentLength ?? -1L;
                        var canReportProgress = total != -1;
                        var buffer = new byte[8192];
                        long read = 0;
                        int readCount;
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fs = new FileStream(newJarPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            while ((readCount = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fs.WriteAsync(buffer, 0, readCount);
                                read += readCount;
                                if (canReportProgress)
                                {
                                    DrawProgressBar((double)read / total, 50);
                                }
                            }
                        }
                        if (canReportProgress)
                        {
                            DrawProgressBar(1, 50);
                            Console.WriteLine();
                        }
                    }
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nDownload failed or the file is corrupted!");
                    Console.ResetColor();
                    Console.WriteLine("Operation cancelled. Press any key to return.");
                    Console.ReadKey();
                    return;
                }
            }
            newVersion = selectedVer.version;
        }
        else
        {
            Console.Write("Provide direct URL to the new .jar file for your loader: ");
            string loaderUrl = Console.ReadLine().Trim();
            if (string.IsNullOrWhiteSpace(loaderUrl))
            {
                Console.WriteLine("Invalid input. Press any key to return.");
                Console.ReadKey();
                return;
            }
            Console.Write("Enter new version (e.g. 1.20.4): ");
            newVersion = Console.ReadLine().Trim();
            if (string.IsNullOrWhiteSpace(newVersion))
            {
                Console.WriteLine("Invalid input. Press any key to return.");
                Console.ReadKey();
                return;
            }
            newJarFile = $"{data["type"].ToLower()}-{newVersion}.jar";
            newJarPath = Path.Combine(serverPath, newJarFile);

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    Console.WriteLine($"\nDownloading loader jar as {newJarFile}...\n");
                    using (var response = await client.GetAsync(loaderUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        var total = response.Content.Headers.ContentLength ?? -1L;
                        var canReportProgress = total != -1;
                        var buffer = new byte[8192];
                        long read = 0;
                        int readCount;
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fs = new FileStream(newJarPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            while ((readCount = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fs.WriteAsync(buffer, 0, readCount);
                                read += readCount;
                                if (canReportProgress)
                                {
                                    DrawProgressBar((double)read / total, 50);
                                }
                            }
                        }
                        if (canReportProgress)
                        {
                            DrawProgressBar(1, 50);
                            Console.WriteLine();
                        }
                    }
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nDownload failed or the file is corrupted!");
                    Console.ResetColor();
                    Console.WriteLine("Operation cancelled. Press any key to return.");
                    Console.ReadKey();
                    return;
                }
            }
        }

        // Move old jar to old_versions
        string oldVersionsDir = Path.Combine(serverPath, "old_versions");
        if (!Directory.Exists(oldVersionsDir))
            Directory.CreateDirectory(oldVersionsDir);

        if (File.Exists(oldJarPath))
        {
            string destPath = Path.Combine(oldVersionsDir, oldJar);
            int i = 1;
            while (File.Exists(destPath))
            {
                destPath = Path.Combine(oldVersionsDir, Path.GetFileNameWithoutExtension(oldJar) + $"_{i}" + Path.GetExtension(oldJar));
                i++;
            }
            File.Move(oldJarPath, destPath);
        }

        // Update database
        data["jar"] = newJarFile;
        data["version"] = newVersion;
        data["executable_path"] = newJarPath;
        Database[serverName] = data;
        SaveDatabase();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nServer updated to version {newVersion}!");
        Console.ResetColor();
        Console.WriteLine("Old .jar moved to old_versions. Press any key to return.");
        Console.ReadKey();
    }

    static void StartServer(string serverName)
    {
        var data = Database[serverName];
        string serverPath = data["server_path"];
        string jarFile = data["jar"];
        string jarPath = Path.Combine(serverPath, jarFile);

        int ram = data.ContainsKey("ram") && int.TryParse(data["ram"], out int r) ? r : 1024;
        bool showConsole = !data.ContainsKey("console") || data["console"] != "false";
        string port = data.ContainsKey("port") ? data["port"] : "25565";

        string args = $"-Xmx{ram}M -Xms{ram}M -jar \"{jarFile}\" nogui --port {port}";

        var process = new Process();
        process.StartInfo.FileName = "java";
        process.StartInfo.Arguments = args;
        process.StartInfo.WorkingDirectory = serverPath;

        if (showConsole)
        {
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.OutputDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine(e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            Console.WriteLine("\nServer stopped. Press any key to return.");
            Console.ReadKey();
        }
        else
        {
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            Console.WriteLine("Server started in background. Press any key to return.");
            Console.ReadKey();
        }
    }

    static void DeleteServer(string serverName)
    {
        var data = Database[serverName];
        string serverPath = data["server_path"];

        // Try to kill running java processes in this server folder
        try
        {
            var processes = Process.GetProcessesByName("java");
            foreach (var proc in processes)
            {
                try
                {
                    string cmdLine = GetCommandLine(proc);
                    if (cmdLine != null && cmdLine.Contains(serverPath))
                    {
                        proc.Kill();
                    }
                }
                catch { }
            }
        }
        catch { }

        try
        {
            if (Directory.Exists(serverPath))
            {
                foreach (string file in Directory.GetFiles(serverPath, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(serverPath, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete server folder: {ex.Message}");
            Console.WriteLine("You may need to close any open files or stop running processes using this folder.");
            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
        }
        Database.Remove(serverName);
        SaveDatabase();
        Console.WriteLine("Server deleted. Press any key to return.");
        Console.ReadKey();
    }

    static void ServerSettings(string serverName)
    {
        var data = Database[serverName];
        var history = new List<(string, ConsoleColor)>();
        while (true)
        {
            ClearConsole();
            Console.WriteLine($"=== Settings for {serverName} ===");
            Console.WriteLine();
            string ram = data.ContainsKey("ram") ? data["ram"] : "1024";
            string console = data.ContainsKey("console") ? data["console"] : "true";
            string port = data.ContainsKey("port") ? data["port"] : "25565";
            string jarFile = data["jar"];
            string startCmd = $"java -Xmx{ram}M -Xms{ram}M -jar \"{jarFile}\" nogui --port {port}";
            Console.WriteLine($"Start command:");
            Console.WriteLine(startCmd);
            Console.WriteLine();

            // EULA accept info
            bool eulaAccepted = data.ContainsKey("eula_accepted") && data["eula_accepted"] == "true";
            if (!eulaAccepted)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("EULA is NOT accepted! Type 'eula accept' to accept and start the server.");
                Console.ResetColor();
            }

            // Print command history with color
            if (history.Count > 0)
            {
                Console.WriteLine("─────────────────────────────────────────────");
                foreach (var (msg, color) in history)
                {
                    Console.ForegroundColor = color;
                    Console.WriteLine(msg);
                }
                Console.ResetColor();
                Console.WriteLine("─────────────────────────────────────────────");
            }

            // Detect plugins or mods folder
            string pluginsDir = Path.Combine(data["server_path"], "plugins");
            string modsDir = Path.Combine(data["server_path"], "mods");
            bool hasPlugins = Directory.Exists(pluginsDir);
            bool hasMods = Directory.Exists(modsDir);

            Console.WriteLine("Type a command (help, back):");
            Console.Write("> ");
            string input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            var parts = input.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            if (parts[0].ToLower() == "back") break;

            // EULA accept command
            if (parts[0].ToLower() == "eula" && parts.Length > 1 && parts[1].ToLower() == "accept")
            {
                string eulaPath = Path.Combine(data["server_path"], "eula.txt");
                try
                {
                    File.WriteAllText(eulaPath, "eula=true");
                    data["eula_accepted"] = "true";
                    data["eula_path"] = eulaPath;
                    Database[serverName] = data;
                    SaveDatabase();
                    history.Add(("EULA accepted. Restarting server...", ConsoleColor.Green));

                    // Restart server
                    string jarPath = Path.Combine(data["server_path"], data["jar"]);
                    var process = new Process();
                    process.StartInfo.FileName = "java";
                    process.StartInfo.Arguments = $"-Xmx1024M -Xms1024M -jar \"{data["jar"]}\" nogui";
                    process.StartInfo.WorkingDirectory = data["server_path"];
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;

                    process.OutputDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine(e.Data); };
                    process.ErrorDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine(e.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();

                    history.Add(("Server started after EULA accepted.", ConsoleColor.Green));
                }
                catch (Exception ex)
                {
                    history.Add(($"Failed to accept EULA: {ex.Message}", ConsoleColor.Red));
                }
                continue;
            }

            if (parts[0].ToLower() == "help")
            {
                var helpBlocks = new List<(string title, ConsoleColor color, string body)>
            {
                ("--- Server Start Commands ---", ConsoleColor.Cyan,
@"  ram <MB>                - Set RAM in MB (e.g. ram 2048)
  console <true/false>    - Show or hide server console
  port <5-digit>          - Set server port (must be 5 digits, e.g. 25565)"),
                ("--- Server Properties Commands ---", ConsoleColor.Cyan,
@"  onlinemode <true/false> - Set online-mode in server.properties
  playerlimit <number>    - Set max-players in server.properties
  motd <text>             - Set motd in server.properties"),
                ("--- General ---", ConsoleColor.Cyan,
@"  back                    - Return to previous menu
  help                    - Show this help
  eula accept             - Accept Minecraft EULA and start server")
            };

                if (hasPlugins)
                {
                    helpBlocks.Add((
                        "--- Plugins ---", ConsoleColor.Cyan,
    @"  plugin add <url> [filename]    - Download plugin jar to plugins/
  plugin remove <plugin>         - Remove plugin jar from plugins/
  plugin show                    - Show all plugins in plugins/"));
                }
                if (hasMods)
                {
                    helpBlocks.Add((
                        "--- Mods ---", ConsoleColor.Cyan,
    @"  mod add <url> [filename]      - Download mod jar to mods/
  mod remove <mod>              - Remove mod jar from mods/
  mod show                      - Show all mods in mods/"));
                }

                history.Add(("", ConsoleColor.White));
                foreach (var (title, color, body) in helpBlocks)
                {
                    history.Add((title, color));
                    foreach (var line in body.Split('\n'))
                        history.Add((line, ConsoleColor.Gray));
                    history.Add(("", ConsoleColor.White));
                }
                continue;
            }

            // --- Server Properties Commands ---
            string propertiesPath = Path.Combine(data["server_path"], "server.properties");
            if (parts[0].ToLower() == "onlinemode" && parts.Length == 2 && (parts[1].ToLower() == "true" || parts[1].ToLower() == "false"))
            {
                if (File.Exists(propertiesPath))
                {
                    var lines = File.ReadAllLines(propertiesPath);
                    bool found = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].StartsWith("online-mode="))
                        {
                            lines[i] = $"online-mode={parts[1].ToLower()}";
                            found = true;
                        }
                    }
                    if (!found)
                    {
                        lines = lines.Concat(new[] { $"online-mode={parts[1].ToLower()}" }).ToArray();
                    }
                    File.WriteAllLines(propertiesPath, lines);
                    history.Add(($"online-mode set to {parts[1].ToLower()}.", ConsoleColor.Green));
                }
                else
                {
                    history.Add(("server.properties not found.", ConsoleColor.Red));
                }
                continue;
            }
            if (parts[0].ToLower() == "playerlimit" && parts.Length == 2 && int.TryParse(parts[1], out int limit))
            {
                if (File.Exists(propertiesPath))
                {
                    var lines = File.ReadAllLines(propertiesPath);
                    bool found = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].StartsWith("max-players="))
                        {
                            lines[i] = $"max-players={limit}";
                            found = true;
                        }
                    }
                    if (!found)
                    {
                        lines = lines.Concat(new[] { $"max-players={limit}" }).ToArray();
                    }
                    File.WriteAllLines(propertiesPath, lines);
                    history.Add(($"max-players set to {limit}.", ConsoleColor.Green));
                }
                else
                {
                    history.Add(("server.properties not found.", ConsoleColor.Red));
                }
                continue;
            }
            if (parts[0].ToLower() == "motd" && parts.Length >= 2)
            {
                string motd = input.Substring(input.IndexOf(' ') + 1);
                if (File.Exists(propertiesPath))
                {
                    var lines = File.ReadAllLines(propertiesPath);
                    bool found = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].StartsWith("motd="))
                        {
                            lines[i] = $"motd={motd}";
                            found = true;
                        }
                    }
                    if (!found)
                    {
                        lines = lines.Concat(new[] { $"motd={motd}" }).ToArray();
                    }
                    File.WriteAllLines(propertiesPath, lines);
                    history.Add(($"motd set to \"{motd}\".", ConsoleColor.Green));
                }
                else
                {
                    history.Add(("server.properties not found.", ConsoleColor.Red));
                }
                continue;
            }

            // --- Plugins commands ---
            if (hasPlugins && parts[0].ToLower() == "plugin" && parts.Length >= 2)
            {
                if (!Directory.Exists(pluginsDir)) Directory.CreateDirectory(pluginsDir);

                if (parts[1].ToLower() == "add" && parts.Length >= 3)
                {
                    var urlAndName = parts[2].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    string url = urlAndName[0];
                    string fileName = urlAndName.Length > 1 ? urlAndName[1] : Path.GetFileName(new Uri(url).AbsolutePath);
                    string pluginPath = Path.Combine(pluginsDir, fileName);
                    using (var client = new HttpClient())
                    {
                        try
                        {
                            using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                            {
                                response.EnsureSuccessStatusCode();
                                var total = response.Content.Headers.ContentLength ?? -1L;
                                var canReportProgress = total != -1;
                                var buffer = new byte[8192];
                                long read = 0;
                                int readCount;
                                using (var stream = response.Content.ReadAsStreamAsync().Result)
                                using (var fs = new FileStream(pluginPath, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    while ((readCount = stream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        fs.Write(buffer, 0, readCount);
                                        read += readCount;
                                        if (canReportProgress)
                                        {
                                            int width = 40;
                                            double percent = (double)read / total;
                                            int filled = (int)(percent * width);
                                            string bar = "[" + new string('#', filled) + new string('-', width - filled) + $"] {(percent * 100):F0}%";
                                            Console.CursorLeft = 0;
                                            Console.Write(bar);
                                        }
                                    }
                                }
                                if (canReportProgress)
                                {
                                    Console.CursorLeft = 0;
                                    Console.WriteLine(new string(' ', 60)); // clear line
                                }
                            }
                            history.Add(($"Plugin {fileName} added.", ConsoleColor.Green));
                        }
                        catch
                        {
                            history.Add(("Failed to download plugin.", ConsoleColor.Red));
                        }
                    }
                }
                else if (parts[1].ToLower() == "remove" && parts.Length >= 3)
                {
                    string search = parts[2].ToLower();
                    var files = Directory.GetFiles(pluginsDir, "*.jar");
                    var matches = files.Select(f => Path.GetFileName(f))
                        .Where(f => f.ToLower().StartsWith(search)).ToList();

                    if (matches.Count == 1)
                    {
                        File.Delete(Path.Combine(pluginsDir, matches[0]));
                        history.Add(($"Plugin {matches[0]} removed.", ConsoleColor.Green));
                    }
                    else if (matches.Count > 1)
                    {
                        string msg = "Multiple matches found:\n" + string.Join("\n", matches.Select(m => "  " + m));
                        history.Add((msg, ConsoleColor.Yellow));
                    }
                    else
                    {
                        history.Add(("No matching plugin found.", ConsoleColor.Red));
                    }
                }
                else if (parts[1].ToLower() == "show")
                {
                    var files = Directory.GetFiles(pluginsDir, "*.jar");
                    if (files.Length == 0)
                    {
                        history.Add(("No plugins found.", ConsoleColor.Yellow));
                    }
                    else
                    {
                        string msg = "Plugins:\n" + string.Join("\n", files.Select(f => "  " + Path.GetFileName(f)));
                        history.Add((msg, ConsoleColor.Cyan));
                    }
                }
                else
                {
                    history.Add(("Invalid plugin command.", ConsoleColor.Red));
                }
                continue;
            }

            // --- Mods commands ---
            if (hasMods && parts[0].ToLower() == "mod" && parts.Length >= 2)
            {
                if (!Directory.Exists(modsDir)) Directory.CreateDirectory(modsDir);

                if (parts[1].ToLower() == "add" && parts.Length >= 3)
                {
                    var urlAndName = parts[2].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    string url = urlAndName[0];
                    string fileName = urlAndName.Length > 1 ? urlAndName[1] : Path.GetFileName(new Uri(url).AbsolutePath);
                    string modPath = Path.Combine(modsDir, fileName);
                    using (var client = new HttpClient())
                    {
                        try
                        {
                            using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                            {
                                response.EnsureSuccessStatusCode();
                                var total = response.Content.Headers.ContentLength ?? -1L;
                                var canReportProgress = total != -1;
                                var buffer = new byte[8192];
                                long read = 0;
                                int readCount;
                                using (var stream = response.Content.ReadAsStreamAsync().Result)
                                using (var fs = new FileStream(modPath, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    while ((readCount = stream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        fs.Write(buffer, 0, readCount);
                                        read += readCount;
                                        if (canReportProgress)
                                        {
                                            int width = 40;
                                            double percent = (double)read / total;
                                            int filled = (int)(percent * width);
                                            string bar = "[" + new string('#', filled) + new string('-', width - filled) + $"] {(percent * 100):F0}%";
                                            Console.CursorLeft = 0;
                                            Console.Write(bar);
                                        }
                                    }
                                }
                                if (canReportProgress)
                                {
                                    Console.CursorLeft = 0;
                                    Console.WriteLine(new string(' ', 60)); // clear line
                                }
                            }
                            history.Add(($"Mod {fileName} added.", ConsoleColor.Green));
                        }
                        catch
                        {
                            history.Add(("Failed to download mod.", ConsoleColor.Red));
                        }
                    }
                }
                else if (parts[1].ToLower() == "remove" && parts.Length >= 3)
                {
                    string search = parts[2].ToLower();
                    var files = Directory.GetFiles(modsDir, "*.jar");
                    var matches = files.Select(f => Path.GetFileName(f))
                        .Where(f => f.ToLower().StartsWith(search)).ToList();

                    if (matches.Count == 1)
                    {
                        File.Delete(Path.Combine(modsDir, matches[0]));
                        history.Add(($"Mod {matches[0]} removed.", ConsoleColor.Green));
                    }
                    else if (matches.Count > 1)
                    {
                        string msg = "Multiple matches found:\n" + string.Join("\n", matches.Select(m => "  " + m));
                        history.Add((msg, ConsoleColor.Yellow));
                    }
                    else
                    {
                        history.Add(("No matching mod found.", ConsoleColor.Red));
                    }
                }
                else if (parts[1].ToLower() == "show")
                {
                    var files = Directory.GetFiles(modsDir, "*.jar");
                    if (files.Length == 0)
                    {
                        history.Add(("No mods found.", ConsoleColor.Yellow));
                    }
                    else
                    {
                        string msg = "Mods:\n" + string.Join("\n", files.Select(f => "  " + Path.GetFileName(f)));
                        history.Add((msg, ConsoleColor.Cyan));
                    }
                }
                else
                {
                    history.Add(("Invalid mod command.", ConsoleColor.Red));
                }
                continue;
            }

            // --- Server Start Commands ---
            if (parts[0].ToLower() == "ram" && parts.Length == 2 && int.TryParse(parts[1], out int ramVal))
            {
                data["ram"] = ramVal.ToString();
                SaveDatabase();
                history.Add(("RAM updated.", ConsoleColor.Green));
            }
            else if (parts[0].ToLower() == "console" && parts.Length == 2)
            {
                data["console"] = parts[1].ToLower() == "true" ? "true" : "false";
                SaveDatabase();
                history.Add(("Console mode updated.", ConsoleColor.Green));
            }
            else if (parts[0].ToLower() == "port" && parts.Length == 2 && parts[1].Length == 5 && int.TryParse(parts[1], out int portVal))
            {
                data["port"] = parts[1];
                SaveDatabase();

                if (File.Exists(propertiesPath))
                {
                    var lines = File.ReadAllLines(propertiesPath);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].StartsWith("server-port="))
                        {
                            lines[i] = $"server-port={parts[1]}";
                        }
                    }
                    File.WriteAllLines(propertiesPath, lines);
                }

                history.Add(("Port updated.", ConsoleColor.Green));
            }
            else
            {
                history.Add(("Unknown command.", ConsoleColor.Red));
            }
        }
    }
    static async Task CreateMcServer()
    {
        ClearConsole();
        Console.WriteLine("=== Create Minecraft Server ===");
        Console.Write("Enter server name: ");
        string name = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(name) || Database.ContainsKey(name))
        {
            Console.WriteLine("Invalid or duplicate server name. Press any key to return.");
            Console.ReadKey();
            return;
        }

        string serverPath = Path.Combine(ServersFolder, name);
        if (!Directory.Exists(serverPath))
            Directory.CreateDirectory(serverPath);

        Console.WriteLine("\nSelect server type:");
        Console.WriteLine("1. Vanilla");
        Console.WriteLine("2. Paper");
        Console.WriteLine("3. Custom Loader");
        Console.Write("Type: ");
        string typeInput = Console.ReadLine();
        string type = typeInput == "2" ? "Paper" : typeInput == "3" ? "Custom" : "Vanilla";

        string jarFileName = "";
        string jarPath = "";
        string versionString = "";
        string executablePath = "";
        string selectedType = type;

        if (type == "Custom")
        {
            Console.Write("Enter loader name (e.g. Mohist, Purpur, etc): ");
            string loaderName = Console.ReadLine().Trim();
            Console.Write("Enter Minecraft version (e.g. 1.20.4): ");
            string mcVersion = Console.ReadLine().Trim();
            Console.Write("Provide direct URL to the .jar file for your loader: ");
            string loaderUrl = Console.ReadLine().Trim();

            if (string.IsNullOrWhiteSpace(loaderName) || string.IsNullOrWhiteSpace(mcVersion) || string.IsNullOrWhiteSpace(loaderUrl))
            {
                Console.WriteLine("Invalid input. Press any key to return.");
                Console.ReadKey();
                return;
            }

            jarFileName = $"{loaderName.ToLower()}-{mcVersion}.jar";
            jarPath = Path.Combine(serverPath, jarFileName);

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    Console.WriteLine($"\nDownloading loader jar as {jarFileName}...\n");
                    using (var response = await client.GetAsync(loaderUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        var total = response.Content.Headers.ContentLength ?? -1L;
                        var canReportProgress = total != -1;
                        var buffer = new byte[8192];
                        long read = 0;
                        int readCount;
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fs = new FileStream(jarPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            while ((readCount = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fs.WriteAsync(buffer, 0, readCount);
                                read += readCount;
                                if (canReportProgress)
                                {
                                    DrawProgressBar((double)read / total, 50);
                                }
                            }
                        }
                        if (canReportProgress)
                        {
                            DrawProgressBar(1, 50);
                            Console.WriteLine();
                        }
                    }
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nDownload failed or the file is corrupted!");
                    Console.ResetColor();
                    Console.WriteLine("Operation cancelled. Press any key to return.");
                    Console.ReadKey();
                    return;
                }
            }

            Console.WriteLine("\nHow do you want to start your loader?");
            Console.WriteLine("Write the full command as you would in terminal/cmd.");
            Console.WriteLine("Use <loader> where the loader jar path should be (example: java -Xmx2G -jar <loader> nogui)");
            Console.Write("Start command (leave empty for default): ");
            string startCmd = Console.ReadLine();

            // If empty, use default MC server start command
            if (string.IsNullOrWhiteSpace(startCmd))
            {
                startCmd = $"java -Xmx1024M -Xms1024M -jar <loader> nogui";
            }

            if (!startCmd.Contains("<loader>"))
            {
                Console.WriteLine("You must use <loader> in your command. Press any key to return.");
                Console.ReadKey();
                return;
            }

            // Replace <loader> with full path to jar
            string realCmd = startCmd.Replace("<loader>", $"\"{jarFileName}\"");

            versionString = mcVersion;
            executablePath = jarPath;
            selectedType = loaderName;

            var dbData = new Dictionary<string, string>
    {
        { "type", selectedType },
        { "version", versionString },
        { "jar", jarFileName },
        { "server_path", serverPath },
        { "executable_path", executablePath },
        { "eula_accepted", "false" },
        { "eula_path", "" },
        { "custom_start_cmd", startCmd }
    };
            Database[name] = dbData;
            SaveDatabase();

            Console.WriteLine("\nStarting the server for the first time...\n");

            bool eulaAccepted = false;
            string eulaPath = Path.Combine(serverPath, "eula.txt");

            while (true)
            {
                // Split command for ProcessStartInfo
                var cmdParts = realCmd.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var process = new Process();
                process.StartInfo.FileName = cmdParts[0];
                process.StartInfo.Arguments = cmdParts.Length > 1 ? cmdParts[1] : "";
                process.StartInfo.WorkingDirectory = serverPath;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;

                DateTime lastOutput = DateTime.Now;
                bool messageShown = false;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine(e.Data);
                        lastOutput = DateTime.Now;
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine(e.Data);
                        lastOutput = DateTime.Now;
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                while (!process.HasExited)
                {
                    await Task.Delay(200);
                    if (!messageShown && (DateTime.Now - lastOutput).TotalMilliseconds > 1500)
                    {
                        Console.WriteLine("\n[CSharp]");
                        messageShown = true;
                    }
                }

                if (File.Exists(eulaPath))
                {
                    string eulaContent = File.ReadAllText(eulaPath);
                    if (eulaContent.Contains("eula=false"))
                    {
                        Console.WriteLine("\nYou must accept the Minecraft EULA to run the server.");
                        Console.Write("Do you want to accept the EULA now? (yes/no): ");
                        string input = Console.ReadLine();
                        if (input.Trim().ToLower() == "yes")
                        {
                            File.WriteAllText(eulaPath, "eula=true");
                            dbData["eula_accepted"] = "true";
                            dbData["eula_path"] = eulaPath;
                            Database[name] = dbData;
                            SaveDatabase();
                            eulaAccepted = true;
                            Console.WriteLine("\nEULA accepted. Restarting the server...\n");
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("EULA not accepted. Server will not start.");
                        }
                    }
                    else if (eulaContent.Contains("eula=true"))
                    {
                        dbData["eula_accepted"] = "true";
                        dbData["eula_path"] = eulaPath;
                        Database[name] = dbData;
                        SaveDatabase();
                        Console.WriteLine("\nServer stopped. You can now manage your server from the menu.");
                        Console.WriteLine("\nPress any key to return to the menu.");
                    }
                }
                break;
            }
            Console.ReadKey();
            return;
        }

        // --- Vanilla & Paper ---
        List<(string version, string url)> versions = null;

        if (type == "Paper")
            versions = await GetPaperVersionsWithUrls();
        else
            versions = await GetVanillaVersionsWithUrls();

        if (versions == null || versions.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Could not fetch versions or no versions available. Press any key to return.");
            Console.ResetColor();
            Console.ReadKey();
            return;
        }

        string jarFileName2 = "";
        string jarPath2 = "";
        string versionString2 = "";
        string executablePath2 = "";
        string selectedType2 = type;

        Console.WriteLine($"\nAvailable {type} Versions (release only):\n");

        int windowWidth = Console.WindowWidth;
        int colWidth2 = 18;
        int columns2 = Math.Max(1, windowWidth / colWidth2);
        int rows2 = (int)Math.Ceiling(versions.Count / (double)columns2);

        for (int row = 0; row < rows2; row++)
        {
            for (int col = 0; col < columns2; col++)
            {
                int idx = row + col * rows2;
                if (idx < versions.Count)
                {
                    string label = $"{idx + 1}. {versions[idx].version}";
                    Console.Write(label.PadRight(colWidth2));
                }
            }
            Console.WriteLine();
        }

        Console.Write("\nEnter version number: ");
        string vInput = Console.ReadLine();
        int vIndex;
        if (!int.TryParse(vInput, out vIndex) || vIndex < 1 || vIndex > versions.Count)
        {
            Console.WriteLine("Invalid version. Press any key to return.");
            Console.ReadKey();
            return;
        }
        var selectedVer = versions[vIndex - 1];

        string vJarUrl = selectedVer.url;
        jarFileName2 = $"{type.ToLower()}-{selectedVer.version}.jar";
        jarPath2 = Path.Combine(serverPath, jarFileName2);

        bool downloadOk = false;
        using (HttpClient client = new HttpClient())
        {
            try
            {
                Console.WriteLine($"\nDownloading server jar as {jarFileName2}...\n");
                using (var response = await client.GetAsync(vJarUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var total = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = total != -1;
                    var buffer = new byte[8192];
                    long read = 0;
                    int readCount;
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fs = new FileStream(jarPath2, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        while ((readCount = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, readCount);
                            read += readCount;
                            if (canReportProgress)
                            {
                                DrawProgressBar((double)read / total, 50);
                            }
                        }
                    }
                    if (canReportProgress)
                    {
                        DrawProgressBar(1, 50);
                        Console.WriteLine();
                    }
                    downloadOk = true;
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nDownload failed or the file is corrupted!");
                Console.ResetColor();
                Console.Write("Do you have a direct URL to the .jar file? (y/n): ");
                string answer = Console.ReadLine().Trim().ToLower();
                if (answer == "y" || answer == "yes")
                {
                    Console.Write("Paste the direct URL to the .jar file: ");
                    vJarUrl = Console.ReadLine().Trim();
                    jarFileName2 = Path.GetFileName(new Uri(vJarUrl).AbsolutePath);
                    jarPath2 = Path.Combine(serverPath, jarFileName2);

                    using (var response = await client.GetAsync(vJarUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        var total = response.Content.Headers.ContentLength ?? -1L;
                        var canReportProgress = total != -1;
                        var buffer = new byte[8192];
                        long read = 0;
                        int readCount;
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fs = new FileStream(jarPath2, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            while ((readCount = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fs.WriteAsync(buffer, 0, readCount);
                                read += readCount;
                                if (canReportProgress)
                                {
                                    DrawProgressBar((double)read / total, 50);
                                }
                            }
                        }
                        if (canReportProgress)
                        {
                            DrawProgressBar(1, 50);
                            Console.WriteLine();
                        }
                    }
                    downloadOk = true;
                }
                else
                {
                    Console.WriteLine("Operation cancelled. Press any key to return.");
                    Console.ReadKey();
                    return;
                }
            }
        }

        versionString2 = selectedVer.version;
        executablePath2 = jarPath2;
        selectedType2 = type;

    AcceptEulaAndStart:
        var dbData2 = new Dictionary<string, string>
    {
        { "type", selectedType2 },
        { "version", versionString2 },
        { "jar", jarFileName2 },
        { "server_path", serverPath },
        { "executable_path", executablePath2 },
        { "eula_accepted", "false" },
        { "eula_path", "" }
    };
        Database[name] = dbData2;
        SaveDatabase();

        Console.WriteLine("\nStarting the server for the first time...\n");

        bool eulaAccepted2 = false;
        string eulaPath2 = Path.Combine(serverPath, "eula.txt");

        while (true)
        {
            var process = new Process();
            process.StartInfo.FileName = "java";
            process.StartInfo.Arguments = $"-Xmx1024M -Xms1024M -jar \"{jarFileName2}\" nogui";
            process.StartInfo.WorkingDirectory = serverPath;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;

            DateTime lastOutput = DateTime.Now;
            bool messageShown = false;

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(e.Data);
                    lastOutput = DateTime.Now;
                }
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(e.Data);
                    lastOutput = DateTime.Now;
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            while (!process.HasExited)
            {
                await Task.Delay(200);
                if (!messageShown && (DateTime.Now - lastOutput).TotalMilliseconds > 1500)
                {
                    Console.WriteLine("\n[CSharp]");
                    messageShown = true;
                }
            }

            if (File.Exists(eulaPath2))
            {
                string eulaContent = File.ReadAllText(eulaPath2);
                if (eulaContent.Contains("eula=false"))
                {
                    Console.WriteLine("\nYou must accept the Minecraft EULA to run the server.");
                    Console.Write("Do you want to accept the EULA now? (yes/no): ");
                    string input = Console.ReadLine();
                    if (input.Trim().ToLower() == "yes")
                    {
                        File.WriteAllText(eulaPath2, "eula=true");
                        dbData2["eula_accepted"] = "true";
                        dbData2["eula_path"] = eulaPath2;
                        Database[name] = dbData2;
                        SaveDatabase();
                        eulaAccepted2 = true;
                        Console.WriteLine("\nEULA accepted. Restarting the server...\n");
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("EULA not accepted. Server will not start.");
                    }
                }
                else if (eulaContent.Contains("eula=true"))
                {
                    dbData2["eula_accepted"] = "true";
                    dbData2["eula_path"] = eulaPath2;
                    Database[name] = dbData2;
                    SaveDatabase();
                    Console.WriteLine("\nServer stopped. You can now manage your server from the menu.");
                    Console.WriteLine("\nPress any key to return to the menu.");
                }
            }
            break;
        }
        Console.ReadKey();
    }
    static void DrawProgressBar(double percent, int width)
    {
        int filled = (int)(percent * width);
        string bar = "[" + new string('#', filled) + new string('-', width - filled) + $"] {percent:P0}";
        Console.CursorLeft = 0;
        Console.Write(bar);
    }

    static async Task<List<(string version, string url)>> GetVanillaVersionsWithUrls()
    {
        var result = new List<(string version, string url)>();
        try
        {
            using (HttpClient client = new HttpClient())
            {
                string url = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
                string json = await client.GetStringAsync(url);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    foreach (var v in doc.RootElement.GetProperty("versions").EnumerateArray())
                    {
                        if (v.GetProperty("type").GetString() == "release")
                        {
                            string version = v.GetProperty("id").GetString();
                            string metaUrl = v.GetProperty("url").GetString();

                            string metaJson = await client.GetStringAsync(metaUrl);
                            using (JsonDocument metaDoc = JsonDocument.Parse(metaJson))
                            {
                                if (metaDoc.RootElement.TryGetProperty("downloads", out var downloads) &&
                                    downloads.TryGetProperty("server", out var server) &&
                                    server.TryGetProperty("url", out var jarUrlElement))
                                {
                                    string jarUrl = jarUrlElement.GetString();
                                    result.Add((version, jarUrl));
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // ignore errors, return what we have
        }
        return result;
    }

    static async Task<List<(string version, string url)>> GetPaperVersionsWithUrls()
    {
        var result = new List<(string version, string url)>();
        try
        {
            using (HttpClient client = new HttpClient())
            {
                string versionsJson = await client.GetStringAsync("https://api.papermc.io/v2/projects/paper");
                using (JsonDocument doc = JsonDocument.Parse(versionsJson))
                {
                    var versions = doc.RootElement.GetProperty("versions").EnumerateArray().Select(v => v.GetString()).ToList();
                    foreach (var version in versions)
                    {
                        string buildsJson = await client.GetStringAsync($"https://api.papermc.io/v2/projects/paper/versions/{version}");
                        using (JsonDocument buildsDoc = JsonDocument.Parse(buildsJson))
                        {
                            var builds = buildsDoc.RootElement.GetProperty("builds").EnumerateArray().Select(b => b.GetInt32()).ToList();
                            if (builds.Count == 0) continue;
                            int maxBuild = builds.Max();
                            string url = $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{maxBuild}/downloads/paper-{version}-{maxBuild}.jar";
                            result.Add((version, url));
                        }
                    }
                }
            }
        }
        catch
        {
            // ignore errors, return what we have
        }
        return result;
    }

    static void Initialize()
    {
        if (!Directory.Exists(ServersFolder))
            Directory.CreateDirectory(ServersFolder);

        if (File.Exists(DatabaseFile))
        {
            string json = File.ReadAllText(DatabaseFile);
            Database = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json)
                ?? new Dictionary<string, Dictionary<string, string>>();
        }
        else
        {
            SaveDatabase();
        }
    }

    static void SaveDatabase()
    {
        string json = JsonSerializer.Serialize(Database, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(DatabaseFile, json);
    }

    static void ClearConsole()
    {
        Console.Clear();
    }

    static bool IsServerOnline(string serverName)
    {
        var data = Database[serverName];
        string jarFile = data["jar"];
        string serverPath = Path.GetFullPath(data["server_path"]);

        foreach (var proc in Process.GetProcessesByName("java"))
        {
            try
            {
                string cmdLine = GetCommandLine(proc);
                if (cmdLine != null && cmdLine.Contains(jarFile))
                {
                    string procDir = "";
                    try { procDir = Path.GetFullPath(proc.StartInfo.WorkingDirectory); } catch { }
                    if (string.IsNullOrEmpty(procDir) || procDir == serverPath)
                        return true;
                }
            }
            catch { }
        }
        return false;
    }

    static string GetCommandLine(Process process)
    {
        try
        {
            using (var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}"))
            {
                foreach (var @object in searcher.Get())
                {
                    return @object["CommandLine"]?.ToString();
                }
            }
        }
        catch { }
        return null;
    }
}