using System.Collections.Frozen;
using QwQ_Music.Common.Services;

namespace QwQ_Music.Common.Helpers;

public static class EnumHelper<T> where T : Enum {
    public static FrozenDictionary<T, string> GetTranslationDictionary() {
        string typeName = typeof(T).Name;
        return Enum.GetValuesAsUnderlyingType(typeof(T))
                   .Cast<T>()
                   .ToDictionary(e => e, e => I18NService.Lang[e.ToString(), typeName])
                   .ToFrozenDictionary();
    }
}