using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EzPrintScreen.Core;

public class AppSettings
{
    public bool CtrlModifier { get; set; } = false;
    public bool AltModifier { get; set; } = false;
    public bool ShiftModifier { get; set; } = false;
    public bool WinModifier { get; set; } = false;
    public int HotkeyVirtualKey { get; set; } = 0x2C; // VK_SNAPSHOT (PrintScreen)

    [JsonIgnore]
    public System.Windows.Forms.Keys HotkeyKey
    {
        get => (System.Windows.Forms.Keys)HotkeyVirtualKey;
        set => HotkeyVirtualKey = (int)value;
    }

    public string SaveDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop));

    public string SaveFormat { get; set; } = "png";

    public int LineWidth { get; set; } = 3;
    public string DrawColorHex { get; set; } = "#FF0000";

    [JsonIgnore]
    public Color DrawColor
    {
        get => ColorTranslator.FromHtml(DrawColorHex);
        set => DrawColorHex = ColorTranslator.ToHtml(value);
    }

    public int MarkerWidth { get; set; } = 24;
    public string MarkerColorHex { get; set; } = "#FFFF00";

    [JsonIgnore]
    public Color MarkerColor
    {
        get => ColorTranslator.FromHtml(MarkerColorHex);
        set => MarkerColorHex = ColorTranslator.ToHtml(value);
    }

    public string TextColorHex { get; set; } = "#FF0000";

    [JsonIgnore]
    public Color TextColor
    {
        get => ColorTranslator.FromHtml(TextColorHex);
        set => TextColorHex = ColorTranslator.ToHtml(value);
    }

    public int TextFontSize { get; set; } = 16;
    public bool ShowCursorInCapture { get; set; } = false;
    public int OverlayOpacity { get; set; } = 150;
    public bool PlaySoundOnCapture { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EzPrintScreen", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch { }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    public string GetHotkeyDisplayString()
    {
        return NativeMethods.GetHotkeyDisplayString(
            CtrlModifier, AltModifier, ShiftModifier, WinModifier, HotkeyKey);
    }
}