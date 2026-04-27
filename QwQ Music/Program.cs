using System.Diagnostics;
using Avalonia;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Services.Databases;
using QwQ_Music.Common.Utilities;
using AudioPlayManager = QwQ_Music.Common.Managers.AudioPlayManager;
using ThreadState = System.Diagnostics.ThreadState;

namespace QwQ_Music;

public static class Program {
    public static string VersionText => "2.0.6";

    [STAThread]
    public static async Task Main(string[] args) {
        try {
            await LoggerService.InfoAsync(
                                   "Starting up\n" +
                                   $"""
                                    ===========================================
                                                                                                                                
                                      _|_|                          _|_|          _|      _|                      _|            
                                    _|    _|  _|      _|      _|  _|    _|        _|_|  _|_|  _|    _|    _|_|_|        _|_|_|  
                                    _|  _|_|  _|      _|      _|  _|  _|_|        _|  _|  _|  _|    _|  _|_|      _|  _|        
                                    _|    _|    _|  _|  _|  _|    _|    _|        _|      _|  _|    _|      _|_|  _|  _|        
                                      _|_|  _|    _|      _|        _|_|  _|      _|      _|    _|_|_|  _|_|_|    _|    _|_|_|  
                                                                              
                                                                 
                                           ▶  QwQ Music v{VersionText}  🔊
                                         "Where emotions meet melody"

                                    ===========================================
                                    """)
                               .ConfigureAwait(false);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        } catch (Exception e) {
            await LoggerService.ErrorAsync($"程序异常退出！\n捕捉到未处理异常:\n {e.Message}\n {e.StackTrace}").ConfigureAwait(false);

            throw;
        } finally {
            await ShutdownAsync().ConfigureAwait(false);
        }

        LogActiveThreads();
        ThreadPool.GetMinThreads(out int a, out int b);
        ThreadPool.GetMaxThreads(out int c, out int d);
        Console.WriteLine($"[{a},{c}] | [{b},{d}]");
        Environment.Exit(0); // TODO FIXME TIER 1 
        // NOTE: Here must be some leaks.
        // The program did not exit so that I have to exit it manually and explicitly here.
        // We must fix this bug in the future.
    }

    private static void LogActiveThreads() {
        var reasons = new Dictionary<string, int>();
        ProcessThreadCollection threads = Process.GetCurrentProcess().Threads;
        foreach (ProcessThread thread in threads) {
            if (thread.ThreadState == ThreadState.Wait)
                reasons[thread.WaitReason.ToString()] = reasons.GetValueOrDefault(thread.WaitReason.ToString(), 0) + 1;
            else
                reasons[thread.ThreadState.ToString()] =
                    reasons.GetValueOrDefault(thread.ThreadState.ToString(), 0) + 1;
            if (thread.ThreadState == ThreadState.Wait)
                Console.WriteLine($"Thread {thread.Id} is waiting. Wait Reason: {thread.WaitReason}");
        }

        Console.WriteLine(threads.Count);
        foreach (KeyValuePair<string, int> reason in reasons)
            Console.WriteLine($"{reason.Key}: {reason.Value}");
    }


    private static async Task ShutdownAsync() {
        await LoggerService.InfoAsync("正在关闭...").ConfigureAwait(false);
        try {
            await AudioPlayManager.Instance.DisposeAsync().ConfigureAwait(false);
            ConfigManager.SaveConfig(); // 需要用到 AudioPlayManager释放时修改的数据，不要修改前后顺序
            await LoggerService.InfoAsync("设置已保存").ConfigureAwait(false);
            MousePenetrate.ClearCache();
            HotkeyService.ClearCache();
            NavigateService.ClearCache();
            CacheManager.Dispose();
            await MusicItemRepository.Instance.DisposeAsync().ConfigureAwait(false);
            await MusicListRepository.Instance.DisposeAsync().ConfigureAwait(false);
            await MusicListItemsRepository.Instance.DisposeAsync().ConfigureAwait(false);
            await LoggerService.InfoAsync("资源已释放。").ConfigureAwait(false);
        } catch (Exception ex) {
            await LoggerService.ErrorAsync($"关闭App时发生错误: {ex.Message}").ConfigureAwait(false);
        } finally {
            await LoggerService.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static AppBuilder BuildAvaloniaApp() {
        return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
    }
}