using Clippings.Core.Helpers;

namespace Clippings.Core.Tests;

public sealed class AudioGainTests
{
    [Fact]
    public void Apply_UnityGain_LeavesSamplesUnchanged()
    {
        short[] samples = [0, 1_000, -1_000, short.MaxValue, short.MinValue];

        AudioGain.Apply(samples, 1.0);

        Assert.Equal<short[]>([0, 1_000, -1_000, short.MaxValue, short.MinValue], samples);
    }

    [Fact]
    public void Apply_DoublesQuietSamples()
    {
        short[] samples = [1_000, -1_000, 10_000];

        AudioGain.Apply(samples, 2.0);

        Assert.Equal<short[]>([2_000, -2_000, 20_000], samples);
    }

    [Fact]
    public void Apply_ClampsLoudSamples()
    {
        short[] samples = [short.MaxValue, short.MinValue, 30_000, -30_000];

        AudioGain.Apply(samples, 4.0);

        Assert.Equal(short.MaxValue, samples[0]);
        Assert.InRange((int)samples[1], short.MinValue, -32_000);
        Assert.InRange((int)samples[2], 32_000, short.MaxValue);
        Assert.InRange((int)samples[3], short.MinValue, -32_000);
    }

    [Fact]
    public void Apply_InvalidGain_FallsBackToUnity()
    {
        short[] samples = [1_000, -2_000];

        AudioGain.Apply(samples, double.NaN);

        Assert.Equal<short[]>([1_000, -2_000], samples);
    }

    [Fact]
    public void ApplyToBuffer_GainsLittleEndianSamples()
    {
        byte[] buffer = new byte[4];
        BitConverter.TryWriteBytes(buffer.AsSpan(0, 2), (short)1_000);
        BitConverter.TryWriteBytes(buffer.AsSpan(2, 2), (short)-2_000);

        AudioGain.ApplyToBuffer(buffer, buffer.Length, 2.0);

        Assert.Equal((short)2_000, BitConverter.ToInt16(buffer, 0));
        Assert.Equal((short)-4_000, BitConverter.ToInt16(buffer, 2));
    }
}
