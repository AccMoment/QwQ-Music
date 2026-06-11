#if _LINUX

using Tmds.DBus;

namespace SystemMediaInterop.PlatformImpl;

public class LinuxMediaControlImpl : ISystemMediaControlImpl {
    public Task UpdateInfoAsync(IMediaItem model) { return Task.CompletedTask; }
    public double PlaybackSpeed { get; set; }
    public double Rate { get; set; }
    public double Volume { get; set; }
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }
    public MediaPlaybackStatus Status { get; set; }

    public bool ShuffleEnabled { get; set; }

    // public MediaPlaybackMode Mode { get; set; }
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

[DBusInterface("org.mpris.MediaPlayer2")]
public interface IMediaPlayer2 : IDBusObject {
    Task<string> GetIdentityAsync();
    Task<bool> GetCanQuitAsync();
    Task QuitAsync();
}

[DBusInterface("org.mpris.MediaPlayer2.Player")]
public interface IMediaPlayer2Player : IDBusObject {
    Task<string> GetPlaybackStatusAsync();
    Task SetPlaybackStatusAsync(string value);
    Task<IDictionary<string, object>> GetMetadataAsync();
    Task SetMetadataAsync(IDictionary<string, object> value);
    Task<bool> GetCanPlayAsync();
    Task<bool> GetCanPauseAsync();
    Task PlayAsync();
    Task PauseAsync();
}

#endif