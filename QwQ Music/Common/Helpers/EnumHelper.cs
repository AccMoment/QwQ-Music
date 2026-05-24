using System.Collections.Frozen;
using QwQ_Music.Common.Services;

namespace QwQ_Music.Common.Helpers;

public static class EnumDescriptionStore {
    public static readonly Dictionary<string, string> EnumDescriptions = new() {
        // ClosingBehavior
        ["AskAbout"] = "询问", ["Exit"] = "直接退出", ["HideToTray"] = "隐藏到系统托盘"
    };
}

public static class EnumHelper<T> where T : Enum {
    public static Dictionary<T, string> GetValueDescriptionDictionary() {
        return Enum.GetValuesAsUnderlyingType(typeof(T))
                   .Cast<T>()
                   .ToDictionary(
                       e => e,
                       e => EnumDescriptionStore.EnumDescriptions.TryGetValue(e.ToString(), out string? desc) ?
                           desc :
                           e.ToString());
    }

    public static FrozenDictionary<T, string> GetTranslationDictionary() {
        string typeName = typeof(T).Name;
        return Enum.GetValuesAsUnderlyingType(typeof(T))
                   .Cast<T>()
                   .ToDictionary(e => e, e => I18NService.Lang[e.ToString(),typeName]).ToFrozenDictionary();
    }

    public static List<T> ToList() { return GetEnumerable().ToList(); }

    public static T[] ToArray() { return GetEnumerable().ToArray(); }

    private static IEnumerable<T> GetEnumerable() { return Enum.GetValuesAsUnderlyingType(typeof(T)).Cast<T>(); }
}