using System;
using System.Windows.Forms;

namespace EzPrintScreen.Core;

public class HotkeyManager : IDisposable
{
    private const int HOTKEY_ID = 1;
    private readonly IntPtr _hwnd;
    private bool _registered;
    private readonly AppSettings _settings;

    public event Action? HotkeyPressed;

    public HotkeyManager(IntPtr hwnd, AppSettings settings)
    {
        _hwnd = hwnd;
        _settings = settings;
    }

    public bool Register()
    {
        Unregister();

        var modifiers = NativeMethods.GetModifiers(
            _settings.CtrlModifier,
            _settings.AltModifier,
            _settings.ShiftModifier,
            _settings.WinModifier);

        var vk = (uint)_settings.HotkeyVirtualKey;

        _registered = NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID, modifiers, vk);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
    }

    public void ProcessWndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
        }
    }

    public void Dispose()
    {
        Unregister();
    }
}