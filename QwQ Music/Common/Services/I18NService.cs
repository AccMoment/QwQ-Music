using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services.ConfigIO;
using QwQ_Music.Models.ConfigModels;

namespace QwQ_Music.Common.Services;

public class I18NService {
    public FrozenDictionary<string, string> AvailableLanguages { get; private set; } =
        new Dictionary<string, string>().ToFrozenDictionary();

    public static I18NService Lang { get; } = new();

    private I18NService() {
        UpdateAvailableLanguagesAsync()
            .ContinueWith(_ => LoadLanguage())
            .ContinueWith(LoggerService.HandleException)
            .ConfigureAwait(false);
    }

    public Translation Translation { get; set; }


    [MemberNotNull(nameof(AvailableLanguages))]
    public async Task UpdateAvailableLanguagesAsync(bool isForced = false) {
        var identifier = new JsonConfigService(I18NJsonContext.Default, StaticConfig.ConfigSavePath);
        var old = identifier.Load<Dictionary<string, string>>("i18n-identifiers");
        string directory = Path.Combine(Environment.CurrentDirectory, "i18n");
        HashSet<string> files = [.. Directory.GetFiles(directory, "*.QwQ.json").Select(s => Path.GetFileName(s)[..^9])];
        if (old is null) {
            old = new Dictionary<string, string>();
            await foreach (var (k, v) in GetLanguageNames(files).ConfigureAwait(false)) {
                old.Add(k, v);
            }

            await identifier.SaveAsync(old, "i18n-identifiers").ConfigureAwait(false);
            SetAvailableLanguages(old);
            return;
        }

        string[] removed = old.Keys.Except(files).ToArray();
        if (!isForced && old.Count == files.Count && removed.Length == 0) {
            SetAvailableLanguages(old);
            return;
        }

        foreach (string k in removed) {
            old.Remove(k);
        }

        IEnumerable<string> targets = isForced ? files : files.Except(old.Keys);
        await foreach (var (k, v) in GetLanguageNames(targets).ConfigureAwait(false)) {
            old[k] = v;
        }

        await identifier.SaveAsync(old, "i18n-identifiers").ConfigureAwait(false);

        SetAvailableLanguages(old);
        return;

        // ReSharper disable once VariableHidesOuterVariable
        async IAsyncEnumerable<(string, string)> GetLanguageNames(IEnumerable<string> files) {
            foreach (string file in files) {
                string? name = null;
                (await new JsonConfigService(I18NJsonContext.Default, StaticConfig.I18NSavePath)
                       .LoadAsync<Dictionary<string, string>>(file)
                       .ConfigureAwait(false))?.TryGetValue("LanguageName", out name);
                if (name is null) {
                    await LoggerService.WarningAsync($"无法获取语言文件{file}的语言名称，使用文件名。").ConfigureAwait(false);
                    name = file;
                }

                yield return (Path.GetFileNameWithoutExtension(file), name);
            }
        }

        [MemberNotNull(nameof(AvailableLanguages))]
        void SetAvailableLanguages(Dictionary<string, string> translations) {
            translations.TryAdd("en_US", "English (Default)");
            AvailableLanguages = old.ToFrozenDictionary();
        }
    }

    public void LoadLanguage() {
        string fileName = ConfigManager.SystemConfig.Language;
        try {
            Translation = new Translation(
                (new JsonConfigService(I18NJsonContext.Default, StaticConfig.I18NSavePath)
                     .Load<Dictionary<string, string>>(fileName) ??
                 new Dictionary<string, string>()).ToFrozenDictionary());
        } catch (TypeInitializationException ex) {
            LoggerService.Error($"加载语言文件{fileName}失败，可能是翻译文件格式错误", ex);
        } catch (FileNotFoundException ex) {
            LoggerService.Error($"加载语言文件{fileName}失败，未找到文件。", ex);
        }

        if (Translation.Count == 0) {
            LoggerService.Error($"加载语言文件{fileName}失败，未找到翻译数据。");
            Translation = new Translation(new Dictionary<string, string>().ToFrozenDictionary());
        } else
            LoggerService.Info($"成功加载语言文件{fileName}，共{Translation.Count}条翻译数据。");
    }
}

public readonly struct Translation(FrozenDictionary<string, string> translation) {
    public int Count => translation.Count;

    public string this[string key, string callerIdentifier = ""] {
        get {
            if (key.EndsWith('V'))
                key = key[..^1];

            if (!string.IsNullOrWhiteSpace(callerIdentifier))
                callerIdentifier += ".";
            callerIdentifier += key;

            if (translation?.TryGetValue(callerIdentifier, out string? value) ?? false)
                return value;
            if (I18NService.Lang.AvailableLanguages[ConfigManager.SystemConfig.Language] != "English (Default)")
                LoggerService.Warning($"无法获取{callerIdentifier}在{ConfigManager.SystemConfig.Language}下的翻译");
            return key;
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class I18NJsonContext : JsonSerializerContext { }