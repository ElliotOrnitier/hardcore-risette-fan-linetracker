# Hardcore Risette Fan Line Tracker

A Windows utility for tracking progress toward the **Hardcore Risette Fan** achievement in Persona 4 Golden (Steam).

This achievement requires hearing 250 unique Rise battle lines. The tracker reads P4G's memory in real-time to show which lines you've encountered, which you're missing, and how far you have to go.

---

## Features

- **Real-time tracking** — polls P4G memory every 500ms to detect newly heard lines
- **Progress display** — progress bar and counters showing unique slots hit out of 250 required
- **Line list** — full list of all 645 Rise lines with hit/missing/redundant status
- **Filtering** — search by name or ID, show only missing lines, hide redundant lines
- **Recent hits** — panel showing the last 5 newly heard lines
- **Battle stats** — fusion count, all-out attacks, and weakness exploits alongside the Rise counter
- **Auto-attach** — automatically finds and attaches to the P4G process on launch
- **Manual fallback** — hex address input for advanced users if auto-attach fails (e.g., when using a different game version)

---

## Requirements

- Windows 10 or later
- [Persona 4 Golden](https://store.steampowered.com/app/1113000/Persona_4_Golden/) (Steam, 64-bit)
- The game must be running with a save file loaded (not the title screen)
- The tracker should be run as **Administrator** to read process memory

---

## Usage

1. Download `RiseLineTracker.exe` from the [latest release](../../releases/latest)
2. Run the exe as Administrator
3. Launch P4G and load a save file
4. The tracker will auto-attach and begin updating

> **If auto-attach fails:** Enter the base address of P4G's trophy stats memory manually in the address box. The tooltip on the address field explains how to find it using Cheat Engine.

### Understanding the line list

| Color | Meaning |
|-------|---------|
| Green / gold | Line has been heard |
| Gray | Heard, but redundant (shares a slot with another line already counted — does not contribute to the 250) |
| Dark (default) | Not yet heard |

Lines marked redundant are alternative lines for a slot you've already filled. They show up in game but won't advance your counter.

---

## Building from source

Requires the [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) and Windows.

```powershell
git clone https://github.com/ElliotOrnitier/hardcore-risette-fan-linetracker
cd hardcore-risette-fan-linetracker

dotnet build RiseLineTracker.csproj -c Release
```

The output will be in `bin/Release/net6.0-windows/`.

To produce a standalone single-file exe (no .NET runtime required):

```powershell
dotnet publish RiseLineTracker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Custom line data

The tracker ships with `rise.tsv` embedded in the exe. If you want to override the line data (e.g., to correct an entry), place a `rise.tsv` file in the same directory as the exe — it will be loaded in preference to the embedded version.

---

## Releases

Releases are built automatically via GitHub Actions when a `v*` tag is pushed. Each release contains a single self-contained `RiseLineTracker.exe` that requires no .NET installation.
