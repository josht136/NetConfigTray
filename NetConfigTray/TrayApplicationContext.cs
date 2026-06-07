using NetConfigTray.Forms;
using NetConfigTray.Helpers;
using NetConfigTray.Models;
using NetConfigTray.Services;

namespace NetConfigTray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly NetworkInfoService _networkInfoService;
    private readonly ThroughputMonitorService _throughputMonitorService;
    private readonly ToolStripMenuItem _autostartMenuItem;
    private readonly Form _hostForm;
    private readonly System.Windows.Forms.Timer _trayRefreshTimer;
    private InterfacePopupForm? _popupForm;
    private bool _isExiting;
    private Icon? _currentTrayIcon;

    public TrayApplicationContext()
    {
        _networkInfoService = new NetworkInfoService();
        _throughputMonitorService = new ThroughputMonitorService();

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

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(new ToolStripMenuItem("Open", null, (_, _) => ShowPopup()));
        contextMenu.Items.Add(_autostartMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => Exit()));

        _currentTrayIcon = AppIconHelper.CreateTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentTrayIcon,
            Text = "NetConfigTray — Network IP & DHCP status",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.MouseClick += OnNotifyIconMouseClick;

        _trayRefreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _trayRefreshTimer.Tick += (_, _) => RefreshTrayIcon();
        _trayRefreshTimer.Start();
        RefreshTrayIcon();
    }

    private void RefreshTrayIcon()
    {
        if (_isExiting)
        {
            return;
        }

        try
        {
            var primary = _networkInfoService.GetPrimaryInterface();
            var configType = primary?.ConfigurationType;
            var newIcon = AppIconHelper.CreateTrayIcon(configType);

            _notifyIcon.Icon = newIcon;
            _currentTrayIcon?.Dispose();
            _currentTrayIcon = newIcon;

            if (primary is not null)
            {
                _notifyIcon.Text = $"{primary.Name}: {primary.IPv4Address} ({primary.ConfigurationLabel})";
            }
            else
            {
                _notifyIcon.Text = "NetConfigTray — No active interface";
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
            ShowPopup();
        }
    }

    private void ShowPopup()
    {
        if (_isExiting)
        {
            return;
        }

        EnsurePopupForm();

        if (!_popupForm!.ShowNearTray())
        {
            RecreatePopupForm();
            _popupForm.ShowNearTray();
        }
    }

    private void EnsurePopupForm()
    {
        if (_popupForm is null || _popupForm.IsDisposed)
        {
            RecreatePopupForm();
        }
    }

    private void RecreatePopupForm()
    {
        if (_popupForm is { IsDisposed: false })
        {
            _popupForm.Dispose();
        }

        _popupForm = new InterfacePopupForm(_networkInfoService, _throughputMonitorService);
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

        if (_popupForm is { IsDisposed: false })
        {
            _popupForm.Dispose();
        }

        _popupForm = null;
        _currentTrayIcon?.Dispose();
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

            if (_popupForm is { IsDisposed: false })
            {
                _popupForm.Dispose();
            }

            if (!_hostForm.IsDisposed)
            {
                _hostForm.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
