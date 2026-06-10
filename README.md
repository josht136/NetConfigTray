# NetConfigTray — Tiller Network Tool (TNT)

A lightweight Windows system tray app for field network engineers. It shows active network
interfaces with their IPv4 address and Static/DHCP configuration, and bundles a **Toolbox** of
focused diagnostic tools: equipment console (serial/SSH/Telnet), LLDP/CDP switch-port discovery,
continuous latency/MTR monitoring, TCP port scanning, iperf3 throughput testing, and a Wi-Fi
survey/analyzer.

## Features

- **System tray icon** — shows **D** (DHCP) or **S** (Static) for the primary interface
- **Tray interface list** — left-click the tray icon to list interfaces (name, DHCP/Static, IP); click one to open the full window on that interface
- **Per-interface info** — name, IPv4, Static/DHCP, and expandable details
- **Interface details** — CIDR, MAC, link speed, live upload/download, gateway, DNS
- **Gateway & DNS ping** — round-trip latency to the default gateway and primary DNS server
- **Interface context menu** — right-click an interface for LAN scan, renew/release DHCP, flush DNS, traceroute, continuous ping, and `ipconfig /all`
- **LAN scan** — AngryIP-style ping sweep of the interface subnet (IP, latency, hostname, MAC), with a large-network warning
- **Connected device** — upstream gateway on Ethernet; AP BSSID/SSID/signal on Wi-Fi
- **Copy support** — copy individual fields or all details for an interface
- **DHCP lease info** — server, obtained, and expiry times
- **Wi-Fi details** — channel, band, and radio type
- **Subnet calculator** — network, broadcast, host range, usable hosts
- **Gateway ping** — round-trip latency to default gateway
- **Route metric** — interface routing priority
- **Connection uptime** — time since interface was first seen active
- **Public IP** — external address shown in popup status bar
- **Throughput sparkline** — download history chart per interface
- **Change notifications** — balloon tips when IP or config changes (toggle in tray menu)
- **Auto-refresh** — updates every 2 seconds while the popup is open
- **Manual refresh** — Refresh button for on-demand updates
- **Exit** — right-click the tray icon → Exit
- **Start with Windows** — enabled by default; toggle via right-click context menu

## Field Toolkit

Open the **Toolbox** from the main-window **Tools** button or the tray **Tools** submenu. Several
tools are also available per-interface from the right-click context menu.

- **Console (Serial / SSH / Telnet)** — connect to switch/router consoles. Serial uses a COM cable
  (port/baud/data/parity/stop pickers, default 9600 8N1); SSH uses an interactive shell; Telnet uses
  a raw socket with minimal IAC negotiation. Saved session profiles and optional session logging to
  a file for documentation.
- **LLDP / CDP discovery** — listens (~60 s) on a chosen adapter for LLDP and CDP advertisements and
  reports which **switch and port** you are plugged into (system name, port, VLAN, management IP,
  platform). Requires the [Npcap](https://npcap.com) capture driver.
- **Latency monitor** — continuous ping with min/avg/max/jitter/loss, an optional MTR-style per-hop
  trace, a live chart, and CSV export.
- **Port scan** — parallel TCP connect scan over common or custom ports (`22,80,443,1-1024`) with
  service names and banner grab. Also reachable by right-clicking a host in the LAN scan results.
- **Throughput test (iperf3)** — runs as an iperf3 client or server with a live Mbps chart and parsed
  summary. Bundles `iperf3.exe` (or resolves it from `PATH`).
- **Wi-Fi survey** — scans visible BSSIDs (SSID, signal, channel, band, PHY, security), shows a
  channel-overlap graph per band, and recommends the least-congested channel.

## Requirements

- Windows 10 or later
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (for framework-dependent builds)
- **Optional:** [Npcap](https://npcap.com/#download) — only for LLDP/CDP discovery (the app detects
  it and prompts to install if missing).
- **Optional:** `iperf3.exe` — bundled by the installer; for manual builds, drop it in
  `NetConfigTray/tools/iperf3/` or have it on `PATH`.

## Dependencies

Managed packages (restored via NuGet): `System.Management`, `System.IO.Ports` (serial console),
`SSH.NET` (SSH console), `SharpPcap` + `PacketDotNet` (LLDP/CDP capture), `ManagedNativeWifi`
(Wi-Fi survey). Native: Npcap runtime (optional, LLDP/CDP) and a bundled `iperf3.exe` (throughput).

## Privileges

The app ships with an `asInvoker` manifest, so there is **no UAC prompt at startup**. Operations that
need administrator rights elevate on demand by relaunching with `--elevated-op` (handled early in
`Program.cs`). Packet capture for LLDP/CDP may require running elevated depending on the Npcap install
options.

## Installer

A Windows installer is built with [Inno Setup](https://jrsoftware.org/isinfo.php) from
`installer/TNT.iss`.

```powershell
# Publishes, optionally bundles iperf3, and builds installer\Output\TNT-Setup-<version>.exe
pwsh installer\build-installer.ps1 -Version 2.0.0 -Iperf3Path C:\path\to\iperf3.exe

# Also bundle the Npcap installer (place it in installer\redist\npcap-installer.exe first)
pwsh installer\build-installer.ps1 -Version 2.0.0 -IncludeNpcap
```

The installer offers an optional "Start with Windows" task and, when built with `/DIncludeNpcap`, an
optional Npcap installation step.

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

GitHub Actions builds on every push to `main`/`master`. It downloads and bundles `iperf3.exe`,
publishes the app, and produces an installer with Inno Setup. Download the `NetConfigTray-win-x64`
(portable) and `TNT-Setup` (installer) artifacts from the Actions tab after a successful run.

## Roadmap / future ideas

- **LLDP/CDP discovery** is now implemented (see the Field Toolkit). It reads LLDP/CDP neighbor
  advertisements via Npcap to surface the directly-connected managed switch/modem's chassis ID, port
  ID, system name, VLAN, and management IP — answering "which switch and port am I plugged into?"
- **Set IP / DHCP-probe helpers** that elevate on demand via the existing `--elevated-op` path.
