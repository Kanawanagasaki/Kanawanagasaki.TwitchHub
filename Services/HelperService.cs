namespace Kanawanagasaki.TwitchHub.Services;

public class HelperService
{
    public (byte r, byte g, byte b) HexToRgb(string hex)
    {
        int rgb = int.Parse(hex.Substring(1), System.Globalization.NumberStyles.HexNumber);
        byte r = (byte)((rgb >> 16) & 0xFF);
        byte g = (byte)((rgb >> 8) & 0xFF);
        byte b = (byte)(rgb & 0xFF);
        return (r, g, b);
    }

    public (double h, double s, double l) RgbToHsl((byte br, byte bg, byte bb) colors)
        => RgbToHsl(colors.br, colors.bg, colors.bb);

    public (double h, double s, double l) RgbToHsl(byte br, byte bg, byte bb)
    {
        double r = br / 255d;
        double g = bg / 255d;
        double b = bb / 255d;

        double cmin = Math.Min(r, Math.Min(g, b));
        double cmax = Math.Max(r, Math.Max(g, b));
        double delta = cmax - cmin;
        double h = 0;
        double s = 0;
        double l = 0;

        if (delta == 0) h = 0;
        else if (cmax == r) h = ((g - b) / delta) % 6;
        else if (cmax == g) h = (b - r) / delta + 2;
        else h = (r - g) / delta + 4;

        h = Math.Round(h * 60);

        if (h < 0) h += 360;

        l = (cmax + cmin) / 2;

        s = delta == 0 ? 0 : delta / (1 - Math.Abs(2 * l - 1));

        return (h, s, l);
    }

}