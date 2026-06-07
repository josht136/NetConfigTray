namespace NetConfigTray.Helpers;

public static class AppIconHelper
{
    public static Icon CreateTrayIcon()
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var backgroundBrush = new SolidBrush(Color.FromArgb(0, 120, 215));
        graphics.FillEllipse(backgroundBrush, 2, 2, size - 4, size - 4);

        using var pen = new Pen(Color.White, 2.5f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };

        graphics.DrawArc(pen, 8, 10, 16, 12, 180, 180);
        graphics.DrawLine(pen, 8, 16, 12, 20);
        graphics.DrawLine(pen, 24, 16, 20, 20);

        var handle = bitmap.GetHicon();
        var icon = Icon.FromHandle(handle);
        return (Icon)icon.Clone();
    }
}
