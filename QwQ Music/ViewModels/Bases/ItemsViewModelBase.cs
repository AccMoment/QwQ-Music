using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;

namespace QwQ_Music.ViewModels.Bases;

public abstract partial class ItemsViewModelBase<T> : ViewModelBase {
    [ObservableProperty]
    public partial double DataGridHorizontalScrollValue { get; set; }

    private List<T> _allItemsList = [];

    [ObservableProperty]
    public partial AvaloniaList<T> FilteredList { get; set; } = [];

    public string SearchText {
        get;
        set {
            if (SetProperty(ref field, value)) {
                OnSearchTextChanged(field);
            }
        }
    } = "";

    public List<T> SelectedItems { get; set; } = [];

    public void SetAllItems(List<T> items) {
        _allItemsList = items;
        OnSearchTextChanged(SearchText);
    }

    public void ChangeAllItems(IEnumerable<T>? oldItems, IEnumerable<T>? newItems) {
        if (oldItems is not null) {
            _allItemsList.RemoveAll(oldItems.Contains);
        }

        if (newItems is not null) {
            _allItemsList.AddRange(newItems);
        }

        OnSearchTextChanged(SearchText);
    }

    protected void OnSearchTextChanged(string value) {
        if (string.IsNullOrEmpty(value)) {
            if (FilteredList.Count != _allItemsList.Count) {
                FilteredList = new AvaloniaList<T>(_allItemsList);
            }

            return;
        }

        FilteredList = new AvaloniaList<T>(_allItemsList.AsParallel().Where(item => CustomFilter(in value, in item)));
    }

    protected abstract bool CustomFilter(ref readonly string value, ref readonly T item);

    [RelayCommand]
    private void SelectedItemsChanged(IList items) { SelectedItems = items.Cast<T>().ToList(); }

    [RelayCommand]
    private void ScrollToTop(DataGrid dataGrid) {
        // 滚动到第一行（第一行数据）
        dataGrid.ScrollIntoView(dataGrid.CollectionView.Cast<T>().FirstOrDefault(), null);
    }
}

public partial class MusicItemsViewModelBase : ItemsViewModelBase<MusicItemModel> {
    [RelayCommand]
    private void SetMusicList(MusicListModel? model) {
        string name;
        IEnumerable<MusicItemModel> items;
        if (model is not null) {
            name = model.Name;
            items = model.Musics!;
        } else {
            name = MusicItemsManager.All.Name;
            items = MusicItemsManager.All.MusicItems.Values;
        }

        (ConfigManager.PlayerConfig.AddMusicBehavior switch {
                AddMusicBehavior.AddToNext => Task.FromResult(PlaylistManager.Instance.InsertToNext(SelectedItems)),
                AddMusicBehavior.SetToList => PlaylistManager.Instance.ReplaceAsync(
                    PlaylistManager.CUSTOM,
                    SelectedItems,
                    SelectedItems[0],
                    true),
                AddMusicBehavior.ReplaceList => PlaylistManager.Instance.ReplaceAsync(
                    name,
                    items,
                    items.Single(i => i == SelectedItems[0]),
                    true),
                _ => throw new IndexOutOfRangeException(
                    $"{ConfigManager.PlayerConfig.AddMusicBehavior} is not a valid state of {
                        nameof(ConfigManager.PlayerConfig.AddMusicBehavior)}")
            }).ContinueWith(LoggerService.HandleException)
              .ConfigureAwait(false);
    }

    [RelayCommand]
    private void SelectCurrentMusicItem() { SelectedItems = [AudioPlayManager.Instance.CurrentMusicItem.Model]; }

    [RelayCommand]
    private static void AddToPlaylistNext(IList items) {
        if (items.Count <= 0)
            return;

        List<MusicItemModel> musicItems = items.Cast<MusicItemModel>().ToList();

        PlaylistManager.Instance.InsertRange(PlaylistManager.Instance.CurrentItem, musicItems);
    }

    [RelayCommand]
    private void AddSelectedTo(MusicListModel? musicListModel) {
        if (SelectedItems is { Count: > 0 } && musicListModel is not null) {
            MusicListsManager.Instance.AddToMusicListAsync(SelectedItems, musicListModel)
                             .ContinueWith(LoggerService.HandleException)
                             .ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private void RemoveSelectedFrom(MusicListModel? musicListModel) {
        if (SelectedItems is { Count: > 0 } && musicListModel?.Name != null) {
            MusicListsManager.Instance.RemoveToMusicList(SelectedItems, musicListModel)
                             .ContinueWith(LoggerService.HandleException)
                             .ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private void PlayMusicList() {
        if (FilteredList.Count == 0)
            return;

        PlaylistManager.Instance.ReplaceAsync(PlaylistManager.CUSTOM, FilteredList, isPlayNow: true)
                       .ContinueWith(LoggerService.HandleException)
                       .ConfigureAwait(false);
    }

    protected override bool CustomFilter(ref readonly string value, ref readonly MusicItemModel item) {
        //TODO TAGS
        return item.Title.Contains(value, StringComparison.OrdinalIgnoreCase) ||
               item.Artists.Contains(value, StringComparison.OrdinalIgnoreCase) ||
               item.Album.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}