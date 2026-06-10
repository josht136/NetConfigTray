using System.IO.Ports;
using System.Text;

namespace NetConfigTray.Services.Console;

/// <summary>Settings for a serial console connection (defaults to 9600 8N1, no flow control).</summary>
public sealed class SerialConsoleSettings
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    public Handshake Handshake { get; set; } = Handshake.None;

    public string Summary => $"{PortName} @ {BaudRate} {DataBits}{ParityLetter}{StopBitsDigit}";

    private char ParityLetter => Parity switch
    {
        Parity.None => 'N',
        Parity.Even => 'E',
        Parity.Odd => 'O',
        Parity.Mark => 'M',
        Parity.Space => 'S',
        _ => '?'
    };

    private string StopBitsDigit => StopBits switch
    {
        StopBits.One => "1",
        StopBits.OnePointFive => "1.5",
        StopBits.Two => "2",
        _ => "?"
    };
}

public sealed class SerialConsoleTransport : IConsoleTransport
{
    private readonly SerialConsoleSettings _settings;
    private SerialPort? _port;

    public SerialConsoleTransport(SerialConsoleSettings settings)
    {
        _settings = settings;
    }

    public event Action<string>? DataReceived;
    public event Action<string>? Disconnected;

    public bool IsConnected => _port?.IsOpen == true;

    public string Description => $"serial {_settings.Summary}";

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        var port = new SerialPort(_settings.PortName, _settings.BaudRate, _settings.Parity, _settings.DataBits, _settings.StopBits)
        {
            Handshake = _settings.Handshake,
            Encoding = Encoding.UTF8,
            ReadTimeout = 500,
            WriteTimeout = 1000,
            DtrEnable = true,
            RtsEnable = _settings.Handshake != Handshake.RequestToSend
        };

        port.DataReceived += OnSerialDataReceived;
        port.ErrorReceived += (_, e) => Disconnected?.Invoke($"Serial error: {e.EventType}");
        port.Open();
        _port = port;
        return Task.CompletedTask;
    }

    private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var port = _port;
            if (port is null || !port.IsOpen)
            {
                return;
            }

            var text = port.ReadExisting();
            if (!string.IsNullOrEmpty(text))
            {
                DataReceived?.Invoke(text);
            }
        }
        catch (Exception ex)
        {
            Disconnected?.Invoke($"Serial read failed: {ex.Message}");
        }
    }

    public void Send(string text)
    {
        try
        {
            _port?.Write(text);
        }
        catch (Exception ex)
        {
            Disconnected?.Invoke($"Serial write failed: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        try
        {
            if (_port is not null)
            {
                _port.DataReceived -= OnSerialDataReceived;
                if (_port.IsOpen)
                {
                    _port.Close();
                }
            }
        }
        catch
        {
            // Ignore close failures.
        }
    }

    public void Dispose()
    {
        Disconnect();
        _port?.Dispose();
        _port = null;
    }

    public static string[] GetAvailablePortNames()
    {
        try
        {
            return SerialPort.GetPortNames().Distinct().OrderBy(n => n).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
