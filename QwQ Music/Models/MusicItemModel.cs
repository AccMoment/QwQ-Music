using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ATL;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Services.MusicTagExtractors;
using QwQ_Music.Common.Utilities;
using QwQ_Music.Common.Utilities.StringUtilities;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.Models.Enums;

namespace QwQ_Music.Models;

public partial class MusicItemModel : ObservableObject {
    public static readonly MusicItemModel Default = new() { Title = "听你想听", Artists = "YOU", FilePath = string.Empty };

    public bool IsCurrent { get; private set; }
    public Stream Data => File.OpenRead(FilePath);

    [ObservableProperty]
    public partial string Title { get; set; } = "未知标题";

    [ObservableProperty]
    public partial string Artists { get; set; } = "未知歌手";

    [ObservableProperty]
    public partial string? Composer { get; set; }

    [ObservableProperty]
    public partial string Album { get; set; } = "未知专辑";

    [ObservableProperty]
    public partial string AlbumArtist { get; set; } = "未知专辑艺术家";

    [ObservableProperty]
    public partial TimeSpan Record { get; set; } = TimeSpan.Zero;

    public TimeSpan Duration { get; set; }

    public required string FilePath {
        get;
        set {
            field = value;
            Extension = Path.GetExtension(FilePath).ToUpper();
        }
    }

    public string FileSize { get; set; } = "未知";

    public double Gain { get; set; }

    public string EncodingFormat { get; set; } = "未知";

    public string? CoverId { get; set; }

    public string[]? CoverColors { get; set; }

    public AudioQualityLevel AudioQualityLevel { get; set; }

    public string Comment { get; set; } = "";

    public Bitmap CoverImage =>
        CacheManager.TryLoadCacheFromFile(CoverId, "音频", "封面", StaticConfig.GetMusicCoverFullPath(CoverId));

    [ObservableProperty]
    public partial string Remarks { get; set; } = "";

    public double LyricOffset { get; set; }

    public DateTime InsertTime { get; set; }

    public DateTime ModificationTime { get; set; }

    public int Channels { get; set; }
    public double SampleRate { get; set; }

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。
    public string Extension;   // Initializes when setting FilePath.
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。
    public void RemoveCover() {
        if (CoverId is not null)
            CacheManager.DeleteImage(CoverId);
        CoverId = null;
    }

    public async Task<Track?> GetTrackAsync() {
        if (Extension == AudioFileValidator.AudioFormatsExtendToNameMap[AudioFileValidator.ExtendAudioFormats.Ncm]) {
            return await new NcmMusicTagExtractor(FilePath).GetTrackAsync().ConfigureAwait(false);
        }

        return new Track(FilePath);
    }

    public async Task<bool> UpdateMetaDataAsync(bool forceRefresh = false) {
        if (!File.Exists(FilePath)) {
            await LoggerService.ErrorAsync($"未找到音乐文件: {FilePath}").ConfigureAwait(false);
            return false;
        }

        var fileInfo = new FileInfo(FilePath);

        // 如果修改时间没有变动，不需要更新
        if (fileInfo.LastWriteTimeUtc == ModificationTime && !forceRefresh) {
            return false;
        }


        if (await GetTrackAsync().ConfigureAwait(false) is { } track)
            SetMusicMetadata(track, fileInfo);
        return true;
    }

    private void SetMusicMetadata(Track track, FileInfo? fileInfo = null) {
        Title = string.IsNullOrEmpty(track.Title) ? Path.GetFileNameWithoutExtension(FilePath) : track.Title;

        if (!string.IsNullOrEmpty(track.Artist))
            Artists = track.Artist;

        if (!string.IsNullOrEmpty(track.Album))
            Album = track.Album;

        if (!string.IsNullOrEmpty(track.AlbumArtist))
            AlbumArtist = track.AlbumArtist;

        Composer = track.Composer;
        Duration = TimeSpan.FromMilliseconds(track.DurationMs);
        EncodingFormat = track.AudioFormat.ShortName;
        Comment = track.Comment;
        AudioQualityLevel = AudioQualityDetector.DetermineQualityLevel(track);
        Channels = track.ChannelsArrangement.NbChannels;
        SampleRate = track.SampleRate;
        try {
            if (track.EmbeddedPictures.Count > 0) {
                using var coverStream = new MemoryStream(track.EmbeddedPictures[0].PictureData);
                CoverId = Guid.NewGuid().ToString();
                FileOperationService.SaveImageAsync(
                                        Bitmap.DecodeToWidth(coverStream, 128),
                                        StaticConfig.GetMusicCoverFullPath(CoverId),
                                        true)
                                    .ConfigureAwait(false);
            }
        } catch (Exception ex) {
            LoggerService.Error($"解码封面图像失败 {FilePath}: {ex.Message}\n{ex.StackTrace}");
        }

        // 更新文件信息
        fileInfo ??= new FileInfo(FilePath);

        ModificationTime = fileInfo.LastWriteTimeUtc;

        FileSize = StringFormatter.FormatFileSize(fileInfo.Length);
    }

    private async Task<LyricsData> LoadLyricsAsync(Track track) {
        var lyricsList = track.Lyrics;

        // 查找同步歌词
        var syncLyricsInfo = lyricsList.FirstOrDefault(l => l.SynchronizedLyrics.Count > 0);

        if (syncLyricsInfo != null) {
            var syncLyrics = syncLyricsInfo.SynchronizedLyrics;

            // 按时间点分组
            var grouped = syncLyrics.GroupBy(p => p.TimestampStart).OrderBy(g => g.Key);

            var lyricLines = new List<LyricLine>();

            foreach (var group in grouped) {
                var phrases = group.ToList();

                string? primary, translation = null;

                // 方案二：尝试分隔符
                string[] split = phrases[0].Text.Split(["//", "|", "\n", " "], StringSplitOptions.RemoveEmptyEntries);

                if (split.Length == 2) {
                    primary = split[0].Trim();
                    translation = split[1].Trim();
                } else {
                    primary = phrases[0].Text.Trim();

                    // 方案一：同一时间点有多条
                    if (phrases.Count > 1)
                        translation = phrases[1].Text.Trim();
                }

                lyricLines.Add(new LyricLine(group.Key / 1000.0, primary, translation));
            }

            return new LyricsData { Data = lyricLines };
        }

        // 查找非同步歌词
        var unsyncLyricsInfo = lyricsList.FirstOrDefault(l => !string.IsNullOrEmpty(l.UnsynchronizedLyrics));

        if (unsyncLyricsInfo != null) {
            string? lyric = unsyncLyricsInfo.UnsynchronizedLyrics;

            if (!string.IsNullOrEmpty(lyric))
                return await Task.Run(() => LyricsService.ParseLrcFile(lyric)).ConfigureAwait(false) ??
                       LyricsData.Empty;
        }

        // 获取目录路径
        string? directoryPath = Path.GetDirectoryName(FilePath);

        // 获取文件名（不含扩展名）
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(FilePath);

        // 拼接完整路径（不含扩展名）
        if (directoryPath == null || fileNameWithoutExtension == "")
            return LyricsData.Empty;

        string fullPathWithoutExtension = Path.Combine(directoryPath, fileNameWithoutExtension);
        string lyricPath = fullPathWithoutExtension + ".lrc";

        if (!Path.Exists(lyricPath))
            return LyricsData.Empty;

        string lyricText = await File.ReadAllTextAsync(lyricPath).ConfigureAwait(false);

        return await Task.Run(() => LyricsService.ParseLrcFile(lyricText)).ConfigureAwait(false) ?? LyricsData.Empty;
    }

    public async Task LoadCurrentAsync() {
        IsCurrent = true;
        if (string.IsNullOrWhiteSpace(FilePath) || await GetTrackAsync().ConfigureAwait(false) is not { } track)
            return;
        Track = track;
        OriginalCover = new Bitmap(new MemoryStream(track.EmbeddedPictures[0].PictureData));
        Lyrics = await LoadLyricsAsync(track).ConfigureAwait(false);
    }

    public void DisposeCurrent() {
        IsCurrent = false;
        Track = null;
        OriginalCover = null;
        Lyrics = LyricsData.Loading;
    }

    #region 播放歌曲前加载的属性

    public Bitmap? OriginalCover {
        get;
        private set {
            if (value is null ||
                value.Size is { AspectRatio: 1 } ||
                ConfigManager.UiConfig.CoverConfig.AllowNonSquareCover)
                field = value;
            else
                field = BitmapCropper.Crop(value, 1.0);
        }
    }

    public LyricsData Lyrics { get; private set; } = LyricsData.Loading;


    public Track? Track { get; private set; }

    #endregion 播放歌曲前加载的属性
}