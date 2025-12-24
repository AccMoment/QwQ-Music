using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.Models.Enums;

namespace QwQ_Music.Models;

public partial class AlbumItemModel(string name, string artist, string? coverFileName = null) : ObservableObject {
    public string Name { get; } = name;

    public string Artist { get; } = artist;

    [ObservableProperty]
    public partial string? Description { get; set; }

    [ObservableProperty]
    public partial string? PublishTime { get; set; }

    [ObservableProperty]
    public partial string? Company { get; set; }

    public Bitmap CoverImage =>
        CacheManager.TryLoadCacheFromFile(coverFileName, "专辑", "封面", StaticConfig.GetMusicCoverFullPath(coverFileName));
}