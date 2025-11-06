using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QwQ_Music.Common.Manager;
using QwQ_Music.Models.ConfigModels;

namespace QwQ_Music.Common.Services;

public enum LogLevel
{
    Off = -1,
    Debug,
    Info,
    Warning,
    Error,
    Fatal,
    Custom,
}

public static class LoggerService
{
    private const int BASE_RETRY_DELAY_MS = 100;
    private const int MAX_RETRY_COUNT = 3;

    private static readonly LoggerServiceConfig _config = ConfigManager.LoggerServiceConfig;
    private static readonly string _savePath = StaticConfig.LogSavePath;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    private static FileStream? fileStream;
    private static DateTime currentDay = DateTime.Today;
    private static bool useFallbackPath;
    private static bool disposed;

    private static string LogFilePath => Path.Combine(
        useFallbackPath ? Path.GetTempPath() : _savePath,
        $"{currentDay:yyyy-MM-dd}.QwQ.log"
    );

    public static LogLevel Level => _config.Level;

    public static bool IsKeepOpen => _config.IsKeepOpen;

    public static int RetryCount => Math.Min(_config.RetryCount, MAX_RETRY_COUNT);

    private static FileStream GetFileStream()
    {
        if (fileStream is { CanWrite: true }) 
            return fileStream;

        try
        {
            string? dir = Path.GetDirectoryName(LogFilePath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            fileStream?.Dispose();
            fileStream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            return fileStream;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            if (useFallbackPath) throw;

            useFallbackPath = true;
            Console.WriteLine($"[Logger] 切换至备用路径: {LogFilePath}");

            return GetFileStream();
        }
    }

    private static string FormatMessage(string level, string msg, int line, string? func, string? file)
    {
        return $"{DateTime.Now:HH:mm:ss.fff} [{level}] <{func ?? "unknown"}> at {Path.GetFileName(file) ?? "unknown"}, line {line}: {msg}";
    }

    private static async Task WriteLogAsync(string formattedMessage)
    {
        if (disposed) return;

        await _semaphore.WaitAsync();

        try
        {
            if (DateTime.Today != currentDay)
            {
                currentDay = DateTime.Today;

                if (fileStream != null)
                {
                    await fileStream.DisposeAsync();
                }

                fileStream = null;
            }

            int attempts = 0;

            while (attempts < RetryCount)
            {
                try
                {
                    await using var writer = new StreamWriter(GetFileStream(), Encoding.UTF8, 1024, IsKeepOpen);
                    await writer.WriteLineAsync(formattedMessage);

                    return;
                }
                catch (IOException ex) when (ex.HResult is -2147024864 or -2147024891) // Sharing violation or Access denied
                {
                    attempts++;

                    if (attempts >= RetryCount) throw;

                    await Task.Delay(BASE_RETRY_DELAY_MS * attempts);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static void Log(
        LogLevel level,
        string status,
        string message,
        int line = 0,
        string? function = null,
        string? filename = null
        )
    {
        if (level < Level || disposed) return;

        string formatted = FormatMessage(status, message, line, function, filename);
        _ = WriteLogAsync(formatted);
    }

    private static Task LogAsync(
        LogLevel level,
        string status,
        string message,
        int line = 0,
        string? function = null,
        string? filename = null
        )
    {
        if (level < Level || disposed) return Task.CompletedTask;

        string formatted = FormatMessage(status, message, line, function, filename);

        return WriteLogAsync(formatted);
    }

    public static void Debug(
        string message,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        Log(LogLevel.Debug, "DEBUG", message, line, function, filename);
    }

    public static void Info(
        string message,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        Log(LogLevel.Info, "INFO", message, line, function, filename);
    }

    public static void Warning(
        string message,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        Log(LogLevel.Warning, "WARN", message, line, function, filename);
    }

    public static void Error(
        string message,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        Log(LogLevel.Error, "ERROR", message, line, function, filename);
    }

    public static void Fatal(
        string message,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        Log(LogLevel.Fatal, "FATAL", message, line, function, filename);
    }

    public static void Custom(
        string message,
        string status,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        Log(LogLevel.Custom, status.ToUpper(), message, line, function, filename);
    }

    // 异步版本
    public static Task DebugAsync(
        string message,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        return LogAsync(LogLevel.Debug, "DEBUG", message, line, function, filename);
    }

    public static Task InfoAsync(
        string message,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        return LogAsync(LogLevel.Info, "INFO", message, line, function, filename);
    }

    public static Task WarningAsync(
        string message,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        return LogAsync(LogLevel.Warning, "WARN", message, line, function, filename);
    }

    public static Task ErrorAsync(
        string message,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        return LogAsync(LogLevel.Error, "ERROR", message, line, function, filename);
    }

    public static Task FatalAsync(
        string message,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        return LogAsync(LogLevel.Fatal, "FATAL", message, line, function, filename);
    }

    public static Task CustomAsync(
        string message,
        string status,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null,
        [CallerFilePath] string? filename = null
        )
    {
        return LogAsync(LogLevel.Custom, status.ToUpper(), message, line, function, filename);
    }

    public static void Shutdown()
    {
        if (disposed) return;

        disposed = true;
        fileStream?.Dispose();
        Info("日志服务已关闭");
    }
}
