using System.Collections.Frozen;
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
    public static I18NService I18NService => I18NService.Lang;

    public static FrozenDictionary<ClosingBehavior, string> ClosingBehaviors =>
        EnumHelper<ClosingBehavior>.GetTranslationDictionary();

    public static FrozenDictionary<LogLevel,string> LogLevels { get; } = EnumHelper<LogLevel>.GetTranslationDictionary();
}