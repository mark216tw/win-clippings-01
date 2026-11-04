using System.Runtime.InteropServices;

namespace Clippings.Core.Helpers;

public static class AudioGain
{
    private const double LimitThreshold = short.MaxValue * 0.85;

    public static void Apply(Span<short> samples, double gain)
    {
        var effectiveGain = double.IsFinite(gain) && gain > 0 ? gain : 1.0;
        if (Math.Abs(effectiveGain - 1.0) < double.Epsilon)
        {
            return;
        }

        for (var index = 0; index < samples.Length; index++)
        {
            var scaled = samples[index] * effectiveGain;
            samples[index] = effectiveGain < 1.0
                ? (short)Math.Round(scaled)
                : Limit(scaled);
        }
    }

    public static void ApplyToBuffer(Span<byte> buffer, int length, double gain)
    {
        if (length < sizeof(short))
        {
            return;
        }

        var usableLength = length & ~1;
        var samples = MemoryMarshal.Cast<byte, short>(buffer[..usableLength]);
        Apply(samples, gain);
    }

    private static short Limit(double value)
    {
        var magnitude = Math.Abs(value);
        if (magnitude <= LimitThreshold)
        {
            return (short)Math.Round(value);
        }

        var headroom = short.MaxValue - LimitThreshold;
        var limited = LimitThreshold + headroom * Math.Tanh((magnitude - LimitThreshold) / headroom);
        var signed = Math.Sign(value) * limited;
        return (short)Math.Clamp(signed, short.MinValue, short.MaxValue);
    }
}
