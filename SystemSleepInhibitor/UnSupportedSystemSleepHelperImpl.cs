namespace SystemSleepInhibitor;

public class UnSupportedSystemSleepHelperImpl : ISystemSleepHelperImpl {
    public ValueTask DisposeAsync() { return ValueTask.CompletedTask; }

    public Task InhibitAsync(bool keepDisplay,string reason) {
        return Task.CompletedTask;
    }

    public Task RestoreAsync() {
        return Task.CompletedTask;
    }
}