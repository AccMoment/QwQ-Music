#pragma warning disable CS0067 // Event is never used
namespace SystemMediaInterop;

public class UnsupportedSystemMediaControlImpl : ISystemMediaControlImpl {
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

    public void Dispose() { }
}