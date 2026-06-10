namespace NetConfigTray.Helpers;

/// <summary>Maps TCP port numbers to common service names and provides default scan sets.</summary>
public static class WellKnownPorts
{
    public static readonly IReadOnlyDictionary<int, string> Services = new Dictionary<int, string>
    {
        [20] = "FTP-data",
        [21] = "FTP",
        [22] = "SSH",
        [23] = "Telnet",
        [25] = "SMTP",
        [53] = "DNS",
        [67] = "DHCP",
        [68] = "DHCP",
        [69] = "TFTP",
        [80] = "HTTP",
        [110] = "POP3",
        [123] = "NTP",
        [135] = "MS-RPC",
        [137] = "NetBIOS",
        [138] = "NetBIOS",
        [139] = "NetBIOS",
        [143] = "IMAP",
        [161] = "SNMP",
        [162] = "SNMP-trap",
        [179] = "BGP",
        [389] = "LDAP",
        [443] = "HTTPS",
        [445] = "SMB",
        [465] = "SMTPS",
        [500] = "IKE/IPsec",
        [514] = "Syslog",
        [515] = "LPD",
        [587] = "SMTP-sub",
        [636] = "LDAPS",
        [993] = "IMAPS",
        [995] = "POP3S",
        [1433] = "MSSQL",
        [1521] = "Oracle",
        [1701] = "L2TP",
        [1723] = "PPTP",
        [1812] = "RADIUS",
        [2049] = "NFS",
        [3128] = "Squid",
        [3268] = "GC-LDAP",
        [3306] = "MySQL",
        [3389] = "RDP",
        [5060] = "SIP",
        [5061] = "SIP-TLS",
        [5432] = "PostgreSQL",
        [5900] = "VNC",
        [5985] = "WinRM",
        [5986] = "WinRM-HTTPS",
        [6379] = "Redis",
        [8000] = "HTTP-alt",
        [8080] = "HTTP-proxy",
        [8443] = "HTTPS-alt",
        [9100] = "JetDirect"
    };

    public static readonly IReadOnlyList<int> CommonPorts = Services.Keys.OrderBy(p => p).ToArray();

    public static string ServiceName(int port) => Services.TryGetValue(port, out var name) ? name : "unknown";
}
