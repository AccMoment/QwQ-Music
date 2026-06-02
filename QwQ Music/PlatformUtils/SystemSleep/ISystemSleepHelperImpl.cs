namespace QwQ_Music.PlatformUtils.SystemSleep;

public interface ISystemSleepHelperImpl : IAsyncDisposable {
    ValueTask PreventSleepAsync(bool keepDisplay,string reason);
    ValueTask RestoreSleepAsync();
}