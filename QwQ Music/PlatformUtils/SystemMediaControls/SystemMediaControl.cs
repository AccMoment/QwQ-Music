namespace QwQ_Music.PlatformUtils.SystemMediaControls;

public class SystemMediaControlEventArgs : EventArgs;

public class PlaybackPositionChangedEventArgs(TimeSpan position) : SystemMediaControlEventArgs {
    public readonly TimeSpan Position = position;
}

public static class SystemMediaControl {
    public const string APPID = "com.Mioter.QwQMusic";
    public static ISystemMediaControlImpl Instance = CreateSystemMediaControl();

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