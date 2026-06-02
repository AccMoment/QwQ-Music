namespace QwQ_Music.PlatformUtils.SystemSleep;

public static class SystemSleepHelper {
    public static ISystemSleepHelperImpl Instance { get; } = GetNewPlatformInstance();

    public static ISystemSleepHelperImpl GetNewPlatformInstance() {
#if _WIN_NT
        return new WindowsSleepHelperImpl();
#elif _LINUX
        return new LinuxSleepHelperImpl();
#elif _OSX
        return new UnSupportedSystemSleepHelperImpl();
#else
        return new UnSupportedSystemSleepHelperImpl();
#endif
    }
}