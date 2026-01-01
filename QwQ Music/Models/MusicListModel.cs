using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services.Databases;
using QwQ_Music.Models.ConfigModels;

namespace QwQ_Music.Models;

public enum SortMode {
    Custom, AddTimeAscending, AddTimeDescending, NameAscending, NameDescending
}

public partial class MusicListModel : ObservableObject {
    [MemberNotNullWhen(true, nameof(Musics))]
    public bool IsLoaded { get; private set; }

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

    public string Creator { get; init; } = "未知"; // Company for Album

    public DateTime CreateTime { get; init; } // PublishTime for Album.
    public DateTime ModifyTime { get; set; }

    public virtual List<MusicItemModel>? Musics { get; private set; }

    public void AddRange(IEnumerable<MusicItemModel> musics) {
        if (!IsLoaded) {
            MusicListItemsRepository.Instance.InsertRange(this, musics);
            return;
        }

        List<MusicItemModel> items = musics.ToList();
        MusicListItemsRepository.Instance.InsertRange(this, items);
        Musics.InsertRange(0, items);
    }

    public void RemoveRange(IEnumerable<MusicItemModel> musics) {
        if (!IsLoaded) {
            MusicListItemsRepository.Instance.RemoveRange(this, musics);
            return;
        }

        List<MusicItemModel> items = musics.ToList();
        MusicListItemsRepository.Instance.RemoveRange(this, items);
        items.ForEach(item => Musics.Remove(item));
    }

    public Task LoadCurrentAsync() {
        return Task.Run(() => {
            Musics = MusicListItemsRepository.Instance.GetAll(Name)
                                             .Select(path => MusicItemsManager.All.MusicItems[path])
                                             .ToList();
            IsLoaded = true;
        });
    }

    public void DisposeCurrent() {
        IsLoaded = false;
        Musics = null;
    }
}