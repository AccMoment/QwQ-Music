using System;
#if _WIN_NT
using Windows.Media;
using Windows.Media.Playback;
#endif


namespace QwQ_Music.Common.Audio;

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

public static class SystemMediaControl {
    public static ISystemMediaControlImpl CreateSystemMediaControl() {
#if _WIN_NT
        return new WindowsMediaControlImpl();
#else
        return new LinuxMediaControlImpl();
#endif
    }
}

public interface ISystemMediaControlImpl : IDisposable {
    double Rate { get; set; }
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
    // event EventHandler<SystemMediaControlEventArgs> 
}
#if _WIN_NT
public class WindowsMediaControlImpl : ISystemMediaControlImpl {
    private static readonly MediaPlayer _player;

    static WindowsMediaControlImpl() {
        _player = new MediaPlayer();
        _player.CommandManager.IsEnabled = false;
    }

    private static SystemMediaTransportControls Control => _player.SystemMediaTransportControls;

    public double Rate {
        get => Control.PlaybackRate;
        set => Control.PlaybackRate = value;
    }

    public double Volume { get; set; }

    public TimeSpan Position {
        get;
        set {
            field = value;
            Control.UpdateTimelineProperties(
                new SystemMediaTransportControlsTimelineProperties {
                    Position = value,
                    StartTime = TimeSpan.Zero,
                    EndTime = Duration,
                    MaxSeekTime = Duration,
                    MinSeekTime = TimeSpan.Zero
                });
        }
    }

    public TimeSpan Duration {
        get;
        set {
            field = value;
            Control.UpdateTimelineProperties(
                new SystemMediaTransportControlsTimelineProperties {
                    Position = Position,
                    StartTime = TimeSpan.Zero,
                    EndTime = value,
                    MaxSeekTime = value,
                    MinSeekTime = TimeSpan.Zero
                });
        }
    }

    public MediaPlaybackStatus Status {
        get => StatusConverter.Convert(Control.PlaybackStatus);
        set => Control.PlaybackStatus = StatusConverter.Convert(value);
    }

    public bool ShuffleEnabled {
        get => false;
        set => throw new InvalidOperationException();
    }

    public MediaPlaybackMode Mode { get; set; }
    public bool IsPlayEnabled { get; set; }
    public bool IsPauseEnabled { get; set; }
    public bool IsPreviousEnabled { get; set; }
    public bool IsNextEnabled { get; set; }
    public bool IsStopEnabled { get; set; }

    public void Dispose() { _player.Dispose(); }
}

public static class StatusConverter {
    public static MediaPlaybackStatus Convert(global::Windows.Media.MediaPlaybackStatus status) {
        return status switch {
            global::Windows.Media.MediaPlaybackStatus.Changing => MediaPlaybackStatus.Changing,
            global::Windows.Media.MediaPlaybackStatus.Playing  => MediaPlaybackStatus.Playing,
            global::Windows.Media.MediaPlaybackStatus.Paused   => MediaPlaybackStatus.Paused,
            global::Windows.Media.MediaPlaybackStatus.Stopped or global::Windows.Media.MediaPlaybackStatus.Closed => MediaPlaybackStatus
                .Stopped,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    public static global::Windows.Media.MediaPlaybackStatus Convert(MediaPlaybackStatus status) {
        return status switch {
            MediaPlaybackStatus.Changing => global::Windows.Media.MediaPlaybackStatus.Changing,
            MediaPlaybackStatus.Playing  => global::Windows.Media.MediaPlaybackStatus.Playing,
            MediaPlaybackStatus.Paused   => global::Windows.Media.MediaPlaybackStatus.Paused,
            MediaPlaybackStatus.Stopped  => global::Windows.Media.MediaPlaybackStatus.Stopped,
            _                            => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}
#else
public class LinuxMediaControlImpl : ISystemMediaControlImpl {
    public double Rate { get; set; }
    public double Volume { get; set; }
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }
    public MediaPlaybackStatus Status { get; set; }
    public bool ShuffleEnabled { get; set; }
    public MediaPlaybackMode Mode { get; set; }
    public bool IsPlayEnabled { get; set; }
    public bool IsPauseEnabled { get; set; }
    public bool IsPreviousEnabled { get; set; }
    public bool IsNextEnabled { get; set; }
    public bool IsStopEnabled { get; set; }

    public void Dispose() {
        //TODO
        GC.SuppressFinalize(this);
    }
}
#endif