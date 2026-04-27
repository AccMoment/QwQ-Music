namespace QwQ_Music.Common.Services;

public class I18NService {
    public enum Language {
        EnUs, ZhCn
    }

    private static readonly Dictionary<Language, Dictionary<string, string>> _characterSet = new() {
        {
            Language.ZhCn,
            new Dictionary<string, string> {
                ["Window"] = "窗口",
                ["Music"] = "音乐",
                ["Classification"] = "分类",
                ["Other"] = "其他",
                ["Statistics"] = "统计",
                ["Settings"] = "设置",
                ["LyricConfig"] = "歌词",
                ["Offset"] = "偏移",
                ["IsEnabled"] = "启用",
                ["IsDoubleLine"] = "双行模式",
                ["IsDualLang"] = "显示翻译",
                ["IsVertical"] = "纵向模式",
                ["PositionX"] = "横向顶点坐标",
                ["PositionY"] = "纵向顶点坐标",
                ["Width"] = "宽度",
                ["Height"] = "高度",
                ["Maximize"] = "最大化",
                ["Reset"] = "恢复默认值",
                ["LyricMainTopColor"] = "主要歌词顶部",
                ["LyricMainBottomColor"] = "主要歌词底部",
                ["LyricMainBorderColor"] = "主要歌词描边",
                ["LyricAltTopColor"] = "备选歌词顶部",
                ["LyricAltBottomColor"] = "备选歌词底部",
                ["LyricAltBorderColor"] = "备选歌词描边",
                ["Loading..."] = "加载中..."
            }
        }
    };

    public static readonly I18NService Lang = new();

    public static Language CurrentLanguage { get; set; } = Language.ZhCn;

    public string this[string key] {
        get {
            _characterSet[CurrentLanguage].TryGetValue(key, out string? value);
            if (value is not null)
                return value;
            LoggerService.Warning($"无法获取{key}在{CurrentLanguage}下的翻译");
            return key;
        }
    }

    public static void LoadLanguage(Language language) { throw new NotImplementedException(); }
}