using Clippings.Core.Helpers;
using Clippings.Core.Models;
using NAudio.Wave;

namespace Clippings.Platform.Capture;

internal sealed class SystemAudioLoopbackCapture : IDisposable
{
    public const int SampleRate = 48_000;
    public const int ChannelCount = 2;
    public const int BitsPerSample = 16;
    public const int PacketMilliseconds = 20;
    public const int FramesPerPacket = SampleRate * PacketMilliseconds / 1000;
    public const int BytesPerPacket = FramesPerPacket * ChannelCount * (BitsPerSample / 8);

    private readonly object _sync = new();
    private readonly RecordingControl _control;
    private readonly WasapiLoopbackCapture _capture;
    private readonly BufferedWaveProvider _buffer;
    private readonly MediaFoundationResampler _resampler;
    private readonly double _gain;
    private Exception? _failure;
    private bool _started;
    private bool _stopping;
    private bool _disposed;

    public SystemAudioLoopbackCapture(RecordingControl control, double gain = 2.0)
    {
        _control = control;
        _gain = double.IsFinite(gain) && gain > 0 ? gain : 1.0;
        try
        {
            _capture = new WasapiLoopbackCapture();
            _buffer = new BufferedWaveProvider(_capture.WaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(2),
                DiscardOnBufferOverflow = true,
                ReadFully = true,
            };
            _resampler = new MediaFoundationResampler(
                _buffer,
                new WaveFormat(SampleRate, BitsPerSample, ChannelCount))
            {
                ResamplerQuality = 60,
            };
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("無法初始化電腦音訊擷取。請確認有可用的播放裝置。", exception);
        }
    }

    public event EventHandler<Exception>? Failed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        try
        {
            _capture.StartRecording();
            _started = true;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("無法開始擷取電腦聲音。請確認預設播放裝置可正常使用。", exception);
        }
    }

    public byte[] ReadPacket()
    {
        lock (_sync)
        {
            if (_failure is not null)
            {
                throw new InvalidOperationException("錄影期間無法繼續擷取電腦聲音。", _failure);
            }

            var packet = new byte[BytesPerPacket];
            var bytesRead = _resampler.Read(packet, 0, packet.Length);
            if (bytesRead < packet.Length)
            {
                Array.Clear(packet, bytesRead, packet.Length - bytesRead);
            }

            AudioGain.ApplyToBuffer(packet, packet.Length, _gain);
            return packet;
        }
    }

    public void ClearBuffer()
    {
        lock (_sync)
        {
            _buffer.ClearBuffer();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping = true;
        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;
        if (_started)
        {
            _capture.StopRecording();
        }

        _resampler.Dispose();
        _capture.Dispose();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        if (_disposed || _control.IsPaused)
        {
            return;
        }

        lock (_sync)
        {
            if (!_disposed && !_control.IsPaused)
            {
                _buffer.AddSamples(args.Buffer, 0, args.BytesRecorded);
            }
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        if (_stopping)
        {
            return;
        }

        var exception = args.Exception ?? new InvalidOperationException("電腦音訊擷取意外停止。");
        lock (_sync)
        {
            _failure ??= exception;
        }

        Failed?.Invoke(this, exception);
    }
}
