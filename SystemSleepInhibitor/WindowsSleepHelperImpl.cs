#if _WIN_NT

using System.Runtime.InteropServices;

namespace SystemSleepInhibitor.SystemSleep;

public sealed class WindowsSleepHelperImpl : ISystemSleepHelperImpl {
    private int? _previousId;

    public Task InhibitAsync(bool keepDisplay, string reason) {
        CheckThread();
        InhibitSystemSleep(keepDisplay);
        return Task.CompletedTask;
    }

    public Task RestoreAsync() {
        CheckThread();
        RestoreSystemSleep();
        return Task.CompletedTask;
    }

    public bool CheckAccess() {
        _previousId ??= Environment.CurrentManagedThreadId;
        return _previousId == Environment.CurrentManagedThreadId;
    }

    public void CheckThread() {
        _previousId ??= Environment.CurrentManagedThreadId;
        if (_previousId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException();
    }

    [Flags]
    private enum ExecutionState : uint {
        Continuous = 0x80000000, SystemRequired = 0x00000001, DisplayRequired = 0x00000002
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    public void InhibitSystemSleep(bool keepDisplay) {
        ExecutionState flags = ExecutionState.Continuous | ExecutionState.SystemRequired;
        if (keepDisplay)
            flags |= ExecutionState.DisplayRequired;

        if (SetThreadExecutionState(flags) != 0)
            return;

        int error = Marshal.GetLastWin32Error();
        throw new InvalidOperationException($"Failed when inhibiting system sleep, win32 error code: {error}");

    }

    public void RestoreSystemSleep() {
        if (SetThreadExecutionState(ExecutionState.Continuous) != 0)
            return;

        int error = Marshal.GetLastWin32Error();
        throw new InvalidOperationException($"Failed when restoring system sleep, win32 error code: {error}");
    }

    public ValueTask DisposeAsync() { return ValueTask.CompletedTask; }
}

#endif