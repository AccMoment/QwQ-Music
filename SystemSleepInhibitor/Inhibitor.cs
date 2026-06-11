namespace SystemSleepInhibitor;

public static class Inhibitor {
    public static ISystemSleepHelperImpl Instance = GetNewPlatformInstance();

    private static ISystemSleepHelperImpl GetNewPlatformInstance() {
#if _WIN_NT
        LoggerService.Debug("检测到Windows");
        return new WindowsSleepHelperImpl();
#elif _LINUX
        LoggerService.Debug("检测到Linux");
        return new LinuxSleepHelperImpl();
#elif _OSX
        LoggerService.Debug("检测到Mac OS");
        return new UnSupportedSystemSleepHelperImpl();
#else
        return new UnSupportedSystemSleepHelperImpl();
#endif
    }
}