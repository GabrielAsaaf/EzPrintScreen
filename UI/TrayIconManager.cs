using System;
using System.Drawing;
using System.Windows.Forms;
using EzPrintScreen.Core;

namespace EzPrintScreen.UI;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly AppSettings _settings;
    private readonly Action _onCapture;
    private readonly Action _onSettings;
    private readonly Action _onExit;

    public TrayIconManager(AppSettings settings, Action onCapture, Action onSettings, Action onExit)
    {
        _settings = settings;
        _onCapture = onCapture;
        _onSettings = onSettings;
        _onExit = onExit;

        _contextMenu = new ContextMenuStrip();

        var captureItem = new ToolStripMenuItem("Take Screenshot", null, (s, e) => _onCapture())
        {
            ShortcutKeyDisplayString = _settings.GetHotkeyDisplayString()
        };
        _contextMenu.Items.Add(captureItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(new ToolStripMenuItem("Settings", null, (s, e) => _onSettings()));
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => _onExit()));

        _trayIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "EzPrintScreen",
            Visible = true,
            ContextMenuStrip = _contextMenu,
        };

        _trayIcon.DoubleClick += (s, e) => _onCapture();
    }

    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var bgBrush = new SolidBrush(Color.FromArgb(0, 120, 215));
            g.FillRectangle(bgBrush, 0, 2, 14, 11);

            using var frameBrush = new SolidBrush(Color.White);
            g.FillRectangle(frameBrush, 2, 3, 10, 8);

            using var screenBrush = new SolidBrush(Color.FromArgb(0, 120, 215));
            g.FillRectangle(screenBrush, 3, 4, 8, 6);

            using var lensBrush = new SolidBrush(Color.FromArgb(0, 174, 255));
            g.FillEllipse(lensBrush, 9, 0, 7, 7);

            using var lensCenter = new SolidBrush(Color.White);
            g.FillEllipse(lensCenter, 11, 2, 3, 3);
        }

        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void UpdateHotkeyDisplay()
    {
        if (_contextMenu.Items[0] is ToolStripMenuItem item)
        {
            item.ShortcutKeyDisplayString = _settings.GetHotkeyDisplayString();
        }
        _trayIcon.Text = $"EzPrintScreen ({_settings.GetHotkeyDisplayString()})";
    }

    public void Dispose()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _contextMenu.Dispose();
    }
}