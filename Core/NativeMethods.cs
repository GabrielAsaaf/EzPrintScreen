using System;
using System.Runtime.InteropServices;

namespace EzPrintScreen.Core;

internal static class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;
    public const int MOD_ALT = 0x0001;
    public const int MOD_CONTROL = 0x0002;
    public const int MOD_SHIFT = 0x0004;
    public const int MOD_WIN = 0x0008;
    public const int MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    public const int LOGPIXELSX = 88;
    public const int LOGPIXELSY = 90;

    public static uint VkFromKeys(bool ctrl, bool alt, bool shift, bool win, System.Windows.Forms.Keys key)
    {
        uint mod = 0;
        if (ctrl) mod |= MOD_CONTROL;
        if (alt) mod |= MOD_ALT;
        if (shift) mod |= MOD_SHIFT;
        if (win) mod |= MOD_WIN;
        mod |= MOD_NOREPEAT;
        return (uint)key & 0xFFFF;
    }

    public static uint GetModifiers(bool ctrl, bool alt, bool shift, bool win)
    {
        uint mod = MOD_NOREPEAT;
        if (ctrl) mod |= MOD_CONTROL;
        if (alt) mod |= MOD_ALT;
        if (shift) mod |= MOD_SHIFT;
        if (win) mod |= MOD_WIN;
        return mod;
    }

    public static string GetHotkeyDisplayString(bool ctrl, bool alt, bool shift, bool win, System.Windows.Forms.Keys key)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (win) parts.Add("Win");
        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");

        var keyStr = key.ToString();
        if (key == System.Windows.Forms.Keys.PrintScreen) keyStr = "PrintScreen";
        else if (key == System.Windows.Forms.Keys.Space) keyStr = "Space";
        else if (key == System.Windows.Forms.Keys.Oemtilde) keyStr = "~";
        else if (key == System.Windows.Forms.Keys.OemMinus) keyStr = "-";
        else if (key == System.Windows.Forms.Keys.Oemplus) keyStr = "+";
        else if (key == System.Windows.Forms.Keys.OemOpenBrackets) keyStr = "[";
        else if (key == System.Windows.Forms.Keys.OemCloseBrackets) keyStr = "]";
        else if (key == System.Windows.Forms.Keys.OemSemicolon) keyStr = ";";
        else if (key == System.Windows.Forms.Keys.OemQuotes) keyStr = "'";
        else if (key == System.Windows.Forms.Keys.Oemcomma) keyStr = ",";
        else if (key == System.Windows.Forms.Keys.OemPeriod) keyStr = ".";
        else if (key == System.Windows.Forms.Keys.OemQuestion) keyStr = "/";
        else if (key == System.Windows.Forms.Keys.OemPipe) keyStr = "\\";
        else if (key == System.Windows.Forms.Keys.Return) keyStr = "Enter";
        else if (key == System.Windows.Forms.Keys.Escape) keyStr = "Esc";
        else if (key == System.Windows.Forms.Keys.Tab) keyStr = "Tab";
        else if (key == System.Windows.Forms.Keys.Back) keyStr = "Backspace";
        else if (key == System.Windows.Forms.Keys.Delete) keyStr = "Del";
        else if (key == System.Windows.Forms.Keys.Insert) keyStr = "Ins";
        else if (key == System.Windows.Forms.Keys.Home) keyStr = "Home";
        else if (key == System.Windows.Forms.Keys.End) keyStr = "End";
        else if (key == System.Windows.Forms.Keys.PageUp) keyStr = "PgUp";
        else if (key == System.Windows.Forms.Keys.PageDown) keyStr = "PgDn";
        else if (keyStr.Length == 1) keyStr = keyStr.ToUpper();

        parts.Add(keyStr);
        return string.Join(" + ", parts);
    }
}