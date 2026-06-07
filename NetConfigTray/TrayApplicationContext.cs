using NetConfigTray.Forms;
using NetConfigTray.Helpers;
using NetConfigTray.Models;
using NetConfigTray.Services;

namespace NetConfigTray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppServices _services = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autostartMenuItem;
    private readonly ToolStripMenuItem _notificationsMenuItem;
    private readonly Form _hostForm;
    private readonly System.Windows.Forms.Timer _trayRefreshTimer;
    private InterfacePopupForm? _mainWindow;
    private bool _isExiting;
    private Icon? _currentTrayIcon;
    private IpConfigurationType? _lastTrayConfigType;

    public TrayApplicationContext()
    {
        _hostForm = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            Size = new Size(0, 0),
            Opacity = 0
        };
        MainForm = _hostForm;
        _hostForm.Show();
        _hostForm.Hide();

        _autostartMenuItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = AutostartHelper.IsEnabled()
        };
        _autostartMenuItem.CheckedChanged += (_, _) =>
            AutostartHelper.SetEnabled(_autostartMenuItem.Checked);

        if (!_autostartMenuItem.Checked)
        {
            AutostartHelper.SetEnabled(true);
            _autostartMenuItem.Checked = true;
        }

        _notificationsMenuItem = new ToolStripMenuItem("Change notifications")
        {
            CheckOnClick = true,
            Checked = true
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(new ToolStripMenuItem($"Open {AppBranding.ShortName}", null, (_, _) => ShowMainWindow()));
        contextMenu.Items.Add(_autostartMenuItem);
        contextMenu.Items.Add(_notificationsMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => Exit()));

        _currentTrayIcon = AppIconHelper.CreateTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentTrayIcon,
            Text = $"{AppBranding.ShortName} — Network status",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
        _services.Snapshot.SnapshotUpdated += UpdateTrayFromSnapshot;

        _trayRefreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _trayRefreshTimer.Tick += (_, _) =>
        {
            _services.PublicIp.RefreshAsync();
            _services.Snapshot.EnsureFresh(TimeSpan.FromSeconds(4));
        };
        _trayRefreshTimer.Start();

        _services.Snapshot.RequestRefresh(includeSlowDetails: false);
    }

    private void UpdateTrayFromSnapshot()
    {
        if (_isExiting)
        {
            return;
        }

        if (_hostForm.InvokeRequired)
        {
            try
            {
                _hostForm.BeginInvoke(UpdateTrayFromSnapshot);
            }
            catch (InvalidOperationException)
            {
                // Host form is closing.
            }

            return;
        }

        try
        {
            var interfaces = _services.Snapshot.GetSnapshot();
            var primary = _services.Snapshot.GetPrimaryInterface();

            if (_notificationsMenuItem.Checked)
            {
                var changes = _services.ChangeNotifier.DetectChanges(interfaces);
                if (changes.Count > 0)
                {
                    var message = changes.Count == 1
                        ? changes[0]
                        : string.Join(Environment.NewLine, changes.Take(3));

                    _notifyIcon.ShowBalloonTip(4000, AppBranding.ShortName, message, ToolTipIcon.Info);
                }
            }

            var configType = primary?.ConfigurationType;
            if (configType != _lastTrayConfigType || _currentTrayIcon is null)
            {
                var newIcon = AppIconHelper.CreateTrayIcon(configType);
                _notifyIcon.Icon = newIcon;
                _currentTrayIcon?.Dispose();
                _currentTrayIcon = newIcon;
                _lastTrayConfigType = configType;
            }

            if (primary is not null)
            {
                var publicIp = _services.PublicIp.GetDisplayText();
                _notifyIcon.Text = $"{AppBranding.ShortName}: {primary.Name} {primary.IPv4Address} ({primary.ConfigurationLabel}) · {publicIp}";
            }
            else
            {
                _notifyIcon.Text = $"{AppBranding.ShortName} — No active interface";
            }
        }
        catch
        {
            // Keep the previous icon if refresh fails.
        }
    }

    private void OnNotifyIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ShowMainWindow();
        }
    }

    private void ShowMainWindow()
    {
        if (_isExiting)
        {
            return;
        }

        EnsureMainWindow();
        _mainWindow!.ShowMainWindow();
    }

    private void EnsureMainWindow()
    {
        if (_mainWindow is null || _mainWindow.IsDisposed)
        {
            RecreateMainWindow();
        }
    }

    private void RecreateMainWindow()
    {
        if (_mainWindow is { IsDisposed: false })
        {
            _mainWindow.Dispose();
        }

        _mainWindow = new InterfacePopupForm(_services);
    }

    private void Exit()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _trayRefreshTimer.Stop();
        _trayRefreshTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        if (_mainWindow is { IsDisposed: false })
        {
            _mainWindow.ForceClose();
            _mainWindow.Dispose();
        }

        _mainWindow = null;
        _currentTrayIcon?.Dispose();
        _services.Dispose();
        _hostForm.Close();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isExiting)
        {
            _trayRefreshTimer.Dispose();
            _notifyIcon.Dispose();
            _currentTrayIcon?.Dispose();
            _services.Dispose();

            if (_mainWindow is { IsDisposed: false })
            {
                _mainWindow.Dispose();
            }

            if (!_hostForm.IsDisposed)
            {
                _hostForm.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
