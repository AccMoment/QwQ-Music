using System.Runtime.InteropServices;
using QwQ_Music.Common.Services;

namespace QwQ_Music.PlatformUtils.SystemSleep;

public class UnSupportedSystemSleepHelperImpl : ISystemSleepHelperImpl {
    public ValueTask DisposeAsync() { return ValueTask.CompletedTask; }

    public ValueTask PreventSleepAsync(bool keepDisplay,string reason) {
        LoggerService.Error(
            $"暂不支持阻止{RuntimeInformation.OSArchitecture} {RuntimeInformation.OSArchitecture}({
                RuntimeInformation.OSDescription})系统的睡眠。");
        return ValueTask.CompletedTask;
    }

    public ValueTask RestoreSleepAsync() {
        LoggerService.Error(
            $"暂不支持阻止{RuntimeInformation.OSArchitecture} {RuntimeInformation.OSArchitecture}({
                RuntimeInformation.OSDescription})系统的睡眠。");
        return ValueTask.CompletedTask;
    }
}