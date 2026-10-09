using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace EzPrintScreen.Core;

public static class ScreenCapture
{
    /// <summary>
    /// Returns the virtual screen bounds in the coordinate system that WinForms uses.
    /// This is the correct coordinate space for form positioning and mouse events.
    /// </summary>
    public static Rectangle GetVirtualBounds()
    {
        return SystemInformation.VirtualScreen;
    }

    /// <summary>
    /// Captures all screens into a single bitmap.
    /// Uses per-monitor capture via Screen.Bounds and CopyFromScreen,
    /// compositing into one bitmap that matches the virtual screen coordinate space.
    /// </summary>
    public static Bitmap CaptureAllScreens(bool showCursor = false)
    {
        var virtualBounds = SystemInformation.VirtualScreen;
        if (virtualBounds.Width <= 0 || virtualBounds.Height <= 0)
            return new Bitmap(1, 1, PixelFormat.Format32bppArgb);

        var bmp = new Bitmap(virtualBounds.Width, virtualBounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Capture each monitor individually and composite
            foreach (var screen in Screen.AllScreens)
            {
                var destX = screen.Bounds.X - virtualBounds.X;
                var destY = screen.Bounds.Y - virtualBounds.Y;

                try
                {
                    g.CopyFromScreen(
                        screen.Bounds.Location,
                        new Point(destX, destY),
                        screen.Bounds.Size,
                        CopyPixelOperation.SourceCopy);
                }
                catch
                {
                    // Fallback: try capturing the entire virtual screen at once
                    try
                    {
                        g.CopyFromScreen(
                            virtualBounds.Location,
                            Point.Empty,
                            virtualBounds.Size,
                            CopyPixelOperation.SourceCopy);
                    }
                    catch { }
                    break;
                }
            }

            if (showCursor)
            {
                DrawCursor(g, virtualBounds.Location);
            }
        }

        return bmp;
    }

    private static void DrawCursor(Graphics g, Point offset)
    {
        try
        {
            var cursorPos = Cursor.Position;
            var relX = cursorPos.X - offset.X;
            var relY = cursorPos.Y - offset.Y;

            using var cursorBmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using var cg = Graphics.FromImage(cursorBmp);
            Cursor.Current?.Draw(cg, new Rectangle(0, 0, 32, 32));
            g.DrawImage(cursorBmp, relX, relY);
        }
        catch { }
    }

    public static Bitmap CropRegion(Bitmap source, Rectangle region)
    {
        if (region.X < 0) { region.Width += region.X; region.X = 0; }
        if (region.Y < 0) { region.Height += region.Y; region.Y = 0; }

        region.Width = Math.Min(region.Width, source.Width - region.X);
        region.Height = Math.Min(region.Height, source.Height - region.Y);

        if (region.Width <= 0 || region.Height <= 0)
            return new Bitmap(1, 1);

        var cropped = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(cropped))
        {
            g.DrawImage(source, new Rectangle(0, 0, region.Width, region.Height),
                region, GraphicsUnit.Pixel);
        }
        return cropped;
    }

    public static ImageFormat GetImageFormat(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => ImageFormat.Jpeg,
            "bmp" => ImageFormat.Bmp,
            "gif" => ImageFormat.Gif,
            "tiff" => ImageFormat.Tiff,
            _ => ImageFormat.Png
        };
    }

    public static string GetFileExtension(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => ".jpg",
            "bmp" => ".bmp",
            "gif" => ".gif",
            "tiff" => ".tiff",
            _ => ".png"
        };
    }
}