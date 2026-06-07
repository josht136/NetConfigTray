# Installer redistributables

Drop optional bundled installers here. They are git-ignored.

- `npcap-installer.exe` — the Npcap installer from https://npcap.com/#download.
  Required only if you build the installer with `/DIncludeNpcap` (or
  `-IncludeNpcap` via `build-installer.ps1`) to offer Npcap installation for the
  LLDP/CDP discovery feature.
