using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace NetConfigTray.Services.Console;

public sealed class SshConsoleSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TerminalColumns { get; set; } = 120;
    public int TerminalRows { get; set; } = 40;
}

public sealed class SshConsoleTransport : IConsoleTransport
{
    private readonly SshConsoleSettings _settings;
    private SshClient? _client;
    private ShellStream? _shell;
    private CancellationTokenSource? _readCts;

    public SshConsoleTransport(SshConsoleSettings settings)
    {
        _settings = settings;
    }

    public event Action<string>? DataReceived;
    public event Action<string>? Disconnected;

    public bool IsConnected => _client?.IsConnected == true;

    public string Description => $"ssh {_settings.Username}@{_settings.Host}:{_settings.Port}";

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var connectionInfo = new ConnectionInfo(
                _settings.Host,
                _settings.Port,
                _settings.Username,
                new PasswordAuthenticationMethod(_settings.Username, _settings.Password))
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            var client = new SshClient(connectionInfo);
            client.ErrorOccurred += (_, e) => Disconnected?.Invoke($"SSH error: {e.Exception.Message}");
            client.Connect();

            var shell = client.CreateShellStream(
                "vt100",
                (uint)_settings.TerminalColumns,
                (uint)_settings.TerminalRows,
                800,
                600,
                4096);

            _client = client;
            _shell = shell;

            _readCts = new CancellationTokenSource();
            _ = Task.Run(() => ReadLoop(shell, _readCts.Token));
        }, cancellationToken);
    }

    private void ReadLoop(ShellStream shell, CancellationToken token)
    {
        var buffer = new byte[4096];
        try
        {
            while (!token.IsCancellationRequested)
            {
                var read = shell.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    if (!IsConnected)
                    {
                        break;
                    }

                    Thread.Sleep(20);
                    continue;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, read);
                DataReceived?.Invoke(text);
            }
        }
        catch (ObjectDisposedException)
        {
            // Stream closed during disconnect.
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Disconnected?.Invoke($"SSH read ended: {ex.Message}");
            }
        }
    }

    public void Send(string text)
    {
        try
        {
            var shell = _shell;
            if (shell is null)
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(text);
            shell.Write(bytes, 0, bytes.Length);
            shell.Flush();
        }
        catch (Exception ex)
        {
            Disconnected?.Invoke($"SSH write failed: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        try
        {
            _readCts?.Cancel();
            _shell?.Dispose();
            if (_client is { IsConnected: true })
            {
                _client.Disconnect();
            }
        }
        catch
        {
            // Ignore disconnect failures.
        }
    }

    public void Dispose()
    {
        Disconnect();
        _shell = null;
        _client?.Dispose();
        _client = null;
        _readCts?.Dispose();
        _readCts = null;
    }
}
