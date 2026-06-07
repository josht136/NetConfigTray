# NetConfigTray

A lightweight Windows system tray app that shows active network interfaces with their IPv4 address and Static/DHCP configuration.

## Features

- **System tray icon** — shows **D** (DHCP) or **S** (Static) for the primary interface
- **Click to view** — left-click the tray icon to open a popup with interface details
- **Per-interface info** — name, IPv4, Static/DHCP, and expandable details
- **Interface details** — CIDR, MAC, link speed, live upload/download, gateway, DNS
- **Connected device** — upstream gateway on Ethernet; AP BSSID/SSID/signal on Wi-Fi
- **Copy support** — copy individual fields or all details for an interface
- **Auto-refresh** — updates every 2 seconds while the popup is open
- **Manual refresh** — Refresh button for on-demand updates
- **Exit** — right-click the tray icon → Exit
- **Start with Windows** — enabled by default; toggle via right-click context menu

## Requirements

- Windows 10 or later
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (for framework-dependent builds)

## Build on Windows

```powershell
git clone <repo-url>
cd DCHP-MAn

dotnet restore NetConfigTray.sln
dotnet build NetConfigTray.sln -c Release

# Run locally
dotnet run --project NetConfigTray/NetConfigTray.csproj -c Release
```

### Publish a single-file executable

```powershell
dotnet publish NetConfigTray/NetConfigTray.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -o publish/win-x64
```

The executable is `publish/win-x64/NetConfigTray.exe`.

## Usage

1. Run `NetConfigTray.exe` (or start via `dotnet run`).
2. Find the blue network icon in the system tray (you may need to click the ^ overflow chevron).
3. **Left-click** the icon to open the interface list.
4. **Right-click** for Open, Start with Windows, or Exit.

## How it works

- Active interfaces are detected via WMI (`Win32_NetworkAdapter`) and verified with `System.Net.NetworkInformation`.
- Static vs DHCP is read from the WMI `DHCPEnabled` property on `Win32_NetworkAdapterConfiguration`.
- Autostart is managed through `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

## CI

GitHub Actions builds a Windows release artifact on every push to `main`/`master`. Download `NetConfigTray-win-x64` from the Actions tab after a successful run.
