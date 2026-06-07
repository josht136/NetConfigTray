# iperf3 binary

Place the Windows `iperf3.exe` (and its `cygwin1.dll` if using the Cygwin build) in this
folder. It is copied next to the application on build/publish, and the Throughput Test tool
resolves it from here (falling back to `iperf3.exe` on `PATH`).

Download: https://iperf.fr/iperf-download.php

This binary is intentionally not committed to the repository; the installer bundles it
(see the packaging milestone).
