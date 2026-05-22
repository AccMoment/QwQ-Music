#if _WIN_NT
#else
using QwQ_Music.Common.Managers;

namespace QwQ_Music.Common.Audio.SystemMediaControls;

public static partial class SystemMediaControl {
    public static void SetProcessInfoId() { }
}

public class LinuxMediaControlImpl : ISystemMediaControlImpl {
    public void UpdateInfo(object? sender, MusicItemChangedEventArgs model) { throw new NotImplementedException(); }

    public double PlaybackSpeed { get; set; }
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

    public event EventHandler? PlayRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? PreviousRequested;
    public event EventHandler? StopRequested;
    public event EventHandler<PlaybackPositionChangedEventArgs>? SeekRequested;

    public void Dispose() {
        //TODO
        GC.SuppressFinalize(this);
    }
}
#endif