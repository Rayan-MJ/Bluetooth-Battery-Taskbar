# Bluetooth Battery Taskbar

**See your Bluetooth devices' battery right from the Windows taskbar.** A lightweight tray app that lives next to your clock — click or hover its icon to see every connected Bluetooth device and its battery percentage, updated live as devices connect, disconnect, or drain. No window to manage, no account, no bloat.

> Built with C# / .NET 8 (WinForms) using the Windows Runtime device APIs.

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Language](https://img.shields.io/badge/C%23-WinForms-178600)

![Bluetooth Battery Taskbar showing the tray menu and hover tooltip with a device and its battery percentage](preview.jpg)

## Why

Windows shows Bluetooth battery buried in **Settings → Bluetooth & devices** — several clicks away, and only if you go looking. This puts it one glance from your taskbar instead.

## Features

- 🔋 **Battery at a glance** — hover the taskbar icon for a tooltip, or click for the full list.
- 📡 **Any battery Windows knows** — earbuds, headsets, BLE mice/keyboards.
- 🔌 **Connected-only** — paired-but-offline devices don't clutter the list.
- ⚡ **Real-time** — devices appear/disappear as they connect/disconnect; battery refreshes automatically.
- 🚀 **Start with Windows** — optional launch-at-login toggle, right in the menu.
- 🪶 **Tiny & quiet** — no window, no polling loop; it just listens.
- 📦 **Single portable `.exe`** — self-contained, runs on any Windows 10/11 PC with nothing to install.

## Download

Grab `BtBattery.exe` from the [Releases](../../releases) page and run it — that's it. It's a single self-contained file, so there's nothing to install.

> On first launch, Windows SmartScreen may warn about an "unknown publisher" (the app isn't code-signed). Click **More info → Run anyway**.

## How it works

The interesting part: Windows doesn't expose "connected devices" and "battery" in one place — so the app joins two sources.

| Source | Provides | Missing |
|--------|----------|---------|
| **Association Endpoint** record | the list of connected devices + clean names | no battery |
| **Device node** record | the battery value (property `{104EA319-…} 2`) | only exists for battery-reporting devices |

Two `DeviceWatcher`s run in parallel — one per source — and the app **joins them on the device's MAC address**, which appears inside both records' IDs. That gives a single list of *every connected device*, with battery filled in where available (and a dash where not).

```
Association Endpoint          Device node
"Wireless Earbuds"            battery = 80%
mac aabbccddeeff         ◄──► ...AABBCCDDEEFF...
            │   join on MAC   │
            ▼                 ▼
   Wireless Earbuds   80%
```

A few Windows-specific details handled along the way:

- Device events arrive on a **background thread**; the UI is updated via a WinForms marshaling control.
- The tray tooltip is **capped at 63 characters** by Windows, so long multi-device text is trimmed.
- Left-click uses `NotifyIcon`'s internal `ShowContextMenu` so the menu dismisses correctly on an outside click.
- "Start with Windows" toggles a per-user entry under the registry `Run` key — no admin, no installer.

## Project structure

| File | Role |
|------|------|
| `Program.cs` | Entry point; runs a tray-only `ApplicationContext`. |
| `BtMonitor.cs` | The engine: two `DeviceWatcher`s + MAC join, raises `DevicesChanged`. |
| `TrayContext.cs` | The UI: tray icon, menu, hover tooltip, launch-at-login. |
| `tray_icon.png` | Tray artwork (embedded into the exe). |
| `app.ico` | The exe's file/taskbar icon. |

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows 10/11.

```bash
# Run it
dotnet run

# Build a single portable self-contained exe (what the Releases binary is)
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

The app starts in the system tray (check the `^` overflow area near the clock). Left-click the icon for the device list; hover for the tooltip; **Exit** from the menu to quit.
