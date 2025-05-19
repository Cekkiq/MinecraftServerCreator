# Minecraft Server Creator & Manager

A powerful, yet simple C# console application for Windows that allows you to easily create, manage, update, and configure multiple Minecraft servers (Vanilla, Paper, or any custom loader).  
You can add plugins/mods, edit server properties, accept EULA, update server versions, and start/stop servers from a single menu.

---

## 📑 Table of Contents

- [How to Get the App](#-how-to-get-the-app)
- [Main Menu Overview](#-main-menu-overview)
- [Creating a New Server](#-creating-a-new-server)
- [Managing Servers](#-managing-servers)
- [Updating a Server](#-updating-a-server)
- [Server Settings](#-server-settings)
- [Plugins & Mods](#-plugins--mods)
- [Data Storage](#-data-storage)
- [FAQ](#-faq)
- [Requirements (for source code)](#-requirements-for-source-code)
- [License](#-license)
- [Credits](#-credits)

---

## 📦 How to Get the App

### Option 1: Download Release (.exe) [Recommended]

1. Go to the [Releases](https://github.com/yourusername/yourrepo/releases) page on GitHub.
2. Download the latest `.zip` file (e.g. `mcserver_creator-v1.0.zip`).
3. Extract the archive anywhere on your PC.
4. Run the included `.exe` file (e.g. `mcserver_creator.exe`).
5. **No installation or .NET required!**

### Option 2: Run from Source Code

#### Requirements

- Windows
- [.NET 6.0 SDK or newer](https://dotnet.microsoft.com/download)
- Internet connection (for downloading server jars/plugins/mods)

#### Steps

1. Download the source code (`Code > Download ZIP` or `git clone ...`).
2. Extract or open the folder in your terminal.
3. Run:
   ```sh
   dotnet build
   dotnet run
   ```
4. The app will start in your terminal window.

---

## 🖥️ Main Menu Overview

When you start the app, you will see:

```
=== Minecraft Server Manager ===
1. Create MC Server
2. Manage MC Servers
3. Exit
Select an option:
```

- **1. Create MC Server** – Create a new server (Vanilla, Paper, or Custom Loader)
- **2. Manage MC Servers** – See, start, update, configure, or delete your servers
- **3. Exit** – Quit the app

---

## 🚀 Creating a New Server

1. **Select "1" from the main menu.**
2. Enter a unique server name (e.g. `MySurvivalServer`).
3. Choose server type:
   ```
   1. Vanilla
   2. Paper
   3. Custom Loader
   Type:
   ```
   - **Vanilla** – Official Mojang server
   - **Paper** – High-performance fork with plugin support
   - **Custom Loader** – Any other .jar (e.g. Mohist, Purpur, etc.)

---

### Vanilla or Paper

1. **Choose a version**  
   The app will show a list of available versions.  
   Enter the number of the version you want.

2. **Download**  
   The server jar will be downloaded automatically.  
   If download fails, you can provide a direct URL to the .jar.

3. **First Start & EULA**  
   The server will start once to generate files.  
   If EULA is not accepted, you will be prompted to accept it.

---

### Custom Loader

1. **Enter loader name** (e.g. `Mohist`)
2. **Enter Minecraft version** (e.g. `1.20.4`)
3. **Provide direct URL** to the .jar file for your loader
4. **Start command**
   - You will be asked for a custom start command (e.g. `java -Xmx2G -jar <loader> nogui`)
   - Use `<loader>` as a placeholder for the jar file (leave empty for default command)
5. **Download**  
   The loader jar will be downloaded and renamed to `loadername-version.jar`
6. **First Start & EULA**  
   The server will start once to generate files.  
   If EULA is not accepted, you will be prompted to accept it.

---

## 🛠️ Managing Servers

1. **Select "2" from the main menu.**
2. You will see a list of all servers, with their status (● Online/Offline).
3. Select a server by number.

### Server Menu

```
=== MySurvivalServer (Paper - 1.20.4) ===
1. Start Server
2. Settings
3. Update Server
4. Delete Server
5. Back
Select an option:
```

- **Start Server** – Runs the server in the foreground or background (see below)
- **Settings** – Configure server properties, plugins/mods, RAM, etc.
- **Update Server** – Download and switch to a new version (see below)
- **Delete Server** – Deletes the server folder and all files
- **Back** – Return to server list

---

## 🔄 Updating a Server

1. **Select "Update Server" from the server menu.**
2. The app will detect the server type:
   - **Vanilla/Paper:**
     - Shows all available versions.
     - Select the version number to download and update.
   - **Custom Loader:**
     - Enter a new direct URL to the .jar file.
     - Enter the new version string (e.g. `1.20.6`).
3. The old `.jar` is moved to `servers/<servername>/old_versions/`.
4. The new `.jar` is downloaded and set as the active server jar.
5. The database is updated with the new version and jar name.
6. You will see a confirmation message.

---

## ⚙️ Server Settings

When you enter settings, you will see:

- Current RAM, port, and start command
- EULA status (if not accepted, you can type `eula accept`)
- Command history (shows results of your actions)

### Available Commands

#### General

- `back` – Return to previous menu
- `help` – Show help for all commands
- `eula accept` – Accept Minecraft EULA and restart server

#### Server Start

- `ram <MB>` – Set RAM in MB (e.g. `ram 2048`)
- `console <true/false>` – Show or hide server console (background/foreground)
- `port <5-digit>` – Set server port (e.g. `port 25565`)

#### Server Properties

- `onlinemode <true/false>` – Set online-mode in server.properties
- `playerlimit <number>` – Set max-players in server.properties
- `motd <text>` – Set motd in server.properties

#### Plugins (if `plugins` folder exists)

- `plugin add <url> [filename]` – Download plugin jar to plugins/
- `plugin remove <plugin>` – Remove plugin jar from plugins/
- `plugin show` – Show all plugins in plugins/

#### Mods (if `mods` folder exists)

- `mod add <url> [filename]` – Download mod jar to mods/
- `mod remove <mod>` – Remove mod jar from mods/
- `mod show` – Show all mods in mods/

---

## 🧩 Plugins & Mods

- The app automatically detects if your server has a `plugins` or `mods` folder.
- You can add, remove, and list plugins/mods using the commands above.
- Just provide a direct URL to a `.jar` file (e.g. from CurseForge, Modrinth, etc.).

---

## 📁 Data Storage

- All server data is stored in `servers/servers.json`.
- Each server has its own folder in `servers/`.
- You can safely move or backup the `servers` folder.

---

## ❓ FAQ

**Q: Can I use this on Linux or Mac?**  
A: The app is designed for Windows. Some features (like process management) may not work on other systems.

**Q: Can I run multiple servers at once?**  
A: Yes, but only one can run in the foreground (console mode). Use `console false` to start in background.

**Q: Where are my plugins/mods?**  
A: In the `plugins` or `mods` folder inside your server's folder.

**Q: How do I update my server?**  
A: Use the "Update Server" option in the server menu.

**Q: What happens to old server jars when I update?**  
A: They are moved to the `old_versions` folder inside your server directory.

---

## 📝 Requirements (for source code)

- Windows
- [.NET 6.0 SDK or newer](https://dotnet.microsoft.com/download)
- Internet connection

---

## 🏷️ License

MIT License

---

## 🙏 Credits

- [PaperMC API](https://papermc.io/)
- [Mojang Version Manifest](https://launchermeta.mojang.com/)
- C#/.NET community

---

**This project is not affiliated with Mojang or Microsoft.**
