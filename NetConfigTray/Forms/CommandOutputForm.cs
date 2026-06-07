using NetConfigTray.Helpers;

namespace NetConfigTray.Forms;

/// <summary>
/// A themed window that runs a console command and streams its output live.
/// Used for traceroute, continuous ping, ipconfig /all, etc.
/// </summary>
public sealed class CommandOutputForm : Form
{
    private readonly TextBox _output;
    private readonly Button _actionButton;
    private readonly Label _statusLabel;
    private readonly string _fileName;
    private readonly string _arguments;
    private CancellationTokenSource? _cts;
    private bool _running;

    public CommandOutputForm(string title, string fileName, string arguments)
    {
        _fileName = fileName;
        _arguments = arguments;

        Text = $"{AppBranding.ShortName} — {title}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(720, 460);
        MinimumSize = new Size(420, 280);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44
        };
        AppTheme.StyleHeaderPanel(header);

        var titleLabel = new Label
        {
            Text = $"{_fileName} {_arguments}".Trim(),
            Font = AppTheme.FontSection,
            ForeColor = AppTheme.TextPrimary,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            Padding = new Padding(4, 0, 0, 0)
        };
        header.Controls.Add(titleLabel);

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = AppTheme.Surface
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Padding = new Padding(14, 0, 0, 0),
            BackColor = Color.Transparent
        };

        _actionButton = new Button
        {
            Text = "Stop",
            Size = new Size(96, 30),
            Dock = DockStyle.Right,
            Margin = new Padding(0)
        };
        AppTheme.StyleAccentButton(_actionButton);
        _actionButton.Click += (_, _) => OnActionClicked();

        var actionHost = new Panel { Dock = DockStyle.Right, Width = 116, Padding = new Padding(8) };
        actionHost.Controls.Add(_actionButton);

        footer.Controls.Add(_statusLabel);
        footer.Controls.Add(actionHost);

        _output = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BackColor = AppTheme.AppBackground,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.ValueFont,
            BorderStyle = BorderStyle.None
        };

        Controls.Add(_output);
        Controls.Add(footer);
        Controls.Add(header);

        Shown += (_, _) => Start();
        FormClosing += (_, _) => _cts?.Cancel();
    }

    private void Start()
    {
        _cts = new CancellationTokenSource();
        _running = true;
        _statusLabel.Text = "Running…";
        var token = _cts.Token;

        Task.Run(() =>
        {
            var exitCode = CommandRunner.RunStreaming(_fileName, _arguments, AppendLine, token);
            RunOnUi(() =>
            {
                _running = false;
                _actionButton.Text = "Close";
                _statusLabel.Text = token.IsCancellationRequested
                    ? "Stopped."
                    : $"Finished (exit code {exitCode}).";
            });
        });
    }

    private void OnActionClicked()
    {
        if (_running)
        {
            _cts?.Cancel();
            return;
        }

        Close();
    }

    private void AppendLine(string line)
    {
        RunOnUi(() =>
        {
            _output.AppendText(line + Environment.NewLine);
        });
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
                // Window closing.
            }

            return;
        }

        action();
    }
}
