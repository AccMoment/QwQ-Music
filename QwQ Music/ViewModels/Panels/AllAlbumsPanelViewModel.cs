using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ATL;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.ViewModels.Bases;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.ViewModels.Panels;

public partial class AllAlbumsPanelViewModel : ItemsViewModelBase<AlbumModel> {
    public AllAlbumsPanelViewModel() {
        MusicItemsManager.All.MusicItemsChanged += MusicItemsOnCollectionChanged;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_OnProcessExit;
    }

    public StyleConfig StyleConfig { get; } = ConfigManager.UiConfig.StyleConfig;

    [ObservableProperty]
    public partial AvaloniaList<AlbumModel> AlbumItems { get; set; } = [];

    [ObservableProperty]
    public partial AlbumModel? SelectedAlbumItem { get; set; }

    private void CurrentDomain_OnProcessExit(object? sender, EventArgs e) {
        MusicItemsManager.All.MusicItemsChanged -= MusicItemsOnCollectionChanged;
        AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_OnProcessExit;
    }

    private void MusicItemsOnCollectionChanged(object? sender, MusicItemsChangedEventArgs e) {
        e.OldItems?.ForEach(RemoveAlbumItemIfNecessary);
        e.NewItems?.ForEach(item => AddOrUpdateAlbumItem(item, false));

        // 更新过滤后的结果（考虑搜索框）
        OnSearchTextChanged(SearchText);
    }

    // 添加或更新专辑项
    private void AddOrUpdateAlbumItem(MusicItemModel musicItem, bool force) {
        {
            bool isAlbumInfoIncomplete = string.IsNullOrWhiteSpace(musicItem.Album) ||
                                         string.IsNullOrWhiteSpace(musicItem.Artists);
            if (!force && !isAlbumInfoIncomplete)
                return;
        }

        AlbumModel? album =
            AllItemsList.FirstOrDefault(a => a.Name == musicItem.Album && a.Artists == musicItem.AlbumArtists);

        if (album is not null)
            return;
        album = new AlbumModel { Name = musicItem.Album, Artists = musicItem.Artists };
        UpdateAlbumProperties(album);
        AllItemsList.Add(album);
    }

    private void UpdateAlbumProperties(AlbumModel model) {
        bool isDescriptionExist = string.IsNullOrWhiteSpace(model.Description);
        bool isPublishTimeExist = model.PublishTime == null;
        bool isCompanyExist = string.IsNullOrWhiteSpace(model.Company);
        bool needUpdate = isDescriptionExist || isPublishTimeExist || isCompanyExist;
        if (!needUpdate)
            return;
        _ = model.UpdateAsync().ContinueWith(LoggerService.HandleException).ConfigureAwait(false);
    }

    // 如果该音乐是某专辑的最后一首，则移除该专辑
    private void RemoveAlbumItemIfNecessary(MusicItemModel musicItem) {
        if (string.IsNullOrWhiteSpace(musicItem.Album) || string.IsNullOrWhiteSpace(musicItem.Artists))
            return;

        bool hasOtherMusicsInSameAlbum =
            MusicItemsManager.All.MusicItems.Values.Any(m => m.Album == musicItem.Album &&
                                                             m.Artists == musicItem.Artists);

        if (hasOtherMusicsInSameAlbum)
            return;

        var albumToRemove =
            AllItemsList.FirstOrDefault(a => a.Name == musicItem.Album && a.Artists == musicItem.Artists);

        if (albumToRemove != null) {
            AllItemsList.Remove(albumToRemove);
        }
    }

    // 重建整个专辑列表
    private void RebuildAllAlbumItems() {
        AllItemsList.Clear();
        foreach (MusicItemModel item in MusicItemsManager.All.MusicItems.Values) {
            AddOrUpdateAlbumItem(item, true);
        }
    }


    [RelayCommand]
    private static void PlayAlbumMusic(AlbumModel? albumItem) {
        if (albumItem == null)
            return;
        var items = SearchMusicItems(albumItem).ToList();
        PlaylistManager.Instance.ReplaceAsync(albumItem.Name, items, 0, true)
                       .ContinueWith(LoggerService.HandleException)
                       .ConfigureAwait(false);
    }

    private static IEnumerable<MusicItemModel> SearchMusicItems(AlbumModel album) {
        // 找到该专辑对应的所有音乐项
        var albumMusicItems =
            MusicItemsManager.All.MusicItems.Values.Where(music =>
                                                              music.Album == album.Name &&
                                                              string.IsNullOrWhiteSpace(music.AlbumArtists) ?
                                                                  music.Artists.Contains(album.Artists) :
                                                                  music.AlbumArtists.Contains(album.Artists));

        return albumMusicItems;
    }

    protected override bool CustomFilter(ref readonly string value, ref readonly AlbumModel item) {
        return item.Name.Contains(value, StringComparison.OrdinalIgnoreCase) ||
               item.Artists.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}