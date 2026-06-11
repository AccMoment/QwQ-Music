using System.Collections.Frozen;
using QwQ_Music.Common.Services;

namespace QwQ_Music.Common.Helpers;

public static class EnumHelper<T> where T : struct, Enum {
    public static FrozenDictionary<string, T> ToDictionary() {
        return Enum.GetValuesAsUnderlyingType<T>()
                   .Cast<T>()
                   .ToDictionary(e => I18NService.Lang.Translation[e.ToString(), typeof(T).Name], e => e)
                   .ToFrozenDictionary();
    }
}