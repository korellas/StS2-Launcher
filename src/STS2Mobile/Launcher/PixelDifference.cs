using System;

namespace STS2Mobile.Launcher;

public sealed record PixelDifference(
    byte[] Pixels,
    int[] Histogram,
    double MeanAbsolute,
    int Maximum
)
{
    public static PixelDifference Calculate(byte[] original, byte[] changed)
    {
        if (original.Length == 0 || original.Length != changed.Length || original.Length % 4 != 0)
            throw new ArgumentException("Comparison requires equal, non-empty RGBA8 images");
        var pixels = new byte[original.Length];
        var histogram = new int[256];
        long sum = 0;
        int maximum = 0;
        for (int i = 0; i < original.Length; i += 4)
        {
            int delta = 0;
            for (int channel = 0; channel < 4; channel++)
            {
                int absolute = Math.Abs(original[i + channel] - changed[i + channel]);
                sum += absolute;
                delta = Math.Max(delta, absolute);
            }
            histogram[delta]++;
            maximum = Math.Max(maximum, delta);
            pixels[i] = pixels[i + 1] = pixels[i + 2] = (byte)delta;
            pixels[i + 3] = 255;
        }
        return new PixelDifference(pixels, histogram, (double)sum / original.Length, maximum);
    }
}
