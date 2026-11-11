namespace Kawarimi.Formats;

public static class ImageResize
{
    public static byte[] Area(int w, int h, byte[] rgb, int nw, int nh)
    {
        var o = new byte[nw * nh * 3];
        double sx = (double)w / nw, sy = (double)h / nh;
        for (int y = 0; y < nh; y++)
            for (int x = 0; x < nw; x++)
            {
                double x0 = x * sx, x1 = x0 + sx, y0 = y * sy, y1 = y0 + sy;
                double r = 0, g = 0, b = 0, total = 0;
                for (int j = (int)y0; j < Math.Min(h, (int)Math.Ceiling(y1)); j++)
                {
                    double wy = Math.Min(j + 1, y1) - Math.Max(j, y0);
                    for (int i = (int)x0; i < Math.Min(w, (int)Math.Ceiling(x1)); i++)
                    {
                        double weight = wy * (Math.Min(i + 1, x1) - Math.Max(i, x0));
                        int p = (j * w + i) * 3;
                        r += rgb[p] * weight;
                        g += rgb[p + 1] * weight;
                        b += rgb[p + 2] * weight;
                        total += weight;
                    }
                }
                int q = (y * nw + x) * 3;
                o[q] = (byte)Math.Round(r / total);
                o[q + 1] = (byte)Math.Round(g / total);
                o[q + 2] = (byte)Math.Round(b / total);
            }
        return o;
    }
}
