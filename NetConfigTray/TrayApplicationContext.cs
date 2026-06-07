using NetConfigTray.Forms;
using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly NetworkInfoService _networkInfoService;
    private readonly ToolStripMenuItem _autostartMenuItem;
    private readonly Form _hostForm;
    private InterfacePopupForm? _popupForm;
    private bool _isExiting;

    public TrayApplicationContext()
    {
        _networkInfoService = new NetworkInfoService();

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

        _popupForm = new InterfacePopupForm(_networkInfoService);
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
        _hostForm.Close();
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

            if (!_hostForm.IsDisposed)
            {
                _hostForm.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
