using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ATL;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.Common.Helpers;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Services.Databases;
using QwQ_Music.Common.Services.MusicTagExtractors;
using QwQ_Music.Common.Utilities;
using QwQ_Music.Common.Utilities.StringUtilities;
using QwQ_Music.Models.Enums;

namespace QwQ_Music.Models;

public partial class MusicItemModel : ObservableObject {
    public static readonly MusicItemModel Default = new() { Title = "听你想听", Artists = "YOU", FilePath = string.Empty };

    public bool IsCurrent {
        get;
        private set {
            if (SetProperty(ref field, value))
                OnPropertyChanged();
        }
    }

    public Stream Data => File.OpenRead(FilePath);

    [ObservableProperty]
    public partial string Title { get; set; } = "未知标题";

    [ObservableProperty]
    public partial string Artists { get; set; } = "未知歌手";
    //TODO SEPARATE ARTISTS

    [ObservableProperty]
    public partial string? Composer { get; set; }

    [ObservableProperty]
    public partial string Album { get; set; } = "未知专辑";

    [ObservableProperty]
    public partial string AlbumArtists { get; set; } = "未知专辑艺术家";

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

    public string? AlbumId { get; set; }

    public string[]? CoverColors { get; set; }

    public AudioQualityLevel AudioQualityLevel { get; set; }

    public string Comment { get; set; } = "";

    public Bitmap Thumbnail =>
        CacheManager.TryLoadCoverThumbnailAsync(
                        AlbumId,
                        "音频",
                        "封面",
                        AlbumThumbnailRepository.Instance,
                        () => OnPropertyChanged(),
                        Title)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

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
        if (AlbumId is not null)
            CacheManager.DeleteImage(AlbumId);
        AlbumId = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearRecord() { Record = TimeSpan.Zero; }

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


        if (await GetTrackAsync().ConfigureAwait(false) is not { } track) {
            await LoggerService.WarningAsync($"无法获取《{Title}》的音轨信息").ConfigureAwait(false);
            return false;
        }

        await SetMusicMetadata(track, fileInfo, forceRefresh).ConfigureAwait(false);
        return true;
    }

    private async Task SetMusicMetadata(Track track, FileInfo fileInfo, bool forceRefresh) {
        Title = string.IsNullOrEmpty(track.Title) ? Path.GetFileNameWithoutExtension(FilePath) : track.Title;

        if (!string.IsNullOrEmpty(track.Artist))
            Artists = track.Artist;

        if (!string.IsNullOrEmpty(track.Album))
            Album = track.Album;

        AlbumArtists = string.IsNullOrEmpty(track.AlbumArtist) ? Artists : track.AlbumArtist;

        Composer = track.Composer;
        Duration = TimeSpan.FromMilliseconds(track.DurationMs);
        EncodingFormat = track.AudioFormat.ShortName;
        Comment = track.Comment;
        AudioQualityLevel = AudioQualityDetector.DetermineQualityLevel(track);
        Channels = track.ChannelsArrangement.NbChannels;
        SampleRate = track.SampleRate;
        ModificationTime = DateTime.Now;
        FileSize = StringFormatter.FormatFileSize(fileInfo.Length);
        try {
            if (track.EmbeddedPictures.Count == 0) {
                await LoggerService.InfoAsync($"{Title}的封面不存在。").ConfigureAwait(false);
            } else {
                var coverData = track.EmbeddedPictures[0].PictureData;
                AlbumId = string.IsNullOrWhiteSpace(Album) ? Guid.NewGuid().ToString() : $"{Album} - {AlbumArtists}";

                var thumbnail = await ImageHelper
                                      .LoadFromMemoryAsync(new MemoryStream(coverData), $"{Album}_{AlbumArtists}", 128)
                                      .ConfigureAwait(false);
                if (thumbnail is null) {
                    await LoggerService.ErrorAsync("制作缩略图失败").ConfigureAwait(false);
                    return;
                }

                await LoggerService.DebugAsync("制作缩略图成功").ConfigureAwait(false);
                CacheManager.SetImage(AlbumId, "音频", thumbnail);
                _ = AlbumCoverRepository.Instance
                                        .InsertAsync(
                                            this,
                                            coverData,
                                            forceRefresh ? InsertExist.REPLACE : InsertExist.IGNORE)
                                        .ContinueWith(LoggerService.HandleException)
                                        .ConfigureAwait(false);
                OnPropertyChanged(nameof(Thumbnail));
            }
        } catch (Exception e) {
            await LoggerService.ErrorAsync($"解码{Title}的封面图像失败", e).ConfigureAwait(false);
        }

        await MusicItemRepository.Instance.UpdateAsync(this).ConfigureAwait(false);
    }

    private async Task<LyricsData> LoadLyricsAsync(Track track) {
        var result = LyricsData.Create(Title, Artists, Album, AlbumArtists);
        var lyricsList = track.Lyrics;

        // 查找同步歌词
        var syncLyricsInfo = lyricsList.FirstOrDefault(l => l.SynchronizedLyrics.Count > 0);

        if (syncLyricsInfo != null) {
            var syncLyrics = syncLyricsInfo.SynchronizedLyrics;

            // 按时间点分组
            var grouped = syncLyrics.GroupBy(p => p.TimestampStart).OrderBy(g => g.Key);

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

                result.Data.Add(new LyricLine(group.Key / 1000.0, primary, translation));
            }

            return result;
        }

        // 查找非同步歌词
        var unsyncedLyricsInfo = lyricsList.FirstOrDefault(l => !string.IsNullOrEmpty(l.UnsynchronizedLyrics));

        if (unsyncedLyricsInfo != null) {
            string? lyric = unsyncedLyricsInfo.UnsynchronizedLyrics;

            if (!string.IsNullOrEmpty(lyric)) {
                var embedded = await Task.Run(() => LyricsService.ParseLrcFile(lyric)).ConfigureAwait(false);
                result.Offset += embedded.Offset;
                result.Data.AddRange(embedded.Lyrics);
                return result;
            }
        }

        // 获取目录路径
        string? directoryPath = Path.GetDirectoryName(FilePath);

        // 获取文件名（不含扩展名）
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(FilePath);

        // 拼接完整路径（不含扩展名）
        if (directoryPath == null || fileNameWithoutExtension == "")
            return result;

        string fullPathWithoutExtension = Path.Combine(directoryPath, fileNameWithoutExtension);
        string lyricPath = fullPathWithoutExtension + ".lrc";

        if (!Path.Exists(lyricPath))
            return result;

        string lyricText = await File.ReadAllTextAsync(lyricPath).ConfigureAwait(false);

        var outer = await Task.Run(() => LyricsService.ParseLrcFile(lyricText)).ConfigureAwait(false);
        result.Offset += outer.Offset;
        result.Data.AddRange(outer.Lyrics);
        return result;
    }

    public async Task LoadCurrentAsync() {
        if (IsCurrent) {
            await LoggerService.WarningAsync("多余的音频封面与歌词加载请求，已忽略").ConfigureAwait(false);
            return;
        }

        await LoggerService.DebugAsync($"正在异步加载音频《{Title}》的原始封面与歌词...").ConfigureAwait(false);
        IsCurrent = true;
        Cover = CacheManager.Loading;
        if (string.IsNullOrWhiteSpace(FilePath)) {
            await LoggerService.ErrorAsync($"{Title}的文件路径为空。").ConfigureAwait(false);
            return;
        }

        if (await GetTrackAsync().ConfigureAwait(false) is not { } track) {
            await LoggerService.ErrorAsync($"无法获取{Title}的音频流。").ConfigureAwait(false);
            return;
        }

        Track = track;
        Dispatcher.UIThread.Post(() => Cover = track.EmbeddedPictures.Count > 0 ?
                                     new Bitmap(new MemoryStream(track.EmbeddedPictures[0].PictureData)) :
                                     CacheManager.NotExist);

        Lyrics = await LoadLyricsAsync(track).ConfigureAwait(false);
        await LoggerService.DebugAsync($"音频《{Title}》的原始封面与歌词加载完毕。").ConfigureAwait(false);
    }

    public void DisposeCurrent() {
        if (!IsCurrent) {
            LoggerService.Warning("意外的音频封面与歌词释放请求，已忽略。");
            return;
        }

        IsCurrent = false;
        Track = null;
        // Bitmap originalCover = Cover;
        Cover = CacheManager.Loading;
        // originalCover.Dispose();
        Lyrics = LyricsData.Loading;
        LoggerService.Debug($"已释放音频《{Title}》的原始封面与歌词。");
    }

    #region 播放歌曲前加载的属性

    public Bitmap Cover {
        get {
            try {
                _ = field.PixelSize;
                return field;
            } catch (NullReferenceException) {
                Cover = Track?.EmbeddedPictures.Count > 0 ?
                    new Bitmap(new MemoryStream(Track.EmbeddedPictures[0].PictureData)) :
                    CacheManager.NotExist;
                LoggerService.Warning($"歌曲《{Title}》的原始封面意外失效。已重新加载。");
                return Cover;
            }
        }
        private set {
            try {
                _ = value.PixelSize;
            } catch (NullReferenceException) {
                field = CacheManager.Loading;
                return;
            }

            if (value.Size is { AspectRatio: 1 } || ConfigManager.UiConfig.CoverConfig.AllowNonSquareCover)
                field = value;
            else
                field = BitmapCropper.Crop(value, 1.0);
            OnPropertyChanged();
        }
    } = CacheManager.NotExist;

    public LyricsData Lyrics { get; private set; } = LyricsData.Loading;


    public Track? Track { get; private set; }

    #endregion 播放歌曲前加载的属性
}

public readonly record struct PlaylistItemModel {
    public static readonly PlaylistItemModel RefDefault = new(MusicItemModel.Default, 0);

    private PlaylistItemModel(MusicItemModel model, ulong id) {
        Model = model;
        Id = id;
    }

    public PlaylistItemModel(MusicItemModel model) {
        Model = model;
        Id = IdAllocator;
    }

    private static ulong IdAllocator {
        get => field++;
        set;
    } = 1;

    public MusicItemModel Model { get; } = MusicItemModel.Default;
    public readonly ulong Id = 0L;

    public static void Reset() { IdAllocator = 1; }
}