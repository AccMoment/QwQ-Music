namespace SystemMediaInterop;

public interface ISystemMediaControlImpl : IDisposable {
    async Task UpdateInfoAsync(IMediaItemWrapper model) { await UpdateInfoAsync(model.MediaItem); }
    Task UpdateInfoAsync(IMediaItem model);
    double PlaybackSpeed { get; set; }
    double Volume { get; set; }
    TimeSpan Position { get; set; }
    TimeSpan Duration { get; set; }
    MediaPlaybackStatus Status { get; set; }

    bool ShuffleEnabled { get; set; }

    // MediaPlaybackMode Mode { get; set; }
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