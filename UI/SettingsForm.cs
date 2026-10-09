using System;
using System.Drawing;
using System.Windows.Forms;
using EzPrintScreen.Core;

namespace EzPrintScreen.UI;

public class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Action _onHotkeyChanged;
    private CheckBox _chkCtrl = null!;
    private CheckBox _chkAlt = null!;
    private CheckBox _chkShift = null!;
    private CheckBox _chkWin = null!;
    private TextBox _txtHotkey = null!;
    private Keys _recordedKey = Keys.None;
    private bool _recording;
    private Button _btnRecord = null!;
    private ComboBox _cmbFormat = null!;
    private NumericUpDown _numLineWidth = null!;
    private NumericUpDown _numMarkerWidth = null!;
    private NumericUpDown _numFontSize = null!;
    private Button _btnDrawColor = null!;
    private Button _btnMarkerColor = null!;
    private Button _btnTextColor = null!;
    private NumericUpDown _numOverlay = null!;
    private CheckBox _chkCursor = null!;
    private CheckBox _chkStartWithWindows = null!;

    public SettingsForm(AppSettings settings, Action onHotkeyChanged)
    {
        _settings = settings;
        _onHotkeyChanged = onHotkeyChanged;
        InitializeForm();
        LoadSettings();
    }

    private void InitializeForm()
    {
        Text = "EzPrintScreen - Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 556);
        BackColor = Color.FromArgb(245, 245, 245);
        Font = new Font("Segoe UI", 9f);

        var padding = 20;
        var y = padding;

        #region Hotkey Section

        var lblHotkey = new Label
        {
            Text = "Screenshot Hotkey",
            Location = new Point(padding, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold)
        };
        Controls.Add(lblHotkey);
        y += 28;

        var pnlModifiers = new FlowLayoutPanel
        {
            Location = new Point(padding, y),
            Width = 380,
            Height = 30,
            FlowDirection = FlowDirection.LeftToRight
        };

        _chkCtrl = new CheckBox { Text = "Ctrl", Width = 55, AutoSize = true };
        _chkAlt = new CheckBox { Text = "Alt", Width = 50, AutoSize = true };
        _chkShift = new CheckBox { Text = "Shift", Width = 55, AutoSize = true };
        _chkWin = new CheckBox { Text = "Win", Width = 50, AutoSize = true };

        pnlModifiers.Controls.AddRange(new Control[] { _chkCtrl, _chkAlt, _chkShift, _chkWin });
        Controls.Add(pnlModifiers);
        y += 36;

        var lblPlus = new Label { Text = "+", Location = new Point(padding + 180, y + 2), AutoSize = true, Font = new Font("Segoe UI", 11f) };
        Controls.Add(lblPlus);

        _txtHotkey = new TextBox
        {
            Location = new Point(padding + 200, y),
            Width = 100,
            Height = 26,
            ReadOnly = true,
            BackColor = Color.White,
            Text = "PrintScreen",
            Font = new Font("Segoe UI", 9f)
        };
        Controls.Add(_txtHotkey);

        _btnRecord = new Button
        {
            Text = "Record",
            Location = new Point(padding + 310, y),
            Width = 70,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        _btnRecord.Click += OnRecordClick;
        _btnRecord.KeyDown += OnRecordKeyDown;
        Controls.Add(_btnRecord);
        y += 40;

        #endregion

        var sep1 = new Panel { Location = new Point(padding, y), Width = 380, Height = 1, BackColor = Color.FromArgb(200, 200, 200) };
        Controls.Add(sep1);
        y += 15;

        #region Save Section

        var lblSave = new Label
        {
            Text = "Save Settings",
            Location = new Point(padding, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold)
        };
        Controls.Add(lblSave);
        y += 28;

        var lblFormat = new Label { Text = "Default Format:", Location = new Point(padding, y + 3), AutoSize = true };
        Controls.Add(lblFormat);
        _cmbFormat = new ComboBox
        {
            Location = new Point(padding + 110, y),
            Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFormat.Items.AddRange(new object[] { "PNG", "JPEG", "BMP", "GIF" });
        Controls.Add(_cmbFormat);
        y += 34;

        #endregion

        var sep2 = new Panel { Location = new Point(padding, y), Width = 380, Height = 1, BackColor = Color.FromArgb(200, 200, 200) };
        Controls.Add(sep2);
        y += 15;

        #region Drawing Section

        var lblDraw = new Label
        {
            Text = "Drawing Settings",
            Location = new Point(padding, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold)
        };
        Controls.Add(lblDraw);
        y += 28;

        var lblLineWidth = new Label { Text = "Line Width:", Location = new Point(padding, y + 3), AutoSize = true };
        Controls.Add(lblLineWidth);
        _numLineWidth = new NumericUpDown { Location = new Point(padding + 110, y), Width = 60, Minimum = 1, Maximum = 20, Value = 3 };
        Controls.Add(_numLineWidth);

        var lblDrawColor = new Label { Text = "Line Color:", Location = new Point(padding + 190, y + 3), AutoSize = true };
        Controls.Add(lblDrawColor);
        _btnDrawColor = new Button
        {
            Location = new Point(padding + 280, y),
            Width = 60, Height = 24,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Red,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(150, 150, 150) },
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        _btnDrawColor.Click += (s, e) => PickColor(_btnDrawColor);
        Controls.Add(_btnDrawColor);
        y += 34;

        var lblMarkerWidth = new Label { Text = "Marker Width:", Location = new Point(padding, y + 3), AutoSize = true };
        Controls.Add(lblMarkerWidth);
        _numMarkerWidth = new NumericUpDown { Location = new Point(padding + 110, y), Width = 60, Minimum = 4, Maximum = 60, Value = 24 };
        Controls.Add(_numMarkerWidth);

        var lblMarkerColor = new Label { Text = "Marker Color:", Location = new Point(padding + 190, y + 3), AutoSize = true };
        Controls.Add(lblMarkerColor);
        _btnMarkerColor = new Button
        {
            Location = new Point(padding + 280, y),
            Width = 60, Height = 24,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Yellow,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(150, 150, 150) },
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        _btnMarkerColor.Click += (s, e) => PickColor(_btnMarkerColor);
        Controls.Add(_btnMarkerColor);
        y += 34;

        var lblFontSize = new Label { Text = "Text Size:", Location = new Point(padding, y + 3), AutoSize = true };
        Controls.Add(lblFontSize);
        _numFontSize = new NumericUpDown { Location = new Point(padding + 110, y), Width = 60, Minimum = 8, Maximum = 72, Value = 16 };
        Controls.Add(_numFontSize);

        var lblTextColor = new Label { Text = "Text Color:", Location = new Point(padding + 190, y + 3), AutoSize = true };
        Controls.Add(lblTextColor);
        _btnTextColor = new Button
        {
            Location = new Point(padding + 280, y),
            Width = 60, Height = 24,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Red,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(150, 150, 150) },
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        _btnTextColor.Click += (s, e) => PickColor(_btnTextColor);
        Controls.Add(_btnTextColor);
        y += 34;

        var lblOverlay = new Label { Text = "Overlay Opacity:", Location = new Point(padding, y + 3), AutoSize = true };
        Controls.Add(lblOverlay);
        _numOverlay = new NumericUpDown { Location = new Point(padding + 110, y), Width = 60, Minimum = 50, Maximum = 230, Value = 150 };
        Controls.Add(_numOverlay);
        y += 34;

        _chkCursor = new CheckBox { Text = "Show cursor in capture", Location = new Point(padding, y), AutoSize = true };
        Controls.Add(_chkCursor);
        y += 34;

        _chkStartWithWindows = new CheckBox
        {
            Text = "Start with Windows",
            Location = new Point(padding, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        Controls.Add(_chkStartWithWindows);
        y += 34;

        #endregion

        var sep3 = new Panel { Location = new Point(padding, y), Width = 380, Height = 1, BackColor = Color.FromArgb(200, 200, 200) };
        Controls.Add(sep3);
        y += 15;

        #region Buttons

        var btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(padding + 220, y),
            Width = 80, Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(200, 200, 200),
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        btnCancel.Click += (s, e) => Close();
        Controls.Add(btnCancel);

        var btnSave = new Button
        {
            Text = "Save",
            Location = new Point(padding + 310, y),
            Width = 80, Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        btnSave.Click += OnSaveClick;
        Controls.Add(btnSave);

        #endregion

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private void LoadSettings()
    {
        _chkCtrl.Checked = _settings.CtrlModifier;
        _chkAlt.Checked = _settings.AltModifier;
        _chkShift.Checked = _settings.ShiftModifier;
        _chkWin.Checked = _settings.WinModifier;
        _txtHotkey.Text = _settings.HotkeyKey.ToString();

        _cmbFormat.SelectedItem = _settings.SaveFormat.ToUpperInvariant();
        _numLineWidth.Value = _settings.LineWidth;
        _numMarkerWidth.Value = _settings.MarkerWidth;
        _numFontSize.Value = _settings.TextFontSize;
        _btnDrawColor.BackColor = _settings.DrawColor;
        _btnMarkerColor.BackColor = _settings.MarkerColor;
        _btnTextColor.BackColor = _settings.TextColor;
        _numOverlay.Value = _settings.OverlayOpacity;
        _chkCursor.Checked = _settings.ShowCursorInCapture;
        _chkStartWithWindows.Checked = _settings.StartWithWindows;
    }

    private void OnRecordClick(object? sender, EventArgs e)
    {
        _recording = !_recording;
        _btnRecord.Text = _recording ? "..." : "Record";
        _btnRecord.BackColor = _recording ? Color.FromArgb(220, 50, 50) : Color.FromArgb(0, 120, 215);

        if (_recording)
        {
            _btnRecord.Focus();
            _btnRecord.Capture = true;
        }
    }

    private void OnRecordKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_recording) return;

        if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey ||
            e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
            return;

        e.Handled = true;
        _recordedKey = e.KeyCode;
        _txtHotkey.Text = e.KeyCode.ToString();
        _recording = false;
        _btnRecord.Text = "Record";
        _btnRecord.BackColor = Color.FromArgb(0, 120, 215);
    }

    private void PickColor(Button btn)
    {
        using var cd = new ColorDialog
        {
            Color = btn.BackColor,
            AllowFullOpen = true,
            FullOpen = true
        };
        if (cd.ShowDialog() == DialogResult.OK)
        {
            btn.BackColor = cd.Color;
        }
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        _settings.CtrlModifier = _chkCtrl.Checked;
        _settings.AltModifier = _chkAlt.Checked;
        _settings.ShiftModifier = _chkShift.Checked;
        _settings.WinModifier = _chkWin.Checked;
        _settings.HotkeyKey = _recordedKey != Keys.None ? _recordedKey : _settings.HotkeyKey;

        var format = _cmbFormat.SelectedItem?.ToString() ?? "PNG";
        _settings.SaveFormat = format.ToLowerInvariant();

        _settings.LineWidth = (int)_numLineWidth.Value;
        _settings.MarkerWidth = (int)_numMarkerWidth.Value;
        _settings.TextFontSize = (int)_numFontSize.Value;
        _settings.DrawColor = _btnDrawColor.BackColor;
        _settings.MarkerColor = _btnMarkerColor.BackColor;
        _settings.TextColor = _btnTextColor.BackColor;
        _settings.OverlayOpacity = (int)_numOverlay.Value;
        _settings.ShowCursorInCapture = _chkCursor.Checked;
        _settings.StartWithWindows = _chkStartWithWindows.Checked;

        _settings.Save();
        _onHotkeyChanged();
        Close();
    }
}