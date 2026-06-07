using System.Text;
using NetConfigTray.Helpers;
using NetConfigTray.Services;
using NetConfigTray.Services.Console;

namespace NetConfigTray.Forms;

/// <summary>
/// Equipment console window: connects to a switch/router over serial, SSH, or Telnet and shows a
/// live terminal. Supports optional session logging to a file for field documentation.
/// </summary>
public sealed class ConsoleTerminalForm : Form
{
    private readonly AppServices _services;
    private readonly ConsoleView _view;
    private readonly Label _statusLabel;
    private readonly Button _connectButton;
    private readonly Button _logButton;

    private IConsoleTransport? _transport;
    private StreamWriter? _logWriter;
    private string? _logPath;

    public ConsoleTerminalForm(AppServices services)
    {
        _services = services;

        Text = $"{AppBranding.ShortName} — Console";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(840, 540);
        MinimumSize = new Size(520, 320);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);

        _view = new ConsoleView { Dock = DockStyle.Fill };
        _view.UserInput += OnUserInput;

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = AppTheme.Surface };

        _connectButton = new Button { Text = "Connect…", Size = new Size(110, 30), Location = new Point(12, 9) };
        AppTheme.StyleAccentButton(_connectButton);
        _connectButton.Click += (_, _) => OnConnectClicked();

        _logButton = new Button { Text = "Start log", Size = new Size(110, 30), Location = new Point(130, 9) };
        AppTheme.StyleGhostButton(_logButton);
        _logButton.Click += (_, _) => ToggleLogging();

        var clearButton = new Button { Text = "Clear", Size = new Size(80, 30), Location = new Point(248, 9) };
        AppTheme.StyleGhostButton(clearButton);
        clearButton.Click += (_, _) => _view.Clear();

        toolbar.Controls.Add(_connectButton);
        toolbar.Controls.Add(_logButton);
        toolbar.Controls.Add(clearButton);

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = AppTheme.Surface };
        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Padding = new Padding(12, 0, 0, 0),
            Text = "Not connected. Click Connect… to begin."
        };
        statusPanel.Controls.Add(_statusLabel);

        Controls.Add(_view);
        Controls.Add(statusPanel);
        Controls.Add(toolbar);

        FormClosing += (_, _) => Cleanup();
    }

    private async void OnConnectClicked()
    {
        if (_transport is { IsConnected: true })
        {
            Disconnect();
            return;
        }

        using var dialog = new ConsoleConnectionForm(_services.Settings);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Transport is null)
        {
            return;
        }

        var transport = dialog.Transport;
        _transport = transport;
        transport.DataReceived += OnDataReceived;
        transport.Disconnected += OnTransportDisconnected;

        _statusLabel.Text = $"Connecting to {transport.Description}…";
        try
        {
            await transport.ConnectAsync(CancellationToken.None);
            _connectButton.Text = "Disconnect";
            _statusLabel.Text = $"Connected · {transport.Description}";
            Text = $"{AppBranding.ShortName} — {transport.Description}";
            _view.Focus();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Connection failed: {ex.Message}";
            MessageBox.Show(this, ex.Message, AppBranding.ShortName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            DisposeTransport();
        }
    }

    private void OnUserInput(string text)
    {
        _transport?.Send(text);
    }

    private void OnDataReceived(string text)
    {
        RunOnUi(() =>
        {
            _view.AppendOutput(text);
            WriteLog(text);
        });
    }

    private void OnTransportDisconnected(string reason)
    {
        RunOnUi(() =>
        {
            _statusLabel.Text = reason;
            _connectButton.Text = "Connect…";
        });
    }

    private void Disconnect()
    {
        DisposeTransport();
        _connectButton.Text = "Connect…";
        _statusLabel.Text = "Disconnected.";
    }

    private void DisposeTransport()
    {
        if (_transport is null)
        {
            return;
        }

        _transport.DataReceived -= OnDataReceived;
        _transport.Disconnected -= OnTransportDisconnected;
        _transport.Dispose();
        _transport = null;
    }

    private void ToggleLogging()
    {
        if (_logWriter is not null)
        {
            StopLogging();
            return;
        }

        using var save = new SaveFileDialog
        {
            Title = "Save console log",
            Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"console-{DateTime.Now:yyyyMMdd-HHmmss}.log"
        };

        if (save.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _logPath = save.FileName;
            _logWriter = new StreamWriter(_logPath, append: true, Encoding.UTF8) { AutoFlush = true };
            _logWriter.WriteLine($"--- TNT console log started {DateTime.Now:u} ---");
            _logButton.Text = "Stop log";
            _statusLabel.Text = $"Logging to {Path.GetFileName(_logPath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open log file: {ex.Message}", AppBranding.ShortName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopLogging()
    {
        try
        {
            _logWriter?.WriteLine($"--- TNT console log stopped {DateTime.Now:u} ---");
            _logWriter?.Dispose();
        }
        catch
        {
            // Ignore.
        }

        _logWriter = null;
        _logButton.Text = "Start log";
        _statusLabel.Text = $"Stopped logging ({Path.GetFileName(_logPath)}).";
    }

    private void WriteLog(string text)
    {
        try
        {
            _logWriter?.Write(text);
        }
        catch
        {
            // Ignore log write failures.
        }
    }

    private void Cleanup()
    {
        DisposeTransport();
        StopLogging();
    }

    private void RunOnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // Closing.
            }

            return;
        }

        action();
    }
}
