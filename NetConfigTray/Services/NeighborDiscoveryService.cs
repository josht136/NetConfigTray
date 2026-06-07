using NetConfigTray.Helpers;
using NetConfigTray.Models;
using SharpPcap;

namespace NetConfigTray.Services;

public sealed record CaptureDeviceDescriptor(string Name, string Description);

/// <summary>
/// Captures LLDP and CDP advertisements on a chosen adapter (via SharpPcap/Npcap) and raises a
/// <see cref="NeighborFound"/> event for each parsed neighbor. Requires the Npcap driver.
/// </summary>
public sealed class NeighborDiscoveryService : IDisposable
{
    private const string CaptureFilter = "ether proto 0x88cc or ether dst 01:00:0c:cc:cc:cc";

    private ILiveDevice? _device;
    private bool _capturing;

    public event Action<NeighborInfo>? NeighborFound;
    public event Action<string>? Error;

    public static IReadOnlyList<CaptureDeviceDescriptor> ListDevices()
    {
        var result = new List<CaptureDeviceDescriptor>();
        try
        {
            foreach (var device in CaptureDeviceList.Instance)
            {
                result.Add(new CaptureDeviceDescriptor(device.Name, device.Description ?? device.Name));
            }
        }
        catch
        {
            // Npcap missing or enumeration failed.
        }

        return result;
    }

    public void Start(string deviceName)
    {
        Stop();

        var device = CaptureDeviceList.Instance.FirstOrDefault(d => d.Name == deviceName);
        if (device is null)
        {
            Error?.Invoke("Capture device not found.");
            return;
        }

        try
        {
            device.Open(new DeviceConfiguration
            {
                Mode = DeviceModes.Promiscuous,
                ReadTimeout = 1000
            });
            device.Filter = CaptureFilter;
            device.OnPacketArrival += OnPacketArrival;
            device.StartCapture();

            _device = device;
            _capturing = true;
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Could not start capture: {ex.Message}. Capturing may require running as administrator.");
            TryClose(device);
        }
    }

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        try
        {
            var data = e.Data.ToArray();
            var neighbor = ParseFrame(data);
            if (neighbor is not null)
            {
                NeighborFound?.Invoke(neighbor);
            }
        }
        catch
        {
            // Ignore malformed frames.
        }
    }

    private static NeighborInfo? ParseFrame(byte[] data)
    {
        if (data.Length < 15)
        {
            return null;
        }

        var etherType = (data[12] << 8) | data[13];

        if (etherType == 0x88CC)
        {
            var payload = data[14..];
            return LldpParser.Parse(payload);
        }

        // CDP: multicast 01:00:0C:CC:CC:CC, 802.3 LLC/SNAP encapsulated.
        var isCdpDest = data[0] == 0x01 && data[1] == 0x00 && data[2] == 0x0C &&
                        data[3] == 0xCC && data[4] == 0xCC && data[5] == 0xCC;
        if (isCdpDest && data.Length >= 22)
        {
            // LLC (AA AA 03) + SNAP (OUI 00 00 0C + PID 20 00) -> CDP at offset 22.
            var snapOk = data[14] == 0xAA && data[15] == 0xAA && data[16] == 0x03 &&
                         data[17] == 0x00 && data[18] == 0x00 && data[19] == 0x0C &&
                         data[20] == 0x20 && data[21] == 0x00;
            if (snapOk)
            {
                return CdpParser.Parse(data[22..]);
            }
        }

        return null;
    }

    public void Stop()
    {
        if (_device is null)
        {
            return;
        }

        try
        {
            _device.OnPacketArrival -= OnPacketArrival;
            if (_capturing)
            {
                _device.StopCapture();
            }
        }
        catch
        {
            // Ignore.
        }
        finally
        {
            TryClose(_device);
            _device = null;
            _capturing = false;
        }
    }

    private static void TryClose(ILiveDevice device)
    {
        try
        {
            device.Close();
        }
        catch
        {
            // Ignore.
        }
    }

    public void Dispose() => Stop();
}
