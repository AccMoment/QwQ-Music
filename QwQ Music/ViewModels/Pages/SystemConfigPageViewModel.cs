using QwQ_Music.Common.Helpers;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.Models.Enums;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Pages;

public class SystemConfigPageViewModel() : NavigableViewModel(nameof(SystemConfigPageViewModel)) {
    public SystemConfig Config { get; } = ConfigManager.SystemConfig;

    public static LoggerServiceConfig LoggerServiceConfig => ConfigManager.LoggerServiceConfig;

    public static Dictionary<ClosingBehavior, string> ClosingBehaviors =>
        EnumHelper<ClosingBehavior>.GetValueDescriptionDictionary();

    public static LogLevel[] LogLevels { get; } = EnumHelper<LogLevel>.ToArray();
}

public record ClosingBehaviorMap(string Key, ClosingBehavior Value);