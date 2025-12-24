using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using QwQ_Music.Models.ConfigModels;

namespace QwQ_Music.Common.Services;

public static class PlaylistRepository {
    public static Task<string[]> ReadAsync() {
        return File.ReadAllLinesAsync(StaticConfig.PlaylistPath);
    }
    public static async Task WriteAsync(IEnumerable<string> data) {
        await File.WriteAllLinesAsync(StaticConfig.PlaylistPath,data).ConfigureAwait(false);
    }
}