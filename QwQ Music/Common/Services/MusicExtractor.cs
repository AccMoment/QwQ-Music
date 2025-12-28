using System;
using System.IO;
using QwQ_Music.Common.Utilities;

namespace QwQ_Music.Common.Services;

public static class MusicExtractor {
    public static string PrepareCoverInfo(string? artists, string? album, string filePath) {
        if (string.IsNullOrWhiteSpace(artists) && string.IsNullOrWhiteSpace(album)) {
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            return $"{fileName}#{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png";
        }

        string finalArtists = string.IsNullOrWhiteSpace(artists) ?
            $"未知歌手#{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" :
            artists;

        string finalAlbum = string.IsNullOrWhiteSpace(album) ?
            $"未知专辑#{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" :
            album;

        return GetCoverFileName(finalArtists, finalAlbum);
    }

    /// <summary>
    ///     生成并清理封面文件名。
    /// </summary>
    /// <param name="artists">艺术家</param>
    /// <param name="album">专辑</param>
    /// <returns>清理后的封面文件名 (例如 "Artist-Album.jpg")</returns>
    private static string GetCoverFileName(string artists, string album) {
        // 截断并清理文件名
        string safeArtists = string.IsNullOrWhiteSpace(artists) ? "未知歌手" : artists;
        string safeAlbum = string.IsNullOrWhiteSpace(album) ? "未知专辑" : album;

        string coverFileName = PathEnsurer.CleanFileName(
            $"{(safeArtists.Length > 20 ? safeArtists[..20] : safeArtists)}-{
                (safeAlbum.Length > 20 ? safeAlbum[..20] : safeAlbum)}");

        return coverFileName; // 只返回文件名
    }

    /// <summary>
    ///     获取文件流。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    /// <returns>文件流，如果文件不存在则返回 null。</returns>
    private static FileStream? GetMusicCoverStream(string filePath) {
        if (!File.Exists(filePath)) {
            LoggerService.Warning($"文件不存在: {filePath}");

            return null;
        }

        try {
            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        } catch (Exception ex) {
            LoggerService.Error($"加载文件流时发生{ex.GetType()}类型的错误: {filePath}\n" + $"{ex.Message}\n{ex.StackTrace}");

            return null;
        }
    }
}