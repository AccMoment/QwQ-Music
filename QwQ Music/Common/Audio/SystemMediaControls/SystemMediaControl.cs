using QwQ_Music.Common.Managers;

namespace QwQ_Music.Common.Audio.SystemMediaControls;

public class SystemMediaControlEventArgs : EventArgs;

public class PlaybackModeChangedEventArgs(MediaPlaybackMode mode) : SystemMediaControlEventArgs {
    public readonly MediaPlaybackMode Mode = mode;
}

public class PlaybackStatusChangedEventArgs(MediaPlaybackStatus status) : SystemMediaControlEventArgs {
    public readonly MediaPlaybackStatus Status = status;
}

public class PlaybackPositionChangedEventArgs(TimeSpan position) : SystemMediaControlEventArgs {
    public readonly TimeSpan Position = position;
}

public static partial class SystemMediaControl {
    public const string APPID = "com.Mioter.QwQMusic";
    public static ISystemMediaControlImpl CreateSystemMediaControl() {
#if _WIN_NT
        return new WindowsMediaControlImpl();
#else
        return new LinuxMediaControlImpl();
#endif
    }
}

public interface ISystemMediaControlImpl : IDisposable {
    void UpdateInfo(object? sender, MusicItemChangedEventArgs model);
    double PlaybackSpeed { get; set; }
    double Volume { get; set; }
    TimeSpan Position { get; set; }
    TimeSpan Duration { get; set; }
    MediaPlaybackStatus Status { get; set; }
    bool ShuffleEnabled { get; set; }
    MediaPlaybackMode Mode { get; set; }
    bool IsPlayEnabled { get; set; }
    bool IsPauseEnabled { get; set; }
    bool IsPreviousEnabled { get; set; }
    bool IsNextEnabled { get; set; }
    bool IsStopEnabled { get; set; }
    event EventHandler? PlayRequested;
    event EventHandler? PauseRequested;
    event EventHandler? NextRequested;
    event EventHandler? PreviousRequested;
    event EventHandler? StopRequested;

    event EventHandler<PlaybackPositionChangedEventArgs>? SeekRequested;
    // event EventHandler<SystemMediaControlEventArgs> 
}