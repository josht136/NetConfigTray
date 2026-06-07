using System.Runtime.InteropServices;
using NetConfigTray.Models;

namespace NetConfigTray.Helpers;

public static class AppIconHelper
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon CreateTrayIcon(IpConfigurationType? configurationType = null)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);

        var backgroundColor = configurationType switch
        {
            IpConfigurationType.Dhcp => Color.FromArgb(133, 153, 0),
            IpConfigurationType.Static => Color.FromArgb(203, 75, 22),
            _ => Color.FromArgb(42, 161, 152)
        };

        using var backgroundBrush = new SolidBrush(backgroundColor);
        graphics.FillEllipse(backgroundBrush, 2, 2, size - 4, size - 4);

        if (configurationType is null)
        {
            using var pen = new Pen(Color.White, 2.5f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };

            graphics.DrawArc(pen, 8, 10, 16, 12, 180, 180);
            graphics.DrawLine(pen, 8, 16, 12, 20);
            graphics.DrawLine(pen, 24, 16, 20, 20);
        }
        else
        {
            var letter = configurationType == IpConfigurationType.Dhcp ? "D" : "S";
            using var font = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            var textSize = graphics.MeasureString(letter, font);
            graphics.DrawString(
                letter,
                font,
                textBrush,
                (size - textSize.Width) / 2f,
                (size - textSize.Height) / 2f - 1f);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(handle);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
