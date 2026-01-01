using System;
using Avalonia;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Utilities;
using AudioPlayManager = QwQ_Music.Common.Managers.AudioPlayManager;

namespace QwQ_Music;

public static class Program {
    public static string VersionText => "0.9.1+build.251114.2";

    [STAThread]
    public static void Main(string[] args) {
        try {
            LoggerService.Info(
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
                 """);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        } catch (Exception e) {
            LoggerService.Error($"程序异常退出！\n捕捉到未处理异常:\n {e.Message}\n {e.StackTrace}");

            throw;
        } finally {
            ShutdownApplication();
        }
    }

    private static void ShutdownApplication() {
        try {
            LoggerService.Info("正在关闭...");
            Shutdown();
            LoggerService.Info("资源已释放。");
        } catch (Exception ex) {
            LoggerService.Error($"关闭App时发生错误: {ex.Message}");
        }
    }

    private static void Shutdown() {
        ConfigManager.SaveConfig();
        LoggerService.Info("设置已保存");

        AudioPlayManager.Instance.Shutdown();
        MousePenetrate.ClearCache();
        HotkeyService.ClearCache();
        NavigateService.ClearCache();
        CacheManager.ClearCache();
    }

    private static AppBuilder BuildAvaloniaApp() {
        return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
    }
}