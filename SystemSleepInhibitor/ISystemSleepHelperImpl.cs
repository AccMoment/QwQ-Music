namespace SystemSleepInhibitor;

public interface ISystemSleepHelperImpl : IAsyncDisposable {
    Task InhibitAsync(bool keepDisplay,string reason);
    Task RestoreAsync();
}