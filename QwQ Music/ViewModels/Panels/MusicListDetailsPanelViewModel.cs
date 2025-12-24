using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Helper;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Services.Databases;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.ViewModels.Bases;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.ViewModels.Panels;

public partial class MusicListDetailsPanelViewModel : DataGridViewModelBase {
    private readonly AvaloniaList<MusicItemModel> _filterSource = [];

    public MusicListsManager MusicListsManager => MusicListsManager.Instance;
    public MusicItemsManager MusicItemsManager => MusicItemsManager.All;

    [ObservableProperty]
    public partial MusicListModel? MusicListModel { get; set; }

    public Bitmap? CoverImage {
        get => field ?? CacheManager.Default;
        set {
            if (field == value)
                return;

            field?.Dispose();

            field = value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    public partial double DataGridHorizontalScrollValue { get; set; }

    protected override void OnSearchTextChanged(string? value) {
        if (string.IsNullOrEmpty(value)) {
            MusicItems = new AvaloniaList<MusicItemModel>(MusicListsManager.Selected!.Musics!);

            return;
        }

        IEnumerable<MusicItemModel> source = string.IsNullOrEmpty(value) ?
            MusicListsManager.Selected!.Musics! :
            MusicListsManager.Selected!.Musics!.Where(MatchesSearchCriteria);

        _filterSource.Clear();
        _filterSource.AddRange(source);
        MusicItems = _filterSource;

        return;

        bool MatchesSearchCriteria(MusicItemModel item) {
            return item.Title.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                   item.Artists.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                   item.Album.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task UpdateMusicListModelAsync(MusicListModel musicListModel) {
        try {
            if (musicListModel.Name == MusicListModel?.Name)
                return;

            MusicListModel = musicListModel;
            await UpdateCoverImageAsync(musicListModel).ConfigureAwait(true);

            MusicListsManager.Selected?.Name = musicListModel.Name;

            MusicListsManager.Selected!.Musics!.Clear();
            MusicListsManager.Selected.Musics.AddRange(
                MusicListItemsRepository.Instance.GetAll(MusicListModel.Name)
                                        .Select(path => MusicItemsManager.All.MusicItems[path]));

            MusicItems = new AvaloniaList<MusicItemModel>(MusicListsManager.Selected.Musics);
        } catch (Exception e) {
            await LoggerService.ErrorAsync($"更新歌单信息时发生错误！\n{e.Message}\n{e.StackTrace}").ConfigureAwait(false);
            NotificationService.Error($"更新歌单信息时发生错误！\n{e.Message}");
        }
    }

    private async Task UpdateCoverImageAsync(MusicListModel musicList) {
        try {
            if (!musicList.IsCoverExist)
                return;

            // ReSharper disable once UseConfigureAwaitFalse
            CoverImage = await ImageHelper.LoadFromFileAsync(StaticConfig.GetMusicListCoverFullPath(musicList.Name));
        } catch (Exception e) {
            NotificationService.Error("加载大专辑封面时出错！");
            await LoggerService.ErrorAsync($"更新专辑详情页封面时出错 : \n{e.Message}\n{e.StackTrace}").ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task PlayMusicList() {
        if (MusicItems.Count <= 0)
            return;

        PlaylistManager.Instance.Replace(MusicItems);

        await MusicPlayerViewModel.Current.PlayThisMusicAsync(PlaylistManager.Instance.First()).ConfigureAwait(false);
    }

    [RelayCommand]
    private void JumpToTop(DataGrid dataGrid) {
        // 滚动到第一行（第一行数据）
        dataGrid.ScrollIntoView(dataGrid.CollectionView.Cast<MusicItemModel>().FirstOrDefault(), null);
    }

    [RelayCommand]
    private static void BackAllAlMusicList() { NavigateService.NavigateTo("全部歌单"); }
}