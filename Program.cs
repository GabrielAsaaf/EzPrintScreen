using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EzPrintScreen.Core;
using EzPrintScreen.UI;
using Microsoft.Win32;

namespace EzPrintScreen;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length > 0 && args[0] == "--settings")
        {
            var settings = AppSettings.Load();
            using var form = new SettingsForm(settings, () => { });
            Application.Run(form);
            return;
        }

        Application.Run(new AppContext());
    }
}

internal class AppContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly MessageWindow _msgWindow;
    private readonly HotkeyManager _hotkeyManager;
    private readonly TrayIconManager _trayIcon;
    private bool _captureActive;

    public AppContext()
    {
        _settings = AppSettings.Load();
        _msgWindow = new MessageWindow();
        _hotkeyManager = new HotkeyManager(_msgWindow.Handle, _settings);

        _trayIcon = new TrayIconManager(
            _settings,
            OnCaptureRequested,
            OnSettingsRequested,
            OnExitRequested);

        if (!_hotkeyManager.Register())
        {
            MessageBox.Show(
                $"Failed to register hotkey: {_settings.GetHotkeyDisplayString()}\n" +
                "It may be in use by another application.\n\n" +
                "You can change the hotkey via the tray icon → Settings.",
                "EzPrintScreen - Hotkey Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        _msgWindow.HotkeyReceived += () =>
        {
            if (_captureActive) return;
            OnCaptureRequested();
        };
    }

    private void OnCaptureRequested()
    {
        if (_captureActive) return;
        _captureActive = true;

        var timer = new Timer { Interval = 120 };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            timer.Dispose();
            OpenCaptureForm();
        };
        timer.Start();
    }

    private void OpenCaptureForm()
    {
        try
        {
            var captureForm = new CaptureForm(_settings);
            captureForm.FormClosed += (_, _) => { _captureActive = false; };
            captureForm.Show();
        }
        catch
        {
            _captureActive = false;
        }
    }

    private void OnSettingsRequested()
    {
        var settingsForm = new SettingsForm(_settings, OnHotkeyChanged);
        settingsForm.ShowInTaskbar = true;
        settingsForm.Show();
    }

    private void OnHotkeyChanged()
    {
        _hotkeyManager.Register();
        _trayIcon.UpdateHotkeyDisplay();
        StartupManager.Apply(_settings);
    }

    private void OnExitRequested()
    {
        _trayIcon.Dispose();
        _hotkeyManager.Dispose();
        _msgWindow.DestroyHandle();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
            _hotkeyManager.Dispose();
            _msgWindow.DestroyHandle();
        }
        base.Dispose(disposing);
    }
}

internal class MessageWindow : NativeWindow
{
    public event Action? HotkeyReceived;

    public MessageWindow()
    {
        CreateHandle(new CreateParams
        {
            ExStyle = 0x00000008,
            Parent = new IntPtr(-3)
        });
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            HotkeyReceived?.Invoke();
        }
        base.WndProc(ref m);
    }
}

internal static class StartupManager
{
    private const string AppName = "EzPrintScreen";
    private static readonly string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Apply(AppSettings settings)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            if (key == null) return;

            if (settings.StartWithWindows)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch { }
    }
}