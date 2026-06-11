namespace SystemMediaInterop;

public class SystemMediaControlEventArgs : EventArgs;

public class PlaybackPositionChangedEventArgs(TimeSpan position) : SystemMediaControlEventArgs {
    public readonly TimeSpan Position = position;
}

public enum MediaPlaybackStatus {
    Changing, Playing, Paused, Stopped
}

public static class SystemMediaControl {
    public static string AppId {
        get =>
            field ?? throw new NullReferenceException("You have to set AppId before use SystemMediaControl Instance.");
        set;
    }

    public static ISystemMediaControlImpl Instance { get; } = CreateSystemMediaControl();

    public static ISystemMediaControlImpl CreateSystemMediaControl() {
#if _WIN_NT
        return new WindowsMediaControlImpl();
#elif _LINUX
        return new LinuxMediaControlImpl();
#elif _OSX
        return new UnsupportedSystemMediaControlImpl();
#else
        return new UnsupportedSystemMediaControlImpl();
#endif
    }
}