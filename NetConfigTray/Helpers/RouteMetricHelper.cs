using System.Management;

namespace NetConfigTray.Helpers;

internal static class RouteMetricHelper
{
    private const string WmiNetNamespace = @"root\standardcimv2";
    private const uint AutomaticRouteMetric = 256;

    public static Dictionary<uint, uint> QueryMetricsByInterface()
    {
        var interfaceMetrics = QueryInterfaceMetrics();
        var effectiveMetrics = QueryDefaultRouteMetrics(interfaceMetrics);

        foreach (var (interfaceIndex, interfaceMetric) in interfaceMetrics)
        {
            if (!effectiveMetrics.ContainsKey(interfaceIndex))
            {
                effectiveMetrics[interfaceIndex] = interfaceMetric;
            }
        }

        if (effectiveMetrics.Count > 0)
        {
            return effectiveMetrics;
        }

        return QueryLegacyRouteMetrics(interfaceMetrics);
    }

    private static Dictionary<uint, uint> QueryInterfaceMetrics()
    {
        var metrics = new Dictionary<uint, uint>();

        try
        {
            using var searcher = CreateNetSearcher(
                "SELECT InterfaceIndex, InterfaceMetric FROM MSFT_NetIPInterface WHERE AddressFamily = 2");

            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                var interfaceIndex = ReadUInt32(obj["InterfaceIndex"]);
                var metric = ReadUInt32(obj["InterfaceMetric"]);
                if (interfaceIndex is null || metric is null)
                {
                    continue;
                }

                metrics[interfaceIndex.Value] = metric.Value;
            }
        }
        catch
        {
            // MSFT classes unavailable on older systems.
        }

        return metrics;
    }

    private static Dictionary<uint, uint> QueryDefaultRouteMetrics(IReadOnlyDictionary<uint, uint> interfaceMetrics)
    {
        var metrics = new Dictionary<uint, uint>();

        try
        {
            using var searcher = CreateNetSearcher(
                "SELECT InterfaceIndex, RouteMetric FROM MSFT_NetRoute WHERE DestinationPrefix = '0.0.0.0/0' AND AddressFamily = 2");

            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                var interfaceIndex = ReadUInt32(obj["InterfaceIndex"]);
                var routeMetric = ReadUInt32(obj["RouteMetric"]);
                if (interfaceIndex is null)
                {
                    continue;
                }

                interfaceMetrics.TryGetValue(interfaceIndex.Value, out var interfaceMetric);
                var effective = CalculateEffectiveMetric(routeMetric, interfaceMetric);
                if (!metrics.TryGetValue(interfaceIndex.Value, out var existing) || effective < existing)
                {
                    metrics[interfaceIndex.Value] = effective;
                }
            }
        }
        catch
        {
            // MSFT classes unavailable on older systems.
        }

        return metrics;
    }

    private static Dictionary<uint, uint> QueryLegacyRouteMetrics(IReadOnlyDictionary<uint, uint> interfaceMetrics)
    {
        var metrics = new Dictionary<uint, uint>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT InterfaceIndex, Metric1 FROM Win32_IP4RouteTable WHERE Destination='0.0.0.0' AND Mask='0.0.0.0'");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            var interfaceIndex = ReadUInt32(obj["InterfaceIndex"]);
            var routeMetric = ReadUInt32(obj["Metric1"]);
            if (interfaceIndex is null)
            {
                continue;
            }

            interfaceMetrics.TryGetValue(interfaceIndex.Value, out var interfaceMetric);
            var effective = CalculateEffectiveMetric(routeMetric, interfaceMetric);
            if (!metrics.TryGetValue(interfaceIndex.Value, out var existing) || effective < existing)
            {
                metrics[interfaceIndex.Value] = effective;
            }
        }

        return metrics;
    }

    private static uint CalculateEffectiveMetric(uint? routeMetric, uint interfaceMetric)
    {
        if (routeMetric is null or 0 or AutomaticRouteMetric)
        {
            return interfaceMetric;
        }

        return routeMetric.Value + interfaceMetric;
    }

    private static ManagementObjectSearcher CreateNetSearcher(string query)
    {
        var scope = new ManagementScope(WmiNetNamespace);
        scope.Connect();
        return new ManagementObjectSearcher(scope, new ObjectQuery(query));
    }

    private static uint? ReadUInt32(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToUInt32(value);
        }
        catch
        {
            return null;
        }
    }
}
