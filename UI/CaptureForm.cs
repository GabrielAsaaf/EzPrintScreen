using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EzPrintScreen.Core;

namespace EzPrintScreen.UI;

public enum DrawingTool { None, Rectangle, Arrow, Line, Marker, Text }

public enum CaptureState { Idle, Selecting, Selected, Drawing, TextInput }

public class DrawingAction
{
    public DrawingTool Tool { get; set; }
    public List<Point> Points { get; set; } = new();
    public Color Color { get; set; } = Color.Red;
    public int Width { get; set; } = 3;
    public string Text { get; set; } = "";
    public float FontSize { get; set; } = 16f;
    public bool IsFilled { get; set; } = false;
}

public enum SelectionHandle
{
    None, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left, Move
}

public class CaptureForm : Form
{
    private readonly AppSettings _settings;
    private Bitmap? _capturedImage;
    private Rectangle _selectionRect;
    private CaptureState _state = CaptureState.Idle;
    private DrawingTool _currentTool = DrawingTool.None;
    private Point _startPoint;
    private Point _currentPoint;
    private Rectangle _selectionBeforeDrag;
    private SelectionHandle _activeHandle = SelectionHandle.None;
    private readonly List<DrawingAction> _actions = new();
    private DrawingAction? _currentAction;
    private Panel? _toolbar;
    private Panel? _toolbarShadow;
    private ToolTip? _toolTip;
    private TextBox? _textBox;
    private Point _textPlacement;
    private bool _toolbarVisible;
    private bool _dialogOpen;
    private bool _committingText;

    // Visual constants
    private const int HandleRadius = 4;
    private const int HandleDiameter = HandleRadius * 2;
    private const int MinSelectionSize = 10;
    private const int ToolbarHeight = 44;
    private const int ToolbarButtonWidth = 38;
    private const int ToolbarButtonHeight = 30;
    private const int ToolbarPadding = 6;
    private const int ToolbarCornerRadius = 8;
    private const int ShadowOffset = 3;

    // Color palette
    private static readonly Color AccentColor = Color.FromArgb(0, 120, 212);
    private static readonly Color ToolbarBg = Color.FromArgb(32, 32, 32);
    private static readonly Color BtnNormal = Color.FromArgb(48, 48, 48);
    private static readonly Color BtnHover = Color.FromArgb(62, 62, 62);
    private static readonly Color BtnActive = AccentColor;
    private static readonly Color BtnText = Color.FromArgb(220, 220, 220);
    private static readonly Color DividerColor = Color.FromArgb(65, 65, 65);
    private static readonly Color SizeLabelBg = Color.FromArgb(28, 28, 28);
    private static readonly Color SizeLabelBorder = Color.FromArgb(60, 60, 60);
    private static readonly Color SelectionBorder = AccentColor;

    public CaptureForm(AppSettings settings)
    {
        _settings = settings;
        InitializeForm();
    }

    private void InitializeForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = false;
        TopMost = true;
        Cursor = Cursors.Cross;
        StartPosition = FormStartPosition.Manual;

        // Use SystemInformation.VirtualScreen — same coordinate space as
        // Screen.Bounds, mouse events, and CopyFromScreen. No DPI conversion needed.
        var virtualScreen = ScreenCapture.GetVirtualBounds();
        Bounds = new Rectangle(virtualScreen.X, virtualScreen.Y,
                               virtualScreen.Width, virtualScreen.Height);

        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);

        // Capture all screens — image pixels = form-local pixels
        _capturedImage = ScreenCapture.CaptureAllScreens(_settings.ShowCursorInCapture);

        KeyPreview = true;
        KeyDown += OnKeyDown;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        Paint += OnPaint;
        Deactivate += OnDeactivate;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Focus();
    }

    private void OnDeactivate(object? sender, EventArgs e)
    {
        if (_state == CaptureState.TextInput || _dialogOpen) return;
        CloseForm();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_state == CaptureState.TextInput) return;

        if (e.KeyCode == Keys.Escape)
        {
            if (_state == CaptureState.Selected && _currentTool != DrawingTool.None)
            {
                _currentTool = DrawingTool.None;
                UpdateToolbarSelection();
                Cursor = Cursors.Default;
                Invalidate();
                return;
            }
            CloseForm();
        }
        else if (e.Control && e.KeyCode == Keys.Z)
        {
            e.Handled = true;
            Undo();
        }
        else if (e.Control && e.KeyCode == Keys.C)
        {
            e.Handled = true;
            CopyToClipboard();
            CloseForm();
        }
        else if (e.Control && e.KeyCode == Keys.S)
        {
            e.Handled = true;
            SaveToFile();
        }
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        var p = e.Location;

        if (_state == CaptureState.Idle || _state == CaptureState.Selected)
        {
            if (_state == CaptureState.Selected && _currentTool == DrawingTool.None)
            {
                var handle = HitTestHandle(p);
                if (handle != SelectionHandle.None)
                {
                    _activeHandle = handle;
                    _selectionBeforeDrag = _selectionRect;
                    _startPoint = p;
                    _state = CaptureState.Selected;
                    return;
                }
            }

            if (_currentTool == DrawingTool.None)
            {
                _startPoint = p;
                _state = CaptureState.Selecting;
                HideToolbar();
                _selectionRect = Rectangle.Empty;
                _actions.Clear();
                Invalidate();
                return;
            }

            if (_currentTool == DrawingTool.Text)
            {
                if (_selectionRect.Contains(p))
                {
                    StartTextInput(p);
                    return;
                }
                return;
            }

            if (_selectionRect.Contains(p))
            {
                var relP = ToSelectionRelative(p);
                _currentAction = new DrawingAction
                {
                    Tool = _currentTool,
                    Color = _currentTool == DrawingTool.Marker ? _settings.MarkerColor : _settings.DrawColor,
                    Width = _currentTool == DrawingTool.Marker ? _settings.MarkerWidth : _settings.LineWidth,
                    Points = { relP }
                };
                _state = CaptureState.Drawing;
            }
        }
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        var p = e.Location;

        switch (_state)
        {
            case CaptureState.Idle:
                break;

            case CaptureState.Selecting:
                _currentPoint = p;
                var x = Math.Min(_startPoint.X, p.X);
                var y = Math.Min(_startPoint.Y, p.Y);
                var w = Math.Abs(p.X - _startPoint.X);
                var selH = Math.Abs(p.Y - _startPoint.Y);
                _selectionRect = new Rectangle(x, y, w, selH);
                Invalidate();
                break;

            case CaptureState.Selected:
                if (_activeHandle != SelectionHandle.None && _currentTool == DrawingTool.None)
                {
                    var dx = p.X - _startPoint.X;
                    var dy = p.Y - _startPoint.Y;
                    var sr = _selectionBeforeDrag;
                    var r = sr;

                    switch (_activeHandle)
                    {
                        case SelectionHandle.TopLeft:
                            r = new Rectangle(sr.X + dx, sr.Y + dy, sr.Width - dx, sr.Height - dy);
                            break;
                        case SelectionHandle.Top:
                            r = new Rectangle(sr.X, sr.Y + dy, sr.Width, sr.Height - dy);
                            break;
                        case SelectionHandle.TopRight:
                            r = new Rectangle(sr.X, sr.Y + dy, sr.Width + dx, sr.Height - dy);
                            break;
                        case SelectionHandle.Right:
                            r = new Rectangle(sr.X, sr.Y, sr.Width + dx, sr.Height);
                            break;
                        case SelectionHandle.BottomRight:
                            r = new Rectangle(sr.X, sr.Y, sr.Width + dx, sr.Height + dy);
                            break;
                        case SelectionHandle.Bottom:
                            r = new Rectangle(sr.X, sr.Y, sr.Width, sr.Height + dy);
                            break;
                        case SelectionHandle.BottomLeft:
                            r = new Rectangle(sr.X + dx, sr.Y, sr.Width - dx, sr.Height + dy);
                            break;
                        case SelectionHandle.Left:
                            r = new Rectangle(sr.X + dx, sr.Y, sr.Width - dx, sr.Height);
                            break;
                        case SelectionHandle.Move:
                            r = new Rectangle(sr.X + dx, sr.Y + dy, sr.Width, sr.Height);
                            break;
                    }

                    r.X = Math.Max(0, r.X);
                    r.Y = Math.Max(0, r.Y);
                    if (r.Right > ClientSize.Width) r.Width = ClientSize.Width - r.X;
                    if (r.Bottom > ClientSize.Height) r.Height = ClientSize.Height - r.Y;

                    _selectionRect = r;
                    Invalidate();
                    RepositionToolbar();
                }
                else if (_currentTool == DrawingTool.None)
                {
                    var handle = HitTestHandle(p);
                    UpdateCursor(handle);
                }
                break;

            case CaptureState.Drawing:
                if (_currentAction != null)
                {
                    _currentAction.Points.Add(ToSelectionRelative(p));
                    Invalidate();
                }
                break;
        }
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        switch (_state)
        {
            case CaptureState.Selecting:
                if (_selectionRect.Width >= MinSelectionSize && _selectionRect.Height >= MinSelectionSize)
                {
                    _state = CaptureState.Selected;
                    _currentTool = DrawingTool.None;
                    ShowToolbar();
                    Cursor = Cursors.Default;
                }
                else
                {
                    _state = CaptureState.Idle;
                    _selectionRect = Rectangle.Empty;
                }
                Invalidate();
                break;

            case CaptureState.Selected:
                if (_activeHandle != SelectionHandle.None)
                {
                    _activeHandle = SelectionHandle.None;
                    RepositionToolbar();
                }
                break;

            case CaptureState.Drawing:
                if (_currentAction != null)
                {
                    if (_currentAction.Points.Count >= 2)
                        _actions.Add(_currentAction);
                    _currentAction = null;
                }
                _state = CaptureState.Selected;
                Invalidate();
                break;
        }
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        if (_capturedImage == null) return;

        var g = e.Graphics;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // Draw full captured image — image coords == form coords, no scaling
        g.DrawImage(_capturedImage, 0, 0, ClientSize.Width, ClientSize.Height);

        if (_state == CaptureState.Idle)
        {
            using var idleBrush = new SolidBrush(Color.FromArgb(_settings.OverlayOpacity, 0, 0, 0));
            g.FillRectangle(idleBrush, ClientRectangle);
            return;
        }

        if (_state == CaptureState.Selecting)
        {
            using var selBrush = new SolidBrush(Color.FromArgb(_settings.OverlayOpacity, 0, 0, 0));
            g.FillRectangle(selBrush, ClientRectangle);

            if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
            {
                // Draw the bright (un-dimmed) selection directly
                g.DrawImage(_capturedImage, _selectionRect, _selectionRect, GraphicsUnit.Pixel);
                DrawSelectionBorder(g, _selectionRect);
                DrawSizeLabel(g, _selectionRect);
            }
            return;
        }

        if (_state == CaptureState.Selected || _state == CaptureState.Drawing || _state == CaptureState.TextInput)
        {
            using var overlayBrush = new SolidBrush(Color.FromArgb(_settings.OverlayOpacity, 0, 0, 0));
            g.FillRectangle(overlayBrush, ClientRectangle);

            if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
            {
                // Clamp source to captured image bounds, map back for destination
                var srcRect = _selectionRect;
                srcRect.Intersect(new Rectangle(0, 0, _capturedImage.Width, _capturedImage.Height));
                if (srcRect.Width > 0 && srcRect.Height > 0)
                {
                    g.DrawImage(_capturedImage, srcRect, srcRect, GraphicsUnit.Pixel);
                }

                // Draw annotations clipped to selection
                g.SetClip(_selectionRect);
                g.TranslateTransform(_selectionRect.X, _selectionRect.Y);
                RenderActions(g, _actions);
                if (_currentAction != null)
                    RenderAction(g, _currentAction);
                g.ResetTransform();
                g.ResetClip();

                if (_currentTool == DrawingTool.None)
                {
                    DrawSelectionBorder(g, _selectionRect);
                    DrawSizeLabel(g, _selectionRect);
                    DrawResizeHandles(g);
                }
            }
        }
    }

    #region Selection Visuals

    private void DrawSelectionBorder(Graphics g, Rectangle rect)
    {
        using var whitePen = new Pen(Color.FromArgb(200, 255, 255, 255), 1f);
        g.DrawRectangle(whitePen, rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2);
        using var accentPen = new Pen(SelectionBorder, 2f);
        g.DrawRectangle(accentPen, rect);
    }

    private void DrawResizeHandles(Graphics g)
    {
        var handleCenters = GetHandleCenters();
        foreach (var center in handleCenters)
        {
            using var borderPen = new Pen(SelectionBorder, 2f);
            g.DrawEllipse(borderPen,
                center.X - HandleRadius, center.Y - HandleRadius,
                HandleDiameter, HandleDiameter);
            using var fillBrush = new SolidBrush(Color.White);
            g.FillEllipse(fillBrush,
                center.X - HandleRadius + 1, center.Y - HandleRadius + 1,
                HandleDiameter - 2, HandleDiameter - 2);
        }
    }

    private Point[] GetHandleCenters()
    {
        var r = _selectionRect;
        return new[]
        {
            new Point(r.Left, r.Top),
            new Point(r.Left + r.Width / 2, r.Top),
            new Point(r.Right, r.Top),
            new Point(r.Right, r.Top + r.Height / 2),
            new Point(r.Right, r.Bottom),
            new Point(r.Left + r.Width / 2, r.Bottom),
            new Point(r.Left, r.Bottom),
            new Point(r.Left, r.Top + r.Height / 2),
        };
    }

    private Rectangle[] GetHandleRects()
    {
        var centers = GetHandleCenters();
        return centers.Select(c => new Rectangle(
            c.X - HandleRadius - 2, c.Y - HandleRadius - 2,
            HandleDiameter + 4, HandleDiameter + 4)).ToArray();
    }

    private void DrawSizeLabel(Graphics g, Rectangle rect)
    {
        var text = $"{rect.Width} \u00D7 {rect.Height}";
        using var font = new Font("Segoe UI", 10f, FontStyle.Regular);
        var size = g.MeasureString(text, font);

        var padX = 8;
        var padY = 4;
        var labelW = (int)size.Width + padX * 2;
        var labelH = (int)size.Height + padY * 2;
        var labelX = rect.X + rect.Width / 2 - labelW / 2;
        var labelY = rect.Bottom + 8;

        if (labelY + labelH > ClientSize.Height)
            labelY = rect.Bottom - labelH - 4;
        if (labelY < rect.Top)
            labelY = rect.Top + 4;

        var labelRect = new Rectangle(labelX, labelY, labelW, labelH);

        using var bgPath = CreateRoundedRectPath(labelRect, 4);
        using var bgBrush = new SolidBrush(SizeLabelBg);
        g.FillPath(bgBrush, bgPath);
        using var borderPen = new Pen(SizeLabelBorder, 1f);
        g.DrawPath(borderPen, bgPath);

        using var textBrush = new SolidBrush(Color.FromArgb(230, 230, 230));
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, textBrush, labelRect, sf);
    }

    #endregion

    #region Drawing Tools

    private void RenderActions(Graphics g, IEnumerable<DrawingAction> actions)
    {
        foreach (var action in actions)
            RenderAction(g, action);
    }

    private void RenderAction(Graphics g, DrawingAction action)
    {
        switch (action.Tool)
        {
            case DrawingTool.Rectangle:
                DrawRectangleTool(g, action);
                break;
            case DrawingTool.Line:
                DrawLineTool(g, action);
                break;
            case DrawingTool.Arrow:
                DrawArrowTool(g, action);
                break;
            case DrawingTool.Marker:
                DrawMarkerTool(g, action);
                break;
            case DrawingTool.Text:
                DrawTextTool(g, action);
                break;
        }
    }

    private void DrawRectangleTool(Graphics g, DrawingAction action)
    {
        if (action.Points.Count < 2) return;
        var p1 = action.Points[0];
        var p2 = action.Points[^1];
        var rect = new Rectangle(
            Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y),
            Math.Abs(p2.X - p1.X), Math.Abs(p2.Y - p1.Y));

        if (action.IsFilled)
        {
            using var fillBrush = new SolidBrush(Color.FromArgb(50, action.Color));
            g.FillRectangle(fillBrush, rect);
        }
        using var pen = new Pen(action.Color, action.Width);
        g.DrawRectangle(pen, rect);
    }

    private void DrawLineTool(Graphics g, DrawingAction action)
    {
        if (action.Points.Count < 2) return;
        using var pen = new Pen(action.Color, action.Width);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;
        g.DrawLine(pen, action.Points[0], action.Points[^1]);
    }

    private void DrawArrowTool(Graphics g, DrawingAction action)
    {
        if (action.Points.Count < 2) return;
        var from = action.Points[0];
        var to = action.Points[^1];

        using var pen = new Pen(action.Color, action.Width);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;
        g.DrawLine(pen, from, to);

        var angle = (float)Math.Atan2(to.Y - from.Y, to.X - from.X);
        var arrowLen = Math.Max(action.Width * 4, 12);
        var arrowAngle = (float)Math.PI / 6f;

        var left = new Point(
            (int)(to.X - arrowLen * Math.Cos(angle - arrowAngle)),
            (int)(to.Y - arrowLen * Math.Sin(angle - arrowAngle)));
        var right = new Point(
            (int)(to.X - arrowLen * Math.Cos(angle + arrowAngle)),
            (int)(to.Y - arrowLen * Math.Sin(angle + arrowAngle)));

        using var brush = new SolidBrush(action.Color);
        g.FillPolygon(brush, new[] { to, left, right });
    }

    private void DrawMarkerTool(Graphics g, DrawingAction action)
    {
        if (action.Points.Count < 2) return;
        using var pen = new Pen(Color.FromArgb(100, action.Color), action.Width);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;
        pen.LineJoin = LineJoin.Round;
        g.DrawLines(pen, action.Points.ToArray());
    }

    private void DrawTextTool(Graphics g, DrawingAction action)
    {
        if (string.IsNullOrEmpty(action.Text) || action.Points.Count < 1) return;
        using var font = new Font("Segoe UI", action.FontSize, FontStyle.Bold);
        using var brush = new SolidBrush(action.Color);
        var pt = action.Points[0];
        using var shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0));
        g.DrawString(action.Text, font, shadowBrush, pt.X + 1, pt.Y + 1);
        g.DrawString(action.Text, font, brush, pt.X, pt.Y);
    }

    #endregion

    #region Hit Testing

    private SelectionHandle HitTestHandle(Point p)
    {
        if (_selectionRect.Width < MinSelectionSize || _selectionRect.Height < MinSelectionSize)
            return SelectionHandle.None;

        var handles = GetHandleRects();
        for (int i = 0; i < handles.Length; i++)
        {
            if (handles[i].Contains(p))
                return (SelectionHandle)(i + 1);
        }

        var inflate = _selectionRect;
        inflate.Inflate(-HandleRadius * 2, -HandleRadius * 2);
        if (inflate.Contains(p)) return SelectionHandle.Move;

        return SelectionHandle.None;
    }

    private void UpdateCursor(SelectionHandle handle)
    {
        Cursor = handle switch
        {
            SelectionHandle.TopLeft or SelectionHandle.BottomRight => Cursors.SizeNWSE,
            SelectionHandle.TopRight or SelectionHandle.BottomLeft => Cursors.SizeNESW,
            SelectionHandle.Top or SelectionHandle.Bottom => Cursors.SizeNS,
            SelectionHandle.Left or SelectionHandle.Right => Cursors.SizeWE,
            SelectionHandle.Move => Cursors.SizeAll,
            _ => Cursors.Cross
        };
    }

    private Point ToSelectionRelative(Point screenPoint)
    {
        return new Point(screenPoint.X - _selectionRect.X, screenPoint.Y - _selectionRect.Y);
    }

    #endregion

    #region Toolbar

    private void ShowToolbar()
    {
        if (_toolbar != null)
        {
            _toolbar.Dispose();
            _toolbar = null;
        }
        if (_toolbarShadow != null)
        {
            _toolbarShadow.Dispose();
            _toolbarShadow = null;
        }

        if (_toolTip == null)
        {
            _toolTip = new ToolTip
            {
                InitialDelay = 400,
                ReshowDelay = 100,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                IsBalloon = true,
            };
        }
        else
        {
            _toolTip.RemoveAll();
        }

        _toolbarShadow = new Panel
        {
            BackColor = Color.FromArgb(0, 0, 0),
            Height = ToolbarHeight,
            AutoSize = false,
        };

        _toolbar = new Panel
        {
            BackColor = ToolbarBg,
            Height = ToolbarHeight,
            AutoSize = false,
        };

        var buttons = CreateToolbarButtons();

        var xPos = ToolbarPadding;
        foreach (var ctrl in buttons)
        {
            if (ctrl is Button btn)
            {
                btn.Location = new Point(xPos, (ToolbarHeight - ToolbarButtonHeight) / 2);
                btn.Size = new Size(ToolbarButtonWidth, ToolbarButtonHeight);
            }
            else if (ctrl is Panel div)
            {
                div.Location = new Point(xPos, (ToolbarHeight - ToolbarButtonHeight) / 2 + 4);
            }
            _toolbar.Controls.Add(ctrl);
            xPos += ctrl.Width + 2;
        }

        var totalWidth = xPos + ToolbarPadding - 2;
        _toolbar.Width = totalWidth;
        _toolbarShadow.Width = totalWidth;

        ApplyRoundedRegion(_toolbar, ToolbarCornerRadius);
        ApplyRoundedRegion(_toolbarShadow, ToolbarCornerRadius);

        PositionToolbar();

        Controls.Add(_toolbarShadow);
        Controls.Add(_toolbar);
        _toolbarShadow.BringToFront();
        _toolbar.BringToFront();
        _toolbarVisible = true;
    }

    private void PositionToolbar()
    {
        if (_toolbar == null) return;

        var x = _selectionRect.X + _selectionRect.Width / 2 - _toolbar.Width / 2;
        var y = _selectionRect.Top - (ToolbarHeight / 2);

        if (y < 0)
            y = _selectionRect.Top + 4;

        if (y + ToolbarHeight > _selectionRect.Bottom)
            y = _selectionRect.Bottom + 8;

        x = Math.Max(4, Math.Min(x, ClientSize.Width - _toolbar.Width - 4));

        _toolbar.Location = new Point(x, y);
        _toolbarShadow!.Location = new Point(x + ShadowOffset, y + ShadowOffset);
    }

    private void RepositionToolbar()
    {
        if (_toolbarVisible) PositionToolbar();
        Invalidate();
    }

    private void HideToolbar()
    {
        if (_toolbar != null)
        {
            Controls.Remove(_toolbar);
            _toolbar.Dispose();
            _toolbar = null;
        }
        if (_toolbarShadow != null)
        {
            Controls.Remove(_toolbarShadow);
            _toolbarShadow.Dispose();
            _toolbarShadow = null;
        }
        _toolbarVisible = false;
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        using var path = CreateRoundedRectPath(
            new Rectangle(0, 0, control.Width, control.Height), radius);
        control.Region = new Region(path);
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var r = Math.Min(radius, Math.Min(rect.Width / 2, rect.Height / 2));
        path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
        path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    private Control[] CreateToolbarButtons()
    {
        var list = new List<Control>();

        list.Add(CreateToolButton("Copy", "\u2398", "Copy", (s, e) => CopyToClipboard()));
        list.Add(CreateToolButton("Save", "\u2261", "Save", (s, e) => SaveToFile()));
        list.Add(CreateDivider());
        list.Add(CreateToolButton("Rect", "\u25A1", "Rect", (s, e) => SetTool(DrawingTool.Rectangle)));
        list.Add(CreateToolButton("Arrow", "\u2192", "Arrow", (s, e) => SetTool(DrawingTool.Arrow)));
        list.Add(CreateToolButton("Line", "\u2571", "Line", (s, e) => SetTool(DrawingTool.Line)));
        list.Add(CreateToolButton("Mark", "\u2248", "Mark", (s, e) => SetTool(DrawingTool.Marker)));
        list.Add(CreateToolButton("Text", "T", "Text", (s, e) => SetTool(DrawingTool.Text)));
        list.Add(CreateDivider());
        list.Add(CreateColorButton());
        list.Add(CreateDivider());
        list.Add(CreateToolButton("Undo", "\u21A9", "Undo", (s, e) => Undo()));
        list.Add(CreateToolButton("Close", "\u2715", "Close", (s, e) => CloseForm()));

        return list.ToArray();
    }

    private Button CreateToolButton(string tag, string symbol, string tooltip, EventHandler click)
    {
        var btn = new Button
        {
            Text = symbol,
            FlatStyle = FlatStyle.Flat,
            BackColor = BtnNormal,
            ForeColor = BtnText,
            Font = new Font("Segoe UI", 12f, FontStyle.Regular),
            FlatAppearance =
            {
                BorderSize = 0,
                MouseOverBackColor = BtnHover,
                MouseDownBackColor = Color.FromArgb(70, 70, 70),
            },
            Tag = tag,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        btn.Click += click;
        _toolTip?.SetToolTip(btn, tooltip);
        return btn;
    }

    private Panel CreateDivider()
    {
        return new Panel
        {
            Width = 1,
            Height = ToolbarButtonHeight - 10,
            BackColor = DividerColor,
            Margin = new Padding(4, 0, 4, 0),
        };
    }

    private Button CreateColorButton()
    {
        var btn = new Button
        {
            FlatStyle = FlatStyle.Flat,
            BackColor = _currentTool == DrawingTool.Marker ? _settings.MarkerColor : _settings.DrawColor,
            Font = new Font("Segoe UI", 8f),
            FlatAppearance =
            {
                BorderSize = 2,
                BorderColor = Color.FromArgb(100, 100, 100),
                MouseOverBackColor = BtnHover,
            },
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Tag = "color",
            Margin = new Padding(0),
        };

        btn.Click += (s, e) =>
        {
            _dialogOpen = true;
            TopMost = false;
            try
            {
                using var cd = new ColorDialog { Color = btn.BackColor, AllowFullOpen = true, FullOpen = true };
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    btn.BackColor = cd.Color;
                    if (_currentTool == DrawingTool.Marker)
                    {
                        _settings.MarkerColor = cd.Color;
                        _settings.Save();
                    }
                    else
                    {
                        _settings.DrawColor = cd.Color;
                        _settings.TextColor = cd.Color;
                        _settings.Save();
                    }
                }
            }
            finally
            {
                TopMost = true;
                _dialogOpen = false;
            }
        };

        _toolTip?.SetToolTip(btn, "Color");
        return btn;
    }

    private void SetTool(DrawingTool tool)
    {
        if (_currentTool == tool)
        {
            _currentTool = DrawingTool.None;
            Cursor = Cursors.Default;
        }
        else
        {
            _currentTool = tool;
            Cursor = tool == DrawingTool.Text ? Cursors.IBeam : Cursors.Cross;
        }
        UpdateToolbarSelection();
    }

    private void UpdateToolbarSelection()
    {
        if (_toolbar == null) return;
        foreach (Control ctrl in _toolbar.Controls)
        {
            if (ctrl is Button btn && btn.Tag is string tag)
            {
                var isActive = tag switch
                {
                    "Rect" => _currentTool == DrawingTool.Rectangle,
                    "Arrow" => _currentTool == DrawingTool.Arrow,
                    "Line" => _currentTool == DrawingTool.Line,
                    "Mark" => _currentTool == DrawingTool.Marker,
                    "Text" => _currentTool == DrawingTool.Text,
                    _ => false
                };

                btn.BackColor = isActive ? BtnActive : BtnNormal;
                btn.ForeColor = isActive ? Color.White : BtnText;
            }
        }
    }

    #endregion

    #region Text Input

    private void StartTextInput(Point location)
    {
        _state = CaptureState.TextInput;
        _committingText = false;
        _textPlacement = ToSelectionRelative(location);

        _textBox = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", _settings.TextFontSize, FontStyle.Bold),
            ForeColor = _settings.TextColor,
            BackColor = Color.White,
            Location = location,
            Width = Math.Min(300, _selectionRect.Right - location.X - 10),
            Visible = true,
        };

        _textBox.KeyDown += TextBox_KeyDown;
        _textBox.LostFocus += TextBox_LostFocus;

        Controls.Add(_textBox);
        _textBox.Focus();
    }

    private void TextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            CommitText();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            CancelText();
            e.Handled = true;
        }
    }

    private void TextBox_LostFocus(object? sender, EventArgs e)
    {
        if (_state == CaptureState.TextInput)
            CommitText();
    }

    private void CommitText()
    {
        if (_committingText) return;
        _committingText = true;

        try
        {
            string? text = null;
            if (_textBox != null)
            {
                try { text = _textBox.Text; } catch { }
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                _actions.Add(new DrawingAction
                {
                    Tool = DrawingTool.Text,
                    Points = { _textPlacement },
                    Text = text,
                    Color = _settings.TextColor,
                    FontSize = _settings.TextFontSize
                });
            }

            CleanupText();
            _state = CaptureState.Selected;
            Cursor = Cursors.Default;
            Invalidate();
        }
        finally
        {
            _committingText = false;
        }
    }

    private void CancelText()
    {
        if (_committingText) return;
        _committingText = true;
        try
        {
            CleanupText();
            _state = CaptureState.Selected;
            Cursor = Cursors.Default;
        }
        finally
        {
            _committingText = false;
        }
    }

    private void CleanupText()
    {
        if (_textBox != null)
        {
            _textBox.KeyDown -= TextBox_KeyDown;
            _textBox.LostFocus -= TextBox_LostFocus;
            _textBox.Visible = false;
            Controls.Remove(_textBox);
            _textBox.Dispose();
            _textBox = null;
        }
    }

    #endregion

    #region Actions

    private void Undo()
    {
        if (_actions.Count > 0)
        {
            _actions.RemoveAt(_actions.Count - 1);
            Invalidate();
        }
    }

    private void CopyToClipboard()
    {
        try
        {
            var img = BuildFinalImage();
            Clipboard.SetImage(img);
        }
        catch { }
    }

    private void SaveToFile()
    {
        try
        {
            _dialogOpen = true;
            TopMost = false;
            using var sfd = new SaveFileDialog
            {
                InitialDirectory = _settings.SaveDirectory,
                DefaultExt = ScreenCapture.GetFileExtension(_settings.SaveFormat),
                Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp|GIF|*.gif|All Files|*.*",
                FilterIndex = _settings.SaveFormat.ToLowerInvariant() switch
                {
                    "jpg" or "jpeg" => 2,
                    "bmp" => 3,
                    "gif" => 4,
                    _ => 1
                },
                FileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}",
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                using var img = BuildFinalImage();
                img.Save(sfd.FileName, ScreenCapture.GetImageFormat(
                    Path.GetExtension(sfd.FileName).TrimStart('.')));

                _settings.SaveDirectory = Path.GetDirectoryName(sfd.FileName) ?? _settings.SaveDirectory;
                _settings.Save();
            }
        }
        catch { }
        finally
        {
            TopMost = true;
            _dialogOpen = false;
        }
    }

    /// <summary>
    /// Crops the selected region from the captured bitmap.
    /// Image coords == form coords, so _selectionRect maps directly.
    /// </summary>
    private Bitmap BuildFinalImage()
    {
        if (_capturedImage == null)
            return new Bitmap(1, 1);

        var cropRect = _selectionRect;
        cropRect.Intersect(new Rectangle(0, 0, _capturedImage.Width, _capturedImage.Height));

        if (cropRect.Width <= 0 || cropRect.Height <= 0)
            return new Bitmap(1, 1);

        var result = new Bitmap(cropRect.Width, cropRect.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            g.DrawImage(_capturedImage,
                new Rectangle(0, 0, cropRect.Width, cropRect.Height),
                new Rectangle(cropRect.X, cropRect.Y, cropRect.Width, cropRect.Height),
                GraphicsUnit.Pixel);

            // Annotations are in selection-relative coords; translate to output origin
            if (_actions.Count > 0)
            {
                g.TranslateTransform(-cropRect.X + _selectionRect.X, -cropRect.Y + _selectionRect.Y);
                RenderActions(g, _actions);
            }
        }

        return result;
    }

    private void CloseForm()
    {
        CleanupText();
        HideToolbar();
        Close();
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _capturedImage?.Dispose();
            _toolbar?.Dispose();
            _toolbarShadow?.Dispose();
            _toolTip?.Dispose();
            _textBox?.Dispose();
        }
        base.Dispose(disposing);
    }
}