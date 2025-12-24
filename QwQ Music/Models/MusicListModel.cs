using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Services.Databases;
using QwQ_Music.Models.ConfigModels;

namespace QwQ_Music.Models;

public enum SortMode {
    Custom, AddTimeAscending, AddTimeDescending, NameAscending, NameDescending
}

public partial class MusicListModel : ObservableObject {
    public bool IsSelecting { get; set; }

    [ObservableProperty]
    public required partial string Name { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = "暂无简介";

    public SortMode SortMode { get; set; } = SortMode.Custom;
    public bool IsCoverExist => CoverImage != CacheManager.NotExist;

    public Bitmap CoverImage {
        get =>
            CacheManager.TryLoadCacheFromFile(
                Name,
                "音乐列表",
                "封面",
                StaticConfig.GetMusicListCoverFullPath(Name),
                () => OnPropertyChanged());
        set {
            CacheManager.SetImage(Name, value);
            OnPropertyChanged();
        }
    }

    public DateTime CreateTime { get; init; }
    public DateTime ModifyTime { get; set; }

    public List<MusicItemModel>? Musics { get; private set; }

    public Task LoadCurrentAsync() {
        return Task.Run(() => Musics = MusicListItemsRepository.Instance.GetAll(Name)
                                                               .Select(path => MusicItemsManager.All.MusicItems[path])
                                                               .ToList());
    }

    public void DisposeCurrent() { Musics = null; }
}