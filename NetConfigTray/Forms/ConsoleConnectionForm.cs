using System.ComponentModel;
using System.IO.Ports;
using NetConfigTray.Helpers;
using NetConfigTray.Models;
using NetConfigTray.Services;
using NetConfigTray.Services.Console;

namespace NetConfigTray.Forms;

/// <summary>
/// Modal dialog to configure and build a console connection (serial / SSH / Telnet), with optional
/// load/save of named session profiles via <see cref="SettingsStore"/>.
/// </summary>
public sealed class ConsoleConnectionForm : Form
{
    private const string SessionsKey = "console-sessions";

    private readonly SettingsStore _settings;
    private ConsoleSessionList _sessions;

    private readonly ComboBox _savedCombo = new();
    private readonly ComboBox _kindCombo = new();

    private readonly ComboBox _serialPort = new();
    private readonly ComboBox _serialBaud = new();
    private readonly ComboBox _serialDataBits = new();
    private readonly ComboBox _serialParity = new();
    private readonly ComboBox _serialStopBits = new();

    private readonly TextBox _host = new();
    private readonly TextBox _port = new();
    private readonly TextBox _username = new();
    private readonly TextBox _password = new();

    private readonly Panel _serialPanel = new();
    private readonly Panel _networkPanel = new();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IConsoleTransport? Transport { get; private set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ConnectionTitle { get; private set; } = "Console";

    public ConsoleConnectionForm(SettingsStore settings)
    {
        _settings = settings;
        _sessions = _settings.Load<ConsoleSessionList>(SessionsKey);

        Text = $"{AppBranding.ShortName} — New console connection";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 430);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);

        BuildUi();
        LoadSavedList();
        UpdateVisibility();
    }

    private void BuildUi()
    {
        var y = 16;

        AddLabel("Saved session", 16, y);
        _savedCombo.SetBounds(150, y - 3, 200, 24);
        _savedCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _savedCombo.SelectedIndexChanged += (_, _) => ApplySelectedSavedSession();
        Controls.Add(_savedCombo);
        y += 36;

        AddLabel("Connection type", 16, y);
        _kindCombo.SetBounds(150, y - 3, 200, 24);
        _kindCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _kindCombo.Items.AddRange(new object[] { "Serial", "SSH", "Telnet" });
        _kindCombo.SelectedIndex = 0;
        _kindCombo.SelectedIndexChanged += (_, _) => UpdateVisibility();
        Controls.Add(_kindCombo);
        y += 44;

        BuildSerialPanel(y);
        BuildNetworkPanel(y);

        var connect = new Button { Text = "Connect", Size = new Size(110, 32), Location = new Point(238, 384) };
        AppTheme.StyleAccentButton(connect);
        connect.Click += (_, _) => OnConnect();

        var save = new Button { Text = "Save session", Size = new Size(110, 32), Location = new Point(120, 384) };
        AppTheme.StyleGhostButton(save);
        save.Click += (_, _) => OnSaveSession();

        var cancel = new Button { Text = "Cancel", Size = new Size(90, 32), Location = new Point(16, 384) };
        AppTheme.StyleGhostButton(cancel);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(connect);
        Controls.Add(save);
        Controls.Add(cancel);
    }

    private void BuildSerialPanel(int top)
    {
        _serialPanel.SetBounds(0, top, ClientSize.Width, 200);
        _serialPanel.BackColor = Color.Transparent;

        var y = 0;
        AddLabel("COM port", 16, y, _serialPanel);
        _serialPort.SetBounds(150, y - 3, 140, 24);
        _serialPort.DropDownStyle = ComboBoxStyle.DropDownList;
        var refresh = new Button { Text = "↻", Size = new Size(40, 24), Location = new Point(296, y - 3) };
        AppTheme.StyleGhostButton(refresh);
        refresh.Click += (_, _) => RefreshSerialPorts();
        _serialPanel.Controls.Add(_serialPort);
        _serialPanel.Controls.Add(refresh);
        y += 36;

        AddLabel("Baud rate", 16, y, _serialPanel);
        _serialBaud.SetBounds(150, y - 3, 140, 24);
        _serialBaud.DropDownStyle = ComboBoxStyle.DropDownList;
        _serialBaud.Items.AddRange(new object[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 });
        _serialBaud.SelectedItem = 9600;
        _serialPanel.Controls.Add(_serialBaud);
        y += 36;

        AddLabel("Data bits", 16, y, _serialPanel);
        _serialDataBits.SetBounds(150, y - 3, 140, 24);
        _serialDataBits.DropDownStyle = ComboBoxStyle.DropDownList;
        _serialDataBits.Items.AddRange(new object[] { 5, 6, 7, 8 });
        _serialDataBits.SelectedItem = 8;
        _serialPanel.Controls.Add(_serialDataBits);
        y += 36;

        AddLabel("Parity", 16, y, _serialPanel);
        _serialParity.SetBounds(150, y - 3, 140, 24);
        _serialParity.DropDownStyle = ComboBoxStyle.DropDownList;
        _serialParity.Items.AddRange(Enum.GetNames<Parity>().Cast<object>().ToArray());
        _serialParity.SelectedItem = nameof(Parity.None);
        _serialPanel.Controls.Add(_serialParity);
        y += 36;

        AddLabel("Stop bits", 16, y, _serialPanel);
        _serialStopBits.SetBounds(150, y - 3, 140, 24);
        _serialStopBits.DropDownStyle = ComboBoxStyle.DropDownList;
        _serialStopBits.Items.AddRange(new object[] { "One", "OnePointFive", "Two" });
        _serialStopBits.SelectedItem = "One";
        _serialPanel.Controls.Add(_serialStopBits);

        Controls.Add(_serialPanel);
        RefreshSerialPorts();
    }

    private void BuildNetworkPanel(int top)
    {
        _networkPanel.SetBounds(0, top, ClientSize.Width, 200);
        _networkPanel.BackColor = Color.Transparent;
        _networkPanel.Visible = false;

        var y = 0;
        AddLabel("Host", 16, y, _networkPanel);
        _host.SetBounds(150, y - 3, 200, 24);
        StyleInput(_host);
        _networkPanel.Controls.Add(_host);
        y += 36;

        AddLabel("Port", 16, y, _networkPanel);
        _port.SetBounds(150, y - 3, 80, 24);
        _port.Text = "22";
        StyleInput(_port);
        _networkPanel.Controls.Add(_port);
        y += 36;

        AddLabel("Username", 16, y, _networkPanel);
        _username.SetBounds(150, y - 3, 200, 24);
        StyleInput(_username);
        _networkPanel.Controls.Add(_username);
        y += 36;

        AddLabel("Password", 16, y, _networkPanel);
        _password.SetBounds(150, y - 3, 200, 24);
        _password.UseSystemPasswordChar = true;
        StyleInput(_password);
        _networkPanel.Controls.Add(_password);

        Controls.Add(_networkPanel);
    }

    private ConsoleTransportKind SelectedKind => _kindCombo.SelectedIndex switch
    {
        1 => ConsoleTransportKind.Ssh,
        2 => ConsoleTransportKind.Telnet,
        _ => ConsoleTransportKind.Serial
    };

    private void UpdateVisibility()
    {
        var serial = SelectedKind == ConsoleTransportKind.Serial;
        _serialPanel.Visible = serial;
        _networkPanel.Visible = !serial;

        _username.Enabled = _password.Enabled = SelectedKind == ConsoleTransportKind.Ssh;
        if (SelectedKind == ConsoleTransportKind.Ssh && _port.Text is "23" or "")
        {
            _port.Text = "22";
        }
        else if (SelectedKind == ConsoleTransportKind.Telnet && _port.Text is "22" or "")
        {
            _port.Text = "23";
        }
    }

    private void RefreshSerialPorts()
    {
        var selected = _serialPort.SelectedItem as string;
        _serialPort.Items.Clear();
        var ports = SerialConsoleTransport.GetAvailablePortNames();
        _serialPort.Items.AddRange(ports.Cast<object>().ToArray());
        if (selected is not null && _serialPort.Items.Contains(selected))
        {
            _serialPort.SelectedItem = selected;
        }
        else if (_serialPort.Items.Count > 0)
        {
            _serialPort.SelectedIndex = 0;
        }
    }

    private void LoadSavedList()
    {
        _savedCombo.Items.Clear();
        _savedCombo.Items.Add("(new connection)");
        foreach (var session in _sessions.Sessions)
        {
            _savedCombo.Items.Add(session.Name);
        }

        _savedCombo.SelectedIndex = 0;
    }

    private void ApplySelectedSavedSession()
    {
        var index = _savedCombo.SelectedIndex - 1;
        if (index < 0 || index >= _sessions.Sessions.Count)
        {
            return;
        }

        var s = _sessions.Sessions[index];
        _kindCombo.SelectedIndex = s.Kind switch
        {
            ConsoleTransportKind.Ssh => 1,
            ConsoleTransportKind.Telnet => 2,
            _ => 0
        };

        if (s.Kind == ConsoleTransportKind.Serial)
        {
            if (!_serialPort.Items.Contains(s.PortName))
            {
                _serialPort.Items.Add(s.PortName);
            }

            _serialPort.SelectedItem = s.PortName;
            _serialBaud.SelectedItem = s.BaudRate;
            _serialDataBits.SelectedItem = s.DataBits;
            _serialParity.SelectedItem = s.Parity;
            _serialStopBits.SelectedItem = s.StopBits;
        }
        else
        {
            _host.Text = s.Host;
            _port.Text = s.Port.ToString();
            _username.Text = s.Username;
        }

        UpdateVisibility();
    }

    private ConsoleSessionProfile BuildProfile()
    {
        var profile = new ConsoleSessionProfile { Kind = SelectedKind };
        if (SelectedKind == ConsoleTransportKind.Serial)
        {
            profile.PortName = _serialPort.SelectedItem as string ?? "COM1";
            profile.BaudRate = (int)(_serialBaud.SelectedItem ?? 9600);
            profile.DataBits = (int)(_serialDataBits.SelectedItem ?? 8);
            profile.Parity = _serialParity.SelectedItem as string ?? "None";
            profile.StopBits = _serialStopBits.SelectedItem as string ?? "One";
            profile.Name = $"Serial {profile.PortName} {profile.BaudRate}";
        }
        else
        {
            profile.Host = _host.Text.Trim();
            profile.Port = int.TryParse(_port.Text, out var p) ? p : (SelectedKind == ConsoleTransportKind.Ssh ? 22 : 23);
            profile.Username = _username.Text.Trim();
            profile.Name = $"{SelectedKind} {profile.Host}";
        }

        return profile;
    }

    private void OnSaveSession()
    {
        var profile = BuildProfile();
        var name = Prompt.Show(this, "Session name", "Save console session as:", profile.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        profile.Name = name.Trim();
        var existing = _sessions.Sessions.FindIndex(s => string.Equals(s.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            _sessions.Sessions[existing] = profile;
        }
        else
        {
            _sessions.Sessions.Add(profile);
        }

        _settings.Save(SessionsKey, _sessions);
        LoadSavedList();
        _savedCombo.SelectedIndex = _savedCombo.Items.IndexOf(profile.Name);
    }

    private void OnConnect()
    {
        try
        {
            Transport = SelectedKind switch
            {
                ConsoleTransportKind.Serial => new SerialConsoleTransport(new SerialConsoleSettings
                {
                    PortName = _serialPort.SelectedItem as string ?? "COM1",
                    BaudRate = (int)(_serialBaud.SelectedItem ?? 9600),
                    DataBits = (int)(_serialDataBits.SelectedItem ?? 8),
                    Parity = Enum.Parse<Parity>(_serialParity.SelectedItem as string ?? "None"),
                    StopBits = Enum.Parse<StopBits>(_serialStopBits.SelectedItem as string ?? "One")
                }),
                ConsoleTransportKind.Ssh => new SshConsoleTransport(new SshConsoleSettings
                {
                    Host = _host.Text.Trim(),
                    Port = int.TryParse(_port.Text, out var sp) ? sp : 22,
                    Username = _username.Text.Trim(),
                    Password = _password.Text
                }),
                _ => new TelnetConsoleTransport(new TelnetConsoleSettings
                {
                    Host = _host.Text.Trim(),
                    Port = int.TryParse(_port.Text, out var tp) ? tp : 23
                })
            };

            if (SelectedKind != ConsoleTransportKind.Serial && string.IsNullOrWhiteSpace(_host.Text))
            {
                MessageBox.Show(this, "Enter a host.", AppBranding.ShortName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Transport = null;
                return;
            }

            ConnectionTitle = Transport.Description;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, AppBranding.ShortName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Label AddLabel(string text, int x, int y, Control? parent = null)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.FontBody,
            Location = new Point(x, y),
            BackColor = Color.Transparent
        };
        (parent ?? (Control)this).Controls.Add(label);
        return label;
    }

    private static void StyleInput(TextBox box)
    {
        box.BackColor = AppTheme.SurfaceRaised;
        box.ForeColor = AppTheme.TextPrimary;
        box.BorderStyle = BorderStyle.FixedSingle;
    }
}
