namespace Clippings.Core.Models;

public sealed record RecordingOptions(
    string OutputPath,
    uint FrameRate = 30,
    uint Bitrate = 8_000_000,
    bool IncludeCursor = true,
    bool IncludeSystemAudio = false,
    double SystemAudioGain = 2.0);

public sealed class RecordingSettings
{
    public string OutputDirectory { get; set; } = string.Empty;

    public int FrameRate { get; set; } = 30;

    public int Bitrate { get; set; } = 8_000_000;

    public bool IncludeCursor { get; set; } = true;

    public bool IncludeSystemAudio { get; set; }

    public double SystemAudioGain { get; set; } = 2.0;

    public int DelaySeconds { get; set; }

    public RecordingSettings Clone() => (RecordingSettings)MemberwiseClone();
}

public enum RecordingState
{
    Idle = 0,
    Starting = 1,
    Recording = 2,
    Stopping = 3,
    Countdown = 4,
    Paused = 5,
}

public enum RecordingStopReason
{
    UserRequested = 0,
    TargetClosed = 1,
    TargetResized = 2,
    TargetMinimized = 3,
}

public sealed record RecordingResult(string OutputPath, RecordingStopReason StopReason);
