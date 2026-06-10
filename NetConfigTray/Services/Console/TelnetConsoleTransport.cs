using System.Net.Sockets;
using System.Text;

namespace NetConfigTray.Services.Console;

public sealed class TelnetConsoleSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 23;
}

/// <summary>
/// Raw Telnet transport with minimal IAC option negotiation (refuses all options so the remote
/// stops waiting), suitable for switch/router consoles.
/// </summary>
public sealed class TelnetConsoleTransport : IConsoleTransport
{
    private const byte Iac = 255;
    private const byte Dont = 254;
    private const byte Do = 253;
    private const byte Wont = 252;
    private const byte Will = 251;
    private const byte Sb = 250;
    private const byte Se = 240;

    private readonly TelnetConsoleSettings _settings;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _readCts;

    public TelnetConsoleTransport(TelnetConsoleSettings settings)
    {
        _settings = settings;
    }

    public event Action<string>? DataReceived;
    public event Action<string>? Disconnected;

    public bool IsConnected => _client?.Connected == true;

    public string Description => $"telnet {_settings.Host}:{_settings.Port}";

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, cancellationToken);
        _client = client;
        _stream = client.GetStream();

        _readCts = new CancellationTokenSource();
        _ = Task.Run(() => ReadLoop(_stream, _readCts.Token));
    }

    private async Task ReadLoop(NetworkStream stream, CancellationToken token)
    {
        var buffer = new byte[4096];
        var text = new StringBuilder();
        try
        {
            while (!token.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                if (read <= 0)
                {
                    break;
                }

                text.Clear();
                for (var i = 0; i < read; i++)
                {
                    if (buffer[i] == Iac && i + 1 < read)
                    {
                        i = HandleIac(buffer, i, read);
                        continue;
                    }

                    text.Append((char)buffer[i]);
                }

                if (text.Length > 0)
                {
                    DataReceived?.Invoke(text.ToString());
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Disconnected?.Invoke($"Telnet read ended: {ex.Message}");
            }
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                Disconnected?.Invoke("Telnet connection closed.");
            }
        }
    }

    /// <summary>Handles an IAC sequence starting at <paramref name="index"/>; returns the new index.</summary>
    private int HandleIac(byte[] buffer, int index, int length)
    {
        var command = buffer[index + 1];

        if (command is Do or Dont or Will or Wont)
        {
            if (index + 2 >= length)
            {
                return index + 1;
            }

            var option = buffer[index + 2];
            // Refuse everything: DO -> WONT, WILL -> DONT.
            var response = command switch
            {
                Do => Wont,
                Will => Dont,
                _ => (byte)0
            };

            if (response != 0)
            {
                SendRaw(new[] { Iac, response, option });
            }

            return index + 2;
        }

        if (command == Sb)
        {
            // Skip sub-negotiation until IAC SE.
            var i = index + 2;
            while (i + 1 < length && !(buffer[i] == Iac && buffer[i + 1] == Se))
            {
                i++;
            }

            return i + 1;
        }

        return index + 1;
    }

    private void SendRaw(byte[] data)
    {
        try
        {
            _stream?.Write(data, 0, data.Length);
        }
        catch
        {
            // Ignore negotiation write failures.
        }
    }

    public void Send(string text)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            _stream?.Write(bytes, 0, bytes.Length);
            _stream?.Flush();
        }
        catch (Exception ex)
        {
            Disconnected?.Invoke($"Telnet write failed: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        try
        {
            _readCts?.Cancel();
            _stream?.Dispose();
            _client?.Close();
        }
        catch
        {
            // Ignore.
        }
    }

    public void Dispose()
    {
        Disconnect();
        _stream = null;
        _client?.Dispose();
        _client = null;
        _readCts?.Dispose();
        _readCts = null;
    }
}
