namespace SystemMediaInterop;

public interface IMediaItem {
    string Title { get; set; }
    string Artists { get; set; }
    string Album { get; set; }
    Stream ThumbnailStream { get; }
    
    TimeSpan Duration { get; set; }
}

public interface IMediaItemWrapper {
    IMediaItem MediaItem { get; }
}