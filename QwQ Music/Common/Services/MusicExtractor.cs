using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ATL;
using Avalonia.Media.Imaging;
using QwQ_Music.Common.Services.MusicTagExtractors;
using QwQ_Music.Common.Utilities;
using QwQ_Music.Common.Utilities.StringUtilities;
using QwQ_Music.Models;

namespace QwQ_Music.Common.Services;

public static class MusicExtractor
{
    /// <summary>
    ///     异步提取音乐文件的元数据。
    /// </summary>
    /// <param name="filePath">音乐文件路径。</param>
    /// <returns>包含音乐信息的模型。</returns>
    public static async Task<MusicItemModel?> ExtractMusicInfoAsync(string filePath)
    {
        var track = await MusicTagExtractorFactory.GetTrackAsync(filePath).ConfigureAwait(false);

        if (track != null)
        {
            return await Task.Run(() =>
            {
                var itemModel = new MusicItemModel
                {
                    FilePath = filePath,
                };

                SetMusicMetadata(itemModel, track);

                return itemModel;
            }).ConfigureAwait(false); // 在线程池中完成较重的元数据与封面解码，避免切回 UI 线程造成卡顿
        }

        await LoggerService.ErrorAsync($"未能从音频文件中提取元数据 {filePath}!").ConfigureAwait(false);

        return null;
    }

    /// <summary>
    ///     异步更新音乐文件的元数据。
    ///     如果文件的修改时间发生变动，则重新提取并更新除路径外的所有属性。
    /// </summary>
    /// <param name="musicItem">要更新的音乐项模型。</param>
    /// <param name="forceRefresh">是否强制刷新，true: 刷新全部，false: 仅刷新修改时间变动的项目</param>
    /// <returns>如果成功更新则返回 true，否则返回 false。</returns>
    public static async Task<bool> UpdateMusicInfoAsync(MusicItemModel musicItem, bool forceRefresh = false)
    {
        // 检查文件是否存在
        if (!File.Exists(musicItem.FilePath))
        {
            await LoggerService.ErrorAsync($"未找到音乐文件: {musicItem.FilePath}");

            return false;
        }

        var fileInfo = new FileInfo(musicItem.FilePath);

        // 如果修改时间没有变动，不需要更新
        if (fileInfo.LastWriteTimeUtc == musicItem.ModificationTime && !forceRefresh)
        {
            return false;
        }

        // 修改时间发生变动，重新提取元数据
        var metadata = await MusicTagExtractorFactory.GetTrackAsync(musicItem.FilePath);

        if (metadata == null)
        {
            await LoggerService.ErrorAsync($"未能从音频文件中提取元数据: {musicItem.FilePath}");

            return false;
        }

        // 更新除 FilePath 外的所有属性
        SetMusicMetadata(musicItem, metadata, fileInfo);

        return true;
    }

    private static void SetMusicMetadata(MusicItemModel musicItem, Track track, FileInfo? fileInfo = null)
    {
        musicItem.Title = string.IsNullOrEmpty(track.Title)
            ? Path.GetFileNameWithoutExtension(musicItem.FilePath)
            : track.Title;

        if (!string.IsNullOrEmpty(track.Artist))
            musicItem.Artists = track.Artist;

        if (!string.IsNullOrEmpty(track.Album))
            musicItem.Album = track.Album;

        if (!string.IsNullOrEmpty(track.AlbumArtist))
            musicItem.AlbumArtist = track.AlbumArtist;

        musicItem.Composer = track.Composer;
        musicItem.Duration = TimeSpan.FromMilliseconds(track.DurationMs);
        musicItem.EncodingFormat = track.AudioFormat.ShortName;
        musicItem.Comment = track.Comment;
        musicItem.AudioQualityLevel = AudioQualityDetector.DetermineQualityLevel(track);

        try
        {
            if (track.EmbeddedPictures.Count > 0)
            {
                using var coverStream = new MemoryStream(track.EmbeddedPictures[0].PictureData);
                musicItem.CoverImage = Bitmap.DecodeToWidth(coverStream, 128);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Error($"解码封面图像失败 {musicItem.FilePath}: {ex.Message}\n{ex.StackTrace}");
        }

        // 更新文件信息
        fileInfo ??= new FileInfo(musicItem.FilePath);

        musicItem.ModificationTime = fileInfo.LastWriteTimeUtc;

        musicItem.FileSize = StringFormatter.FormatFileSize(fileInfo.Length);
    }

    public static string PrepareCoverInfo(string? artists, string? album, string filePath)
    {
        if (string.IsNullOrWhiteSpace(artists) && string.IsNullOrWhiteSpace(album))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            return $"{fileName}#{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png";
        }

        string finalArtists = string.IsNullOrWhiteSpace(artists)
            ? $"未知歌手#{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
            : artists;

        string finalAlbum = string.IsNullOrWhiteSpace(album)
            ? $"未知专辑#{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
            : album;

        return GetCoverFileName(finalArtists, finalAlbum);
    }

    /// <summary>
    ///     生成并清理封面文件名。
    /// </summary>
    /// <param name="artists">艺术家</param>
    /// <param name="album">专辑</param>
    /// <returns>清理后的封面文件名 (例如 "Artist-Album.jpg")</returns>
    private static string GetCoverFileName(string artists, string album)
    {
        // 截断并清理文件名
        string safeArtists = string.IsNullOrWhiteSpace(artists) ? "未知歌手" : artists;
        string safeAlbum = string.IsNullOrWhiteSpace(album) ? "未知专辑" : album;

        string coverFileName = PathEnsurer.CleanFileName(
            $"{(safeArtists.Length > 20 ? safeArtists[..20] : safeArtists)}-{(safeAlbum.Length > 20 ? safeAlbum[..20] : safeAlbum)}"
        );

        return coverFileName; // 只返回文件名
    }

    /// <summary>
    ///     获取文件流。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    /// <returns>文件流，如果文件不存在则返回 null。</returns>
    private static async Task<FileStream?> GetMusicCoverStream(string filePath)
    {
        if (!File.Exists(filePath))
        {
            await LoggerService.WarningAsync($"文件不存在: {filePath}");

            return null;
        }

        try
        {
            return await Task.Run(() => new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
        }
        catch (Exception ex)
        {
            await LoggerService.ErrorAsync(
                $"意外的 {ex.GetType()} 类型错误发生在加载文件流时: {filePath}\n" +
                $"{ex.Message}\n{ex.StackTrace}"
            );

            return null;
        }
    }

    /// <summary>
    ///     加载压缩的位图。
    /// </summary>
    /// <param name="coverPath">专辑封面索引。</param>
    /// <param name="size">目标尺寸，默认128</param>
    /// <returns>压缩后的位图。</returns>
    public static async Task<Bitmap?> LoadCompressedBitmapFromFileAsync(string coverPath, int size = 128)
    {
        await using var stream = await GetMusicCoverStream(coverPath);

        try
        {
            return stream == null ? null : Bitmap.DecodeToWidth(stream, size); // 解码并缩放图片，使用较小的宽度
        }
        catch (Exception ex)
        {
            await LoggerService.ErrorAsync(
                $"意外的 {ex.GetType()} 类型错误发生在加载压缩后的封面图像时: {coverPath}\n" +
                $"{ex.Message}\n{ex.StackTrace}"
            );

            return null;
        }
    }

    /// <summary>
    ///     从文件系统中加载位图。
    /// </summary>
    /// <param name="coverPath">图片路径。</param>
    /// <returns>原始位图。</returns>
    public static async Task<Bitmap?> LoadBitmapFromFileAsync(string coverPath)
    {
        // 获取文件流
        await using var stream = await GetMusicCoverStream(coverPath);

        try
        {
            return stream == null ? null : new Bitmap(stream); // 直接解码图片
        }
        catch (Exception ex)
        {
            await LoggerService.ErrorAsync(
                $"意外的 {ex.GetType()} 类型错误发生在加载原始封面图像时: {coverPath}\n" +
                $"{ex.Message}\n{ex.StackTrace}"
            );

            return null;
        }
    }

    /// <summary>
    ///     从音频文件中提取专辑封面（支持 .ncm 和常规音频文件）
    /// </summary>
    /// <param name="filePath">音频文件路径</param>
    /// <returns>提取的封面原始位图。</returns>
    public static async Task<Bitmap?> GetCoverFromAudioAsync(string filePath)
    {
        try
        {
            var track = await MusicTagExtractorFactory.GetTrackAsync(filePath);

            if (track?.EmbeddedPictures.Count <= 0)
                return null;

            byte[]? pictureData = track?.EmbeddedPictures[0].PictureData;

            if (pictureData == null || pictureData.Length == 0)
                return null;

            return new Bitmap(new MemoryStream(pictureData));
        }
        catch (FileNotFoundException)
        {
            await LoggerService.WarningAsync($"找不到用于封面提取的音频文件: {filePath}");

            return null;
        }
        catch (Exception ex)
        {
            await LoggerService.ErrorAsync($"从音频文件中提取封面时出错: {filePath}: {ex.Message}");

            return null;
        }
    }

    /// <summary>
    ///     提取音频文件或同名.lrc文件中歌词
    /// </summary>
    /// <param name="filePath">音频文件路径</param>
    /// <returns>歌词数据</returns>
    public static async Task<LyricsData> ExtractMusicLyricsAsync(string? filePath)
    {
        var lyricsData = new LyricsData();
        var track = new Track(filePath);
        var lyricsList = track.Lyrics;

        // 查找同步歌词
        var syncLyricsInfo = lyricsList.FirstOrDefault(l => l.SynchronizedLyrics.Count > 0);

        if (syncLyricsInfo != null)
        {
            var syncLyrics = syncLyricsInfo.SynchronizedLyrics;

            // 按时间点分组
            var grouped = syncLyrics.GroupBy(p => p.TimestampStart).OrderBy(g => g.Key);

            var lyricLines = new List<LyricLine>();

            foreach (var group in grouped)
            {
                var phrases = group.ToList();

                string? primary,
                    translation = null;

                // 方案二：尝试分隔符
                string[] split = phrases[0].Text.Split(["//", "|", "\n", " "], StringSplitOptions.RemoveEmptyEntries);

                if (split.Length == 2)
                {
                    primary = split[0].Trim();
                    translation = split[1].Trim();
                }
                else
                {
                    primary = phrases[0].Text.Trim();

                    // 方案一：同一时间点有多条
                    if (phrases.Count > 1)
                        translation = phrases[1].Text.Trim();
                }

                lyricLines.Add(new LyricLine(group.Key / 1000.0, primary, translation));
            }

            lyricsData.Lyrics = lyricLines;

            return lyricsData;
        }

        // 查找非同步歌词
        var unsyncLyricsInfo = lyricsList.FirstOrDefault(l => !string.IsNullOrEmpty(l.UnsynchronizedLyrics));

        if (unsyncLyricsInfo != null)
        {
            string? lyric = unsyncLyricsInfo.UnsynchronizedLyrics;

            if (!string.IsNullOrEmpty(lyric))
                return await Task.Run(() => LyricsService.ParseLrcFile(lyric)) ?? lyricsData;
        }

        // 获取目录路径
        string? directoryPath = Path.GetDirectoryName(filePath);

        // 获取文件名（不含扩展名）
        string? fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

        // 拼接完整路径（不含扩展名）
        if (directoryPath == null || fileNameWithoutExtension == null)
            return lyricsData;

        string fullPathWithoutExtension = Path.Combine(directoryPath, fileNameWithoutExtension);
        string lyricPath = fullPathWithoutExtension + ".lrc";

        if (!Path.Exists(lyricPath))
            return lyricsData;

        string lyricText = await File.ReadAllTextAsync(lyricPath);

        return await Task.Run(() => LyricsService.ParseLrcFile(lyricText)) ?? lyricsData;
    }
}
