# Bluetooth Battery Tray

A lightweight Windows system-tray app that shows your **connected Bluetooth devices and their battery levels** at a glance. Click or hover the tray icon to see every connected device with its battery percentage — updated live as devices connect, disconnect, or drain.

> Built with C# / .NET 8 (WinForms) using the Windows Runtime device APIs.

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Language](https://img.shields.io/badge/C%23-WinForms-178600)

## Features

- 🔋 **Live battery levels** for connected Bluetooth devices (earbuds, headsets, BLE mice/keyboards, …).
- 🔌 **Connected-only** — paired-but-offline devices don't clutter the list.
- ⚡ **Real-time updates** — devices appear/disappear as they connect/disconnect; battery refreshes when Windows reports a change.
- 🖱️ **Tray menu + hover tooltip** — both show one line per device.
- 🪶 **Tiny footprint** — no window, no polling loop; it just listens.

## How it works

The interesting part is that Windows doesn't expose "connected devices" and "battery" in one place — so the app joins two sources.

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

A couple of Windows-specific details handled along the way:

- Device events arrive on a **background thread**; the UI is updated via a WinForms marshaling control.
- The tray tooltip is **capped at 63 characters** by Windows, so long multi-device text is trimmed.
- Left-click uses `NotifyIcon`'s internal `ShowContextMenu` so the menu dismisses correctly on an outside click.

## Project structure

| File | Role |
|------|------|
| `Program.cs` | Entry point; runs a tray-only `ApplicationContext`. |
| `BtMonitor.cs` | The engine: two `DeviceWatcher`s + MAC join, raises `DevicesChanged`. |
| `TrayContext.cs` | The UI: tray icon, menu, hover tooltip, icon rendering. |
| `tray_icon.png` | Tray artwork (auto-cropped and scaled at load). |

## Build & run

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows 10/11.

```bash
dotnet run
```

Or build a release binary:

```bash
dotnet build -c Release
```

The app starts in the system tray (check the `^` overflow area near the clock). Left-click the icon for the device list; hover for the tooltip; **Exit** from the menu to quit.
