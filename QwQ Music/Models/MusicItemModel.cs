using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.Common.Manager;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Utilities;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.Models.Enums;

namespace QwQ_Music.Models;

public partial class MusicItemModel : ObservableObject
{
    // 添加一个标志表示图片是否正在加载
    private LoadingState? _loadingState;

    [ObservableProperty] public partial string Title { get; set; } = "未知标题";

    [ObservableProperty] public partial string Artists { get; set; } = "未知歌手";

    [ObservableProperty] public partial string? Composer { get; set; }

    [ObservableProperty] public partial string Album { get; set; } = "未知专辑";

    [ObservableProperty] public partial string AlbumArtist { get; set; } = "未知专辑艺术家";

    [ObservableProperty] public partial TimeSpan Current { get; set; }

    public TimeSpan Duration { get; set; }

    public required string FilePath { get; set; }

    public string? FileSize { get; set; }

    public double Gain { get; set; }

    public string? EncodingFormat { get; set; }

    public string? CoverId { get; set; }

    public string[]? CoverColors { get; set; }
    
    public AudioQualityLevel AudioQualityLevel { get; set; }

    public string? Comment { get; set; }

    public Bitmap? CoverImage
    {
        get
        {
            // 如果封面路径不存在，返回不存在封面
            if (string.IsNullOrEmpty(CoverId) || _loadingState == LoadingState.NotExist)
                return CacheManager.NotExist;

            // 如果正在加载中，返回加载中封面
            if (_loadingState == LoadingState.Loading)
                return CacheManager.Loading;

            // 尝试从缓存获取图片
            if (CacheManager.ImageCache.TryGetValue(CoverId, out var bitmap) && bitmap != null)
            {
                _loadingState = LoadingState.Loaded;

                return bitmap;
            }

            // 缓存未命中，标记为正在加载
            _loadingState = LoadingState.Loading;

            // 启动异步加载任务
            Task.Run(async () =>
            {
                try
                {
                    var dbBitmap = await MusicExtractor.LoadBitmapFromFileAsync(
                        StaticConfig.GetMusicCoverFullPath(CoverId));

                    if (dbBitmap == null)
                    {
                        _loadingState = LoadingState.NotExist;
                        OnPropertyChanged();

                        return;
                    }

                    dbBitmap = await Dispatcher.UIThread.InvokeAsync(() => BitmapCropper.Crop(dbBitmap, 1.0));
                    CacheManager.ImageCache.Add(CoverId, dbBitmap);
                    _loadingState = LoadingState.Loaded;
                    OnPropertyChanged(); // 通知 UI 更新
                }
                catch (Exception e)
                {
                    await LoggerService.ErrorAsync($"音乐《{Title}》在异步加载其封面时发生错误 : {e}\n音乐路径 : {FilePath}");
                }
            });

            // 首次或加载中时返回加载中封面
            return CacheManager.Loading;
        }
        set
        {
            if (value != null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        CoverId ??= Guid.NewGuid().ToString();
                        CacheManager.SetImage(CoverId, value);

                        await FileOperationService.SaveImageAsync(value, StaticConfig.GetMusicCoverFullPath(CoverId), true);

                        _loadingState = LoadingState.Loaded;
                        OnPropertyChanged();
                    }
                    catch (Exception e)
                    {
                        await LoggerService.ErrorAsync($"音乐《{Title}》在异步保存其封面时发生错误 : {e}\n音乐路径 : {FilePath}");
                    }
                });
            }
            else
            {
                if (CoverId == null)
                    return;

                Task.Run(() =>
                {
                    try
                    {
                        CacheManager.DeleteImage(CoverId);

                        string coverFullPath = StaticConfig.GetMusicCoverFullPath(CoverId);

                        if (File.Exists(coverFullPath))
                        {
                            File.Delete(coverFullPath);
                        }

                        CoverId = null;
                        _loadingState = LoadingState.NotExist;
                        OnPropertyChanged();
                    }
                    catch (Exception e)
                    {
                        LoggerService.Error($"在删除为音乐《{Title}》缓存的封面时发生错误 : {e}");
                    }
                });
            }
        }
    }

    [ObservableProperty] public partial string? Remarks { get; set; }

    public int LyricOffset { get; set; }

    public DateTime InsertTime { get; set; }
        
    public DateTime ModificationTime { get; set; }
}
