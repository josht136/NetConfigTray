using NetConfigTray.Forms;
using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly NetworkInfoService _networkInfoService;
    private readonly ToolStripMenuItem _autostartMenuItem;
    private InterfacePopupForm? _popupForm;
    private bool _isExiting;

    public TrayApplicationContext()
    {
        _networkInfoService = new NetworkInfoService();
        _popupForm = new InterfacePopupForm(_networkInfoService);

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

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIconHelper.CreateTrayIcon(),
            Text = "NetConfigTray — Network IP & DHCP status",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
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

        if (_popupForm is null || _popupForm.IsDisposed)
        {
            _popupForm = new InterfacePopupForm(_networkInfoService);
        }

        _popupForm.ShowNearTray();
    }

    private void Exit()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        if (_popupForm is { IsDisposed: false })
        {
            _popupForm.Dispose();
        }

        _popupForm = null;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isExiting)
        {
            _notifyIcon.Dispose();

            if (_popupForm is { IsDisposed: false })
            {
                _popupForm.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
