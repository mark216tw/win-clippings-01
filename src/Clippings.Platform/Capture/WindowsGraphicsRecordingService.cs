using Clippings.Core.Models;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Clippings.Platform.Capture;

public sealed class WindowsGraphicsRecordingService
{
    private const int FramePoolBufferCount = 5;
    private const int MaximumRetainedCaptureFrames = FramePoolBufferCount - 1;
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
            ? new SystemAudioLoopbackCapture(control)
            : null;
        var source = CreateMediaSource(
            outputSize,
            options.FrameRate,
            options.IncludeSystemAudio);
        var pipeline = new RecordingPipeline(
            initialSize,
            options.FrameRate,
            () => EnsureFreeSpace(directory),
            control,
            audioCapture);

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
                    () => pipeline.SignalEndOfStream(RecordingStopReason.UserRequested));
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
        private readonly object _sync = new();
        private readonly SizeInt32 _initialSize;
        private readonly uint _frameRate;
        private readonly Action _healthCheck;
        private readonly long _healthCheckInterval;
        private readonly RecordingControl _control;
        private readonly SystemAudioLoopbackCapture? _audioCapture;
        private readonly SemaphoreSlim _videoRequestGate = new(1, 1);
        private readonly SemaphoreSlim _audioRequestGate = new(1, 1);
        private readonly TaskCompletionSource _endOfStreamSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstFrameReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Dictionary<Direct3D11CaptureFrame, int> _frameReferences = [];
        private readonly HashSet<Direct3D11CaptureFrame> _retiredFrames = [];
        private Direct3D11CaptureFrame? _latestFrame;
        private Direct3D11CaptureFrame? _currentFrame;
        private TaskCompletionSource? _frameAvailable;
        private long _nextSampleIndex;
        private long _nextAudioSampleIndex;
        private long _audioPauseGeneration;
        private Exception? _failure;
        private bool _endOfStream;

        public RecordingPipeline(
            SizeInt32 initialSize,
            uint frameRate,
            Action healthCheck,
            RecordingControl control,
            SystemAudioLoopbackCapture? audioCapture)
        {
            _initialSize = initialSize;
            _frameRate = frameRate;
            _healthCheck = healthCheck;
            _healthCheckInterval = frameRate * 5;
            _control = control;
            _audioCapture = audioCapture;
            _audioPauseGeneration = control.PauseGeneration;
            if (_audioCapture is not null)
            {
                _audioCapture.Failed += OnAudioCaptureFailed;
            }
        }

        public RecordingStopReason StopReason { get; private set; } = RecordingStopReason.UserRequested;

        public void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            Direct3D11CaptureFrame? frame = null;
            try
            {
                frame = sender.TryGetNextFrame();
                if (frame.ContentSize.Width != _initialSize.Width ||
                    frame.ContentSize.Height != _initialSize.Height)
                {
                    frame.Dispose();
                    SignalEndOfStream(RecordingStopReason.TargetResized);
                    return;
                }

                Direct3D11CaptureFrame? replaced;
                TaskCompletionSource? waiter;
                lock (_sync)
                {
                    if (_endOfStream)
                    {
                        replaced = frame;
                    }
                    else if (_latestFrame is not null)
                    {
                        replaced = _latestFrame;
                        _latestFrame = frame;
                        frame = null;
                    }
                    else if (_frameReferences.Count < MaximumRetainedCaptureFrames)
                    {
                        replaced = null;
                        _latestFrame = frame;
                        frame = null;
                    }
                    else
                    {
                        // Keep one frame-pool slot free so delayed encoder callbacks cannot stall WGC.
                        replaced = frame;
                    }

                    waiter = frame is null ? _frameAvailable : null;
                    if (waiter is not null)
                    {
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
            => SignalEndOfStream(RecordingStopReason.TargetClosed);

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

        public void SignalEndOfStream(RecordingStopReason reason)
        {
            Direct3D11CaptureFrame? latest;
            TaskCompletionSource? waiter;
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
            }

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
            SignalEndOfStream(StopReason);
            if (_audioCapture is not null)
            {
                _audioCapture.Failed -= OnAudioCaptureFailed;
            }

            List<Direct3D11CaptureFrame> frames = [];
            lock (_sync)
            {
                foreach (var (frame, references) in _frameReferences.ToArray())
                {
                    if (references == 0)
                    {
                        _frameReferences.Remove(frame);
                        _retiredFrames.Remove(frame);
                        frames.Add(frame);
                    }
                    else
                    {
                        _retiredFrames.Add(frame);
                    }
                }

                _currentFrame = null;
            }

            foreach (var frame in frames)
            {
                frame.Dispose();
            }
        }

        private async Task FulfillVideoSampleRequestAsync(
            MediaStreamSource source,
            MediaStreamSourceSampleRequest request,
            MediaStreamSourceSampleRequestDeferral deferral)
        {
            Direct3D11CaptureFrame? submittedFrame = null;
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

                var frame = await GetFrameForSampleAsync(sampleIndex, request).ConfigureAwait(false);
                if (frame is null)
                {
                    request.Sample = null;
                    return;
                }

                submittedFrame = frame;
                var timestamp = GetVideoTimestamp(sampleIndex);
                var sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, timestamp);
                sample.Duration = GetVideoTimestamp(sampleIndex + 1) - timestamp;

                void OnProcessed(MediaStreamSample sender, object args)
                {
                    sender.Processed -= OnProcessed;
                    ReleaseSampleReference(frame);
                }

                sample.Processed += OnProcessed;
                request.Sample = sample;
                submittedFrame = null;
            }
            catch (Exception exception)
            {
                if (submittedFrame is not null)
                {
                    ReleaseSampleReference(submittedFrame);
                }

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
                    request.Sample = null;
                    return;
                }

                var packet = _audioCapture?.ReadPacket()
                    ?? throw new InvalidOperationException("錄影音訊管線尚未初始化。");
                var timestamp = TimeSpan.FromMilliseconds(
                    sampleIndex * SystemAudioLoopbackCapture.PacketMilliseconds);
                var sample = MediaStreamSample.CreateFromBuffer(packet.AsBuffer(), timestamp);
                sample.Duration = TimeSpan.FromMilliseconds(SystemAudioLoopbackCapture.PacketMilliseconds);
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

        private async Task<Direct3D11CaptureFrame?> GetFrameForSampleAsync(
            long sampleIndex,
            MediaStreamSourceSampleRequest request)
        {
            if (sampleIndex > 0)
            {
                while (!_firstFrameReady.Task.IsCompleted)
                {
                    var firstFrameOrEnd = await Task.WhenAny(
                        _firstFrameReady.Task,
                        _endOfStreamSignal.Task,
                        Task.Delay(SampleProgressInterval)).ConfigureAwait(false);
                    if (firstFrameOrEnd == _endOfStreamSignal.Task)
                    {
                        return null;
                    }

                    if (firstFrameOrEnd != _firstFrameReady.Task)
                    {
                        request.ReportSampleProgress(0);
                    }
                }

                if (!await WaitUntilDueAsync(GetVideoTimestamp(sampleIndex), request).ConfigureAwait(false))
                {
                    return null;
                }
            }

            while (true)
            {
                Task waitTask;
                lock (_sync)
                {
                    if (_latestFrame is not null)
                    {
                        RetireCurrentFrameNoLock();
                        _currentFrame = _latestFrame;
                        _latestFrame = null;
                        _frameReferences.Add(_currentFrame, 0);
                        _control.Start();
                        _firstFrameReady.TrySetResult();
                    }

                    if (_endOfStream)
                    {
                        return null;
                    }

                    if (_currentFrame is not null)
                    {
                        _frameReferences[_currentFrame]++;
                        return _currentFrame;
                    }

                    _frameAvailable ??= new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    waitTask = _frameAvailable.Task;
                }

                var completed = await Task.WhenAny(
                    waitTask,
                    _endOfStreamSignal.Task,
                    Task.Delay(SampleProgressInterval)).ConfigureAwait(false);
                if (completed == _endOfStreamSignal.Task)
                {
                    return null;
                }

                if (completed != waitTask)
                {
                    request.ReportSampleProgress(0);
                }
            }
        }

        private TimeSpan GetVideoTimestamp(long sampleIndex)
            => TimeSpan.FromTicks(checked(sampleIndex * TimeSpan.TicksPerSecond / _frameRate));

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

        private void RetireCurrentFrameNoLock()
        {
            if (_currentFrame is null)
            {
                return;
            }

            if (_frameReferences[_currentFrame] == 0)
            {
                _frameReferences.Remove(_currentFrame);
                _currentFrame.Dispose();
            }
            else
            {
                _retiredFrames.Add(_currentFrame);
            }
        }

        private void ReleaseSampleReference(Direct3D11CaptureFrame frame)
        {
            var dispose = false;
            lock (_sync)
            {
                if (!_frameReferences.TryGetValue(frame, out var references))
                {
                    return;
                }

                references--;
                if (references == 0 && _retiredFrames.Remove(frame))
                {
                    _frameReferences.Remove(frame);
                    dispose = true;
                }
                else
                {
                    _frameReferences[frame] = references;
                }
            }

            if (dispose)
            {
                frame.Dispose();
            }
        }

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
