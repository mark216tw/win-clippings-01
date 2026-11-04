using Clippings.Core.Models;
using Clippings.Platform.Interop;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace Clippings.Platform.Capture;

public sealed class WindowsGraphicsRecordingService
{
    private const int FramePoolBufferCount = 5;
    private static readonly Guid DefaultStrideProperty = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    private static readonly TimeSpan SampleProgressInterval = TimeSpan.FromMilliseconds(500);
    private const long MinimumOngoingFreeSpaceBytes = 64L * 1024 * 1024;

    public bool IsSupported =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362) &&
        GraphicsCaptureSession.IsSupported();

    public Task<RecordingResult> RecordWindowAsync(
        IntPtr window,
        RecordingOptions options,
        RecordingControl control,
        CancellationToken stopToken,
        Func<CancellationToken, Task>? waitBeforeStartAsync = null,
        Action? recordingStarted = null)
    {
        Validate(options, control);
        return RecordItemAsync(
            WindowsGraphicsCaptureService.CreateItem(window),
            window,
            isWindowTarget: true,
            options,
            control,
            stopToken,
            waitBeforeStartAsync,
            recordingStarted);
    }

    public Task<RecordingResult> RecordMonitorAsync(
        IntPtr monitor,
        RecordingOptions options,
        RecordingControl control,
        CancellationToken stopToken,
        Func<CancellationToken, Task>? waitBeforeStartAsync = null,
        Action? recordingStarted = null)
    {
        Validate(options, control);
        return RecordItemAsync(
            WindowsGraphicsCaptureService.CreateItemForMonitor(monitor),
            IntPtr.Zero,
            isWindowTarget: false,
            options,
            control,
            stopToken,
            waitBeforeStartAsync,
            recordingStarted);
    }

    private void Validate(RecordingOptions options, RecordingControl control)
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("目前的 Windows 版本不支援螢幕錄影。");
        }

        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(control);
        if (options.FrameRate == 0 || options.Bitrate == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "錄影幀率與位元率必須大於零。");
        }

        var directory = Path.GetDirectoryName(options.OutputPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("錄影輸出路徑必須包含資料夾。", nameof(options));
        }
    }

    private async Task<RecordingResult> RecordItemAsync(
        GraphicsCaptureItem item,
        IntPtr windowHandle,
        bool isWindowTarget,
        RecordingOptions options,
        RecordingControl control,
        CancellationToken stopToken,
        Func<CancellationToken, Task>? waitBeforeStartAsync,
        Action? recordingStarted)
    {
        var directory = Path.GetDirectoryName(options.OutputPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = options.OutputPath + ".recording.tmp";
        TryDelete(temporaryPath);

        var initialSize = item.Size;
        var outputSize = GetEvenSize(initialSize);
        using var device = WindowsGraphicsCaptureService.CreateDevice();
        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            device,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            FramePoolBufferCount,
            outputSize);
        using var session = framePool.CreateCaptureSession(item);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            session.IsCursorCaptureEnabled = options.IncludeCursor;
        }

        using var audioCapture = options.IncludeSystemAudio
            ? new SystemAudioLoopbackCapture(control, options.SystemAudioGain)
            : null;
        var source = CreateMediaSource(
            outputSize,
            options.FrameRate,
            options.IncludeSystemAudio);
        var pipeline = new RecordingPipeline(
            initialSize,
            outputSize,
            options.FrameRate,
            () => EnsureFreeSpace(directory),
            control,
            audioCapture,
            windowHandle,
            isWindowTarget);

        framePool.FrameArrived += pipeline.OnFrameArrived;
        item.Closed += pipeline.OnTargetClosed;
        source.Starting += pipeline.OnSourceStarting;
        source.SampleRequested += pipeline.OnSampleRequested;
        source.Closed += pipeline.OnSourceClosed;

        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(directory);
            var temporaryFile = await folder.CreateFileAsync(
                Path.GetFileName(temporaryPath),
                CreationCollisionOption.ReplaceExisting);

            using (var output = await temporaryFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                var transcoder = new MediaTranscoder();
                var profile = CreateProfile(
                    outputSize,
                    options.FrameRate,
                    options.Bitrate,
                    options.IncludeSystemAudio);
                var prepared = await transcoder.PrepareMediaStreamSourceTranscodeAsync(source, output, profile);
                if (!prepared.CanTranscode)
                {
                    throw new InvalidOperationException($"無法建立 H.264 編碼器：{prepared.FailureReason}");
                }

                using var registration = stopToken.Register(
                    () => pipeline.RequestStop(RecordingStopReason.UserRequested));
                if (waitBeforeStartAsync is not null)
                {
                    await waitBeforeStartAsync(stopToken).ConfigureAwait(false);
                }

                stopToken.ThrowIfCancellationRequested();
                control.Start();
                audioCapture?.Start();
                session.StartCapture();
                recordingStarted?.Invoke();
                await prepared.TranscodeAsync();
            }

            pipeline.ThrowIfFailed();
            File.Move(temporaryPath, options.OutputPath, overwrite: true);
            return new RecordingResult(options.OutputPath, pipeline.StopReason);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
        finally
        {
            framePool.FrameArrived -= pipeline.OnFrameArrived;
            item.Closed -= pipeline.OnTargetClosed;
            source.Starting -= pipeline.OnSourceStarting;
            source.SampleRequested -= pipeline.OnSampleRequested;
            source.Closed -= pipeline.OnSourceClosed;
            pipeline.Dispose();
        }
    }

    private static MediaStreamSource CreateMediaSource(
        SizeInt32 size,
        uint frameRate,
        bool includeSystemAudio)
    {
        var properties = VideoEncodingProperties.CreateUncompressed(
            MediaEncodingSubtypes.Bgra8,
            checked((uint)size.Width),
            checked((uint)size.Height));
        properties.FrameRate.Numerator = frameRate;
        properties.FrameRate.Denominator = 1;
        properties.Properties[DefaultStrideProperty] = checked((uint)size.Width * 4u);

        var videoDescriptor = new VideoStreamDescriptor(properties);
        MediaStreamSource source;
        if (includeSystemAudio)
        {
            var audioProperties = AudioEncodingProperties.CreatePcm(
                SystemAudioLoopbackCapture.SampleRate,
                SystemAudioLoopbackCapture.ChannelCount,
                SystemAudioLoopbackCapture.BitsPerSample);
            var audioDescriptor = new AudioStreamDescriptor(audioProperties);
            source = new MediaStreamSource(videoDescriptor, audioDescriptor);
        }
        else
        {
            source = new MediaStreamSource(videoDescriptor);
        }

        source.BufferTime = TimeSpan.Zero;
        source.CanSeek = false;
        source.IsLive = true;
        return source;
    }

    private static MediaEncodingProfile CreateProfile(
        SizeInt32 size,
        uint frameRate,
        uint bitrate,
        bool includeSystemAudio)
    {
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
        profile.Audio = includeSystemAudio
            ? AudioEncodingProperties.CreateAac(
                SystemAudioLoopbackCapture.SampleRate,
                SystemAudioLoopbackCapture.ChannelCount,
                192_000)
            : null;
        profile.Video.Subtype = MediaEncodingSubtypes.H264;
        profile.Video.Width = checked((uint)size.Width);
        profile.Video.Height = checked((uint)size.Height);
        profile.Video.FrameRate.Numerator = frameRate;
        profile.Video.FrameRate.Denominator = 1;
        profile.Video.Bitrate = bitrate;
        return profile;
    }

    private static SizeInt32 GetEvenSize(SizeInt32 size)
    {
        var width = size.Width & ~1;
        var height = size.Height & ~1;
        if (width < 2 || height < 2)
        {
            throw new InvalidOperationException("擷取範圍太小，無法錄影。");
        }

        return new SizeInt32(width, height);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void EnsureFreeSpace(string directory)
    {
        var root = Path.GetPathRoot(directory);
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        try
        {
            var drive = new DriveInfo(root);
            if (drive.IsReady && drive.AvailableFreeSpace < MinimumOngoingFreeSpaceBytes)
            {
                throw new IOException("可用磁碟空間少於 64 MB，錄影已停止。");
            }
        }
        catch (ArgumentException)
        {
        }
    }

    private sealed class RecordingPipeline : IDisposable
    {
        private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MinimizePollInterval = TimeSpan.FromMilliseconds(250);

        private readonly object _sync = new();
        private readonly SizeInt32 _initialSize;
        private readonly SizeInt32 _outputSize;
        private readonly uint _frameRate;
        private readonly Action _healthCheck;
        private readonly long _healthCheckInterval;
        private readonly RecordingControl _control;
        private readonly SystemAudioLoopbackCapture? _audioCapture;
        private readonly IntPtr _windowHandle;
        private readonly bool _isWindowTarget;
        private readonly SemaphoreSlim _videoRequestGate = new(1, 1);
        private readonly SemaphoreSlim _audioRequestGate = new(1, 1);
        private readonly TaskCompletionSource _endOfStreamSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Timer? _minimizeTimer;
        private Direct3D11CaptureFrame? _latestFrame;
        private TaskCompletionSource? _frameAvailable;
        private Windows.Storage.Streams.Buffer? _lastFrameBuffer;
        private Timer? _drainTimer;
        private long _nextSampleIndex;
        private long _nextAudioSampleIndex;
        private long _audioPauseGeneration;
        private Exception? _failure;
        private bool _stopRequested;
        private bool _endOfStream;
        private bool _videoEnded;
        private bool _audioEnded;
        private TimeSpan? _stopAt;

        public RecordingPipeline(
            SizeInt32 initialSize,
            SizeInt32 outputSize,
            uint frameRate,
            Action healthCheck,
            RecordingControl control,
            SystemAudioLoopbackCapture? audioCapture,
            IntPtr windowHandle,
            bool isWindowTarget)
        {
            _initialSize = initialSize;
            _outputSize = outputSize;
            _frameRate = frameRate;
            _healthCheck = healthCheck;
            _healthCheckInterval = frameRate * 5;
            _control = control;
            _audioCapture = audioCapture;
            _windowHandle = windowHandle;
            _isWindowTarget = isWindowTarget;
            _audioPauseGeneration = control.PauseGeneration;
            _audioEnded = audioCapture is null;
            if (_audioCapture is not null)
            {
                _audioCapture.Failed += OnAudioCaptureFailed;
            }

            if (_isWindowTarget && _windowHandle != IntPtr.Zero)
            {
                _minimizeTimer = new Timer(
                    _ => CheckMinimized(),
                    null,
                    MinimizePollInterval,
                    MinimizePollInterval);
            }
        }

        public RecordingStopReason StopReason { get; private set; } = RecordingStopReason.UserRequested;

        public void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            Direct3D11CaptureFrame? frame = null;
            try
            {
                frame = sender.TryGetNextFrame();
                if (frame is null)
                {
                    return;
                }

                if (_isWindowTarget &&
                    _windowHandle != IntPtr.Zero &&
                    NativeMethods.IsIconic(_windowHandle))
                {
                    frame.Dispose();
                    frame = null;
                    RequestStop(RecordingStopReason.TargetMinimized);
                    return;
                }

                var contentSize = frame.ContentSize;
                if (contentSize.Width <= 0 || contentSize.Height <= 0)
                {
                    frame.Dispose();
                    frame = null;
                    return;
                }

                if (contentSize.Width != _initialSize.Width ||
                    contentSize.Height != _initialSize.Height)
                {
                    frame.Dispose();
                    frame = null;
                    RequestStop(RecordingStopReason.TargetResized);
                    return;
                }

                Direct3D11CaptureFrame? replaced = null;
                TaskCompletionSource? waiter = null;
                lock (_sync)
                {
                    if (_endOfStream || _stopRequested)
                    {
                        replaced = frame;
                        frame = null;
                    }
                    else
                    {
                        replaced = _latestFrame;
                        _latestFrame = frame;
                        frame = null;
                        waiter = _frameAvailable;
                        _frameAvailable = null;
                    }
                }

                replaced?.Dispose();
                waiter?.TrySetResult();
            }
            catch (Exception exception)
            {
                frame?.Dispose();
                Fail(exception);
            }
        }

        public void OnTargetClosed(GraphicsCaptureItem sender, object args)
            => RequestStop(RecordingStopReason.TargetClosed);

        public void OnSourceStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
            => args.Request.SetActualStartPosition(TimeSpan.Zero);

        public void OnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            var request = args.Request;
            var deferral = request.GetDeferral();
            _ = request.StreamDescriptor is AudioStreamDescriptor
                ? FulfillAudioSampleRequestAsync(sender, request, deferral)
                : FulfillVideoSampleRequestAsync(sender, request, deferral);
        }

        public void OnSourceClosed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
            => SignalEndOfStream(StopReason);

        public void RequestStop(RecordingStopReason reason)
        {
            lock (_sync)
            {
                if (_endOfStream)
                {
                    return;
                }

                if (!_stopRequested)
                {
                    _stopRequested = true;
                    StopReason = reason;
                    _stopAt = _control.Elapsed;
                    _drainTimer = new Timer(
                        _ => SignalEndOfStream(StopReason),
                        null,
                        DrainTimeout,
                        Timeout.InfiniteTimeSpan);
                }
                else if (reason == RecordingStopReason.TargetMinimized
                         && StopReason == RecordingStopReason.TargetResized)
                {
                    StopReason = reason;
                }
            }

            _control.ReleaseWaiters();
        }

        public void SignalEndOfStream(RecordingStopReason reason)
        {
            Direct3D11CaptureFrame? latest;
            TaskCompletionSource? waiter;
            Timer? drainTimer;
            lock (_sync)
            {
                if (_endOfStream)
                {
                    return;
                }

                _endOfStream = true;
                StopReason = reason;
                latest = _latestFrame;
                _latestFrame = null;
                waiter = _frameAvailable;
                _frameAvailable = null;
                drainTimer = _drainTimer;
                _drainTimer = null;
            }

            drainTimer?.Dispose();
            latest?.Dispose();
            waiter?.TrySetResult();
            _control.ReleaseWaiters();
            _endOfStreamSignal.TrySetResult();
        }

        public void ThrowIfFailed()
        {
            if (_failure is not null)
            {
                throw new InvalidOperationException("錄影媒體處理失敗。", _failure);
            }
        }

        public void Dispose()
        {
            _minimizeTimer?.Dispose();
            SignalEndOfStream(StopReason);
            if (_audioCapture is not null)
            {
                _audioCapture.Failed -= OnAudioCaptureFailed;
            }

            lock (_sync)
            {
                _lastFrameBuffer = null;
            }
        }

        private void CheckMinimized()
        {
            if (!_isWindowTarget || _windowHandle == IntPtr.Zero)
            {
                return;
            }

            lock (_sync)
            {
                if (_endOfStream || _stopRequested)
                {
                    return;
                }
            }

            if (NativeMethods.IsIconic(_windowHandle))
            {
                RequestStop(RecordingStopReason.TargetMinimized);
            }
        }

        private async Task FulfillVideoSampleRequestAsync(
            MediaStreamSource source,
            MediaStreamSourceSampleRequest request,
            MediaStreamSourceSampleRequestDeferral deferral)
        {
            var gateAcquired = false;
            try
            {
                gateAcquired = await WaitForRequestGateAsync(_videoRequestGate, request).ConfigureAwait(false);
                if (!gateAcquired || !await WaitWhilePausedAsync(request).ConfigureAwait(false))
                {
                    request.Sample = null;
                    return;
                }

                var sampleIndex = Interlocked.Increment(ref _nextSampleIndex) - 1;
                if (sampleIndex > 0 && sampleIndex % _healthCheckInterval == 0)
                {
                    _healthCheck();
                }

                var timestamp = GetVideoTimestamp(sampleIndex);
                if (IsPastStop(timestamp))
                {
                    MarkStreamEnded(isVideo: true);
                    request.Sample = null;
                    return;
                }

                if (!await WaitUntilDueAsync(timestamp, request).ConfigureAwait(false))
                {
                    request.Sample = null;
                    return;
                }

                if (!await EnsureVideoBufferAsync(request).ConfigureAwait(false))
                {
                    request.Sample = null;
                    return;
                }

                Windows.Storage.Streams.Buffer? buffer;
                lock (_sync)
                {
                    buffer = _lastFrameBuffer;
                }

                if (buffer is null)
                {
                    request.Sample = null;
                    return;
                }

                var duration = GetVideoTimestamp(sampleIndex + 1) - timestamp;
                if (TryGetStopAt(out var stopAt) && timestamp + duration > stopAt)
                {
                    duration = stopAt - timestamp;
                }

                if (duration <= TimeSpan.Zero)
                {
                    MarkStreamEnded(isVideo: true);
                    request.Sample = null;
                    return;
                }

                var sample = MediaStreamSample.CreateFromBuffer(buffer, timestamp);
                sample.Duration = duration;
                request.Sample = sample;
            }
            catch (Exception exception)
            {
                Fail(exception);
                source.NotifyError(MediaStreamSourceErrorStatus.Other);
            }
            finally
            {
                if (gateAcquired)
                {
                    _videoRequestGate.Release();
                }

                deferral.Complete();
            }
        }

        private async Task FulfillAudioSampleRequestAsync(
            MediaStreamSource source,
            MediaStreamSourceSampleRequest request,
            MediaStreamSourceSampleRequestDeferral deferral)
        {
            var gateAcquired = false;
            try
            {
                gateAcquired = await WaitForRequestGateAsync(_audioRequestGate, request).ConfigureAwait(false);
                if (!gateAcquired)
                {
                    request.Sample = null;
                    return;
                }

                var sampleIndex = Interlocked.Increment(ref _nextAudioSampleIndex) - 1;
                if (!await WaitForAudioSampleTimeAsync(sampleIndex, request).ConfigureAwait(false))
                {
                    MarkStreamEnded(isVideo: false);
                    request.Sample = null;
                    return;
                }

                var packet = _audioCapture?.ReadPacket()
                    ?? throw new InvalidOperationException("錄影音訊管線尚未初始化。");
                var packetDuration = TimeSpan.FromMilliseconds(
                    SystemAudioLoopbackCapture.PacketMilliseconds);
                var timestamp = TimeSpan.FromMilliseconds(
                    sampleIndex * SystemAudioLoopbackCapture.PacketMilliseconds);
                var duration = packetDuration;
                if (TryGetStopAt(out var stopAt) && timestamp + duration > stopAt)
                {
                    var remaining = stopAt - timestamp;
                    if (remaining <= TimeSpan.Zero)
                    {
                        MarkStreamEnded(isVideo: false);
                        request.Sample = null;
                        return;
                    }

                    var keepBytes = (int)Math.Clamp(
                        remaining.TotalMilliseconds
                        / SystemAudioLoopbackCapture.PacketMilliseconds
                        * packet.Length,
                        0,
                        packet.Length);
                    keepBytes &= ~1;
                    Array.Clear(packet, keepBytes, packet.Length - keepBytes);
                    duration = remaining;
                }

                var sample = MediaStreamSample.CreateFromBuffer(packet.AsBuffer(), timestamp);
                sample.Duration = duration;
                request.Sample = sample;
            }
            catch (Exception exception)
            {
                Fail(exception);
                source.NotifyError(MediaStreamSourceErrorStatus.Other);
            }
            finally
            {
                if (gateAcquired)
                {
                    _audioRequestGate.Release();
                }

                deferral.Complete();
            }
        }

        private async Task<bool> EnsureVideoBufferAsync(MediaStreamSourceSampleRequest request)
        {
            while (true)
            {
                Direct3D11CaptureFrame? frame;
                Task? waitTask = null;
                lock (_sync)
                {
                    if (_endOfStream)
                    {
                        return false;
                    }

                    frame = _latestFrame;
                    _latestFrame = null;
                    if (frame is null && _lastFrameBuffer is not null)
                    {
                        return true;
                    }

                    if (frame is null)
                    {
                        _frameAvailable ??= new TaskCompletionSource(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                        waitTask = _frameAvailable.Task;
                    }
                }

                if (frame is not null)
                {
                    try
                    {
                        return await CopyFrameToBufferAsync(frame).ConfigureAwait(false);
                    }
                    finally
                    {
                        frame.Dispose();
                    }
                }

                var completed = await Task.WhenAny(
                    waitTask!,
                    _endOfStreamSignal.Task,
                    Task.Delay(SampleProgressInterval)).ConfigureAwait(false);
                if (completed == _endOfStreamSignal.Task)
                {
                    return false;
                }

                if (completed != waitTask)
                {
                    request.ReportSampleProgress(0);
                }
            }
        }

        private async Task<bool> CopyFrameToBufferAsync(Direct3D11CaptureFrame frame)
        {
            using var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(
                frame.Surface,
                BitmapAlphaMode.Ignore).AsTask().ConfigureAwait(false);
            var length = checked(bitmap.PixelWidth * bitmap.PixelHeight * 4);
            if (bitmap.PixelWidth != _outputSize.Width || bitmap.PixelHeight != _outputSize.Height)
            {
                throw new InvalidOperationException("擷取畫面尺寸與輸出尺寸不一致。");
            }

            var buffer = new Windows.Storage.Streams.Buffer((uint)length);
            bitmap.CopyToBuffer(buffer);
            lock (_sync)
            {
                if (_endOfStream)
                {
                    return false;
                }

                _lastFrameBuffer = buffer;
            }

            return true;
        }

        private async Task<bool> WaitForAudioSampleTimeAsync(
            long sampleIndex,
            MediaStreamSourceSampleRequest request)
        {
            var due = TimeSpan.FromMilliseconds(
                sampleIndex * SystemAudioLoopbackCapture.PacketMilliseconds);
            while (true)
            {
                if (!await WaitWhilePausedAsync(request).ConfigureAwait(false))
                {
                    return false;
                }

                if (IsPastStop(due))
                {
                    return false;
                }

                var pauseGeneration = _control.PauseGeneration;
                if (pauseGeneration != _audioPauseGeneration)
                {
                    _audioCapture?.ClearBuffer();
                    _audioPauseGeneration = pauseGeneration;
                }

                var delay = due - _control.Elapsed;
                if (delay <= TimeSpan.Zero)
                {
                    return true;
                }

                var completed = await Task.WhenAny(
                    Task.Delay(Min(delay, SampleProgressInterval)),
                    _endOfStreamSignal.Task).ConfigureAwait(false);
                if (completed == _endOfStreamSignal.Task)
                {
                    return false;
                }

                if (delay > SampleProgressInterval)
                {
                    request.ReportSampleProgress(0);
                }
            }
        }

        private TimeSpan GetVideoTimestamp(long sampleIndex)
            => TimeSpan.FromTicks(checked(sampleIndex * TimeSpan.TicksPerSecond / _frameRate));

        private bool IsPastStop(TimeSpan timestamp)
        {
            lock (_sync)
            {
                return _stopAt is { } stop && timestamp >= stop;
            }
        }

        private bool TryGetStopAt(out TimeSpan stopAt)
        {
            lock (_sync)
            {
                if (_stopAt is { } value)
                {
                    stopAt = value;
                    return true;
                }
            }

            stopAt = default;
            return false;
        }

        private void MarkStreamEnded(bool isVideo)
        {
            bool complete;
            lock (_sync)
            {
                if (isVideo)
                {
                    _videoEnded = true;
                }
                else
                {
                    _audioEnded = true;
                }

                complete = _videoEnded && _audioEnded;
            }

            if (complete)
            {
                SignalEndOfStream(StopReason);
            }
        }

        private async Task<bool> WaitForRequestGateAsync(
            SemaphoreSlim gate,
            MediaStreamSourceSampleRequest request)
        {
            while (!_endOfStreamSignal.Task.IsCompleted)
            {
                if (await gate.WaitAsync(SampleProgressInterval).ConfigureAwait(false))
                {
                    return true;
                }

                request.ReportSampleProgress(0);
            }

            return false;
        }

        private async Task<bool> WaitWhilePausedAsync(MediaStreamSourceSampleRequest request)
        {
            while (true)
            {
                var pauseTask = _control.WaitWhilePausedAsync();
                if (pauseTask.IsCompleted)
                {
                    return !_endOfStreamSignal.Task.IsCompleted;
                }

                var completed = await Task.WhenAny(
                    pauseTask,
                    _endOfStreamSignal.Task,
                    Task.Delay(SampleProgressInterval)).ConfigureAwait(false);
                if (completed == _endOfStreamSignal.Task)
                {
                    return false;
                }

                if (completed != pauseTask)
                {
                    request.ReportSampleProgress(0);
                }
            }
        }

        private async Task<bool> WaitUntilDueAsync(
            TimeSpan due,
            MediaStreamSourceSampleRequest? request)
        {
            while (true)
            {
                if (request is not null && !await WaitWhilePausedAsync(request).ConfigureAwait(false))
                {
                    return false;
                }

                var delay = due - _control.Elapsed;
                if (delay <= TimeSpan.Zero)
                {
                    return true;
                }

                var completed = await Task.WhenAny(
                    Task.Delay(Min(delay, SampleProgressInterval)),
                    _endOfStreamSignal.Task).ConfigureAwait(false);
                if (completed == _endOfStreamSignal.Task)
                {
                    return false;
                }

                if (delay > SampleProgressInterval)
                {
                    request?.ReportSampleProgress(0);
                }
            }
        }

        private static TimeSpan Min(TimeSpan left, TimeSpan right)
            => left <= right ? left : right;

        private void Fail(Exception exception)
        {
            lock (_sync)
            {
                _failure ??= exception;
            }

            SignalEndOfStream(StopReason);
        }

        private void OnAudioCaptureFailed(object? sender, Exception exception)
            => Fail(exception);
    }
}
