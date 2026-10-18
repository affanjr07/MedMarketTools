# MedMarketTools

A Windows desktop client for managing Steam manifest / **lua** configurations — built with **WPF on .NET 8**.

MedMarketTools finds and installs manifest sources, edits `stplug-in` lua files (depot pinning, per-depot
enable/disable), manages unlocker backends, and can inject a companion plugin into Steam's store pages.
The interface is translated into 29 languages and the app auto-updates via **Velopack**.

> This project is a rebranded fork of the open-source **LuaTools** desktop client.

---

## Features

| Page | What it does |
| --- | --- |
| **Home** | Overview of your library, plugin status, Steam detection and drag-and-drop install. |
| **Add** | Browse Steam's top sellers / new releases or search by name, App ID or Steam store link. |
| **Manage** | Your installed lua entries: covers, App IDs, added/released dates, quick actions. |
| **Depots** | Keep multiple versions of a game's lua and choose which one Steam uses. |
| **Mode** | Pick the unlock backend (OpenSteamTools, BetterSteamTools, or your own custom unlocker). |
| **Fixes** | Community fixes for individual games, filterable by game and fix type. |
| **Plugin** | Install / reinstall / update the Steam store-page plugin (LuaLoader, no Shittenium). |
| **Downloads** | Live download queue plus a history of finished downloads. |
| **Settings** | Account (Sign in with Discord), manifest sources, install behaviour and app updates. |

### Manifest sources

- **Free sources** — available after signing in with Discord. Free accounts have a daily download
  limit per account.
- **Hubcap** — optional. Paste your own Hubcap API key in **Settings → Hubcap** to unlock Hubcap
  manifests (premium sources with no daily limit). Entirely optional; the app works without it.

---

## Requirements

- Windows 10 / 11 (x64).
- The **.NET 8 Desktop Runtime** (the installer can fetch it if it's missing).

No .NET runtime needed if you use a self-contained build.

---

## Install

1. Download `MedMarketTools-win-Setup.exe` from the
   [latest release](https://github.com/affanjr07/MedMarketTools/releases/latest).
2. Run it. The app installs per-user and creates Start Menu / Desktop shortcuts.
3. Launch **MedMarketTools** and sign in with Discord when prompted.

A portable build (`MedMarketTools-win-Portable.zip`) is attached to every release too.

---

## Updating

The app checks this GitHub repository for new releases on startup. When an update is available a
prompt appears with a **Restart** action — no manual reinstall needed.

You can also check manually from **Settings → Updates**.

---

## Building from source

```powershell
# restore + build
dotnet build src/LuaToolsGui/LuaToolsGui.csproj

# run
dotnet run --project src/LuaToolsGui/LuaToolsGui.csproj

# tests
dotnet test tests/LuaToolsGui.Tests/LuaToolsGui.Tests.csproj
```

To produce distributable packages (requires the [Velopack CLI](https://velopack.io)):

```powershell
dotnet publish src/LuaToolsGui/LuaToolsGui.csproj -c Release -r win-x64 --self-contained false -o publish

vpk pack -u MedMarketTools -v <version> -p publish -e MedMarketTools.exe -o Releases `
  --packTitle "MedMarketTools" -c win -i src/LuaToolsGui/icon.ico
```

---

## Credits

This app is a rebrand of the open-source [LuaTools](https://github.com/madoiscool/LuaTools) project and
would not exist without it. Thanks to its authors and contributors, and to the projects it builds on:
SteamTools / OpenSteamTools, LuaLoader, Millennium and Velopack.

---

## Disclaimer

MedMarketTools is provided as-is, for educational and personal use. It is not affiliated with or
endorsed by Valve, Steam, or any game publisher. You are responsible for how you use it and for
complying with any agreements that apply to you.
