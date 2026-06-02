using System.Runtime.InteropServices;
using QwQ_Music.Common.Services;

#if _WIN_NT
namespace QwQ_Music.PlatformUtils.SystemSleep;

public sealed class WindowsSleepHelperImpl : ISystemSleepHelperImpl {
    private bool _isPrevent;
    private readonly AutoResetEvent _lock = new(false);
    private readonly CancellationTokenSource _cts = new();

    public WindowsSleepHelperImpl() {
        var thread = new Thread(() => {
            while (!_cts.IsCancellationRequested && _lock.WaitOne()) {
                if (_isPrevent) {
                    PauseSystemSleep(true);
                } else {
                    ReleaseSystemSleep();
                }
            }

            _cts.Dispose();
            _lock.Close();
        }) { IsBackground = true, Name = "PreventSystemSleep", Priority = ThreadPriority.Lowest };
        thread.Start();
    }

    public ValueTask PreventSleepAsync(bool keepDisplay, string reason) {
        Interlocked.CompareExchange(ref _isPrevent, false, true);
        _lock.Set();
        return ValueTask.CompletedTask;
    }

    public ValueTask RestoreSleepAsync() {
        Interlocked.CompareExchange(ref _isPrevent, true, false);
        _lock.Set();
        return ValueTask.CompletedTask;
    }

    [Flags]
    private enum ExecutionState : uint {
        Continuous = 0x80000000, SystemRequired = 0x00000001, DisplayRequired = 0x00000002
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    public void PauseSystemSleep(bool keepDisplay) {
        ExecutionState flags = ExecutionState.Continuous | ExecutionState.SystemRequired;
        if (keepDisplay)
            flags |= ExecutionState.DisplayRequired;

        if (SetThreadExecutionState(flags) != 0)
            return;

        int error = Marshal.GetLastWin32Error();
        LoggerService.Error($"设置系统睡眠状态失败，Win32Error={error}");
    }

    public void ReleaseSystemSleep() {
        if (SetThreadExecutionState(ExecutionState.Continuous) != 0)
            return;

        int error = Marshal.GetLastWin32Error();
        LoggerService.Error($"恢复系统睡眠状态失败，Win32Error={error}");
    }

    public ValueTask DisposeAsync() {
        _cts.Cancel();
        _isPrevent = false;
        _lock.Set();
        return ValueTask.CompletedTask;
    }
}
#endif