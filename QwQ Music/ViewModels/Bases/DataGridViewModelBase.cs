using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.ViewModels.Bases;

public partial class DataGridViewModelBase : ViewModelBase {
    protected DataGridViewModelBase() { }

    [ObservableProperty]
    public partial AvaloniaList<MusicItemModel> MusicItems { get; set; } = new(MusicItemsManager.All.MusicItems.Values);

    public string? SearchText {
        get;
        set {
            if (SetProperty(ref field, value)) {
                OnSearchTextChanged(field);
            }
        }
    }

    public List<MusicItemModel> SelectedItems { get; set; } = [];

    protected virtual void OnSearchTextChanged(string? value) { }

    [RelayCommand]
    private void SelectingItemsChanged(IList items) { SelectedItems = items.Cast<MusicItemModel>().ToList(); }

    [RelayCommand]
    private async Task SetMusicAsync(IList items) {
        await (ConfigManager.PlayerConfig.AddMusicBehavior switch {
            AddMusicBehavior.AddToNext => MusicPlayerViewModel.Current.InsertToNextAndPlayAsync(SelectedItems),
            AddMusicBehavior.SetToList => MusicPlayerViewModel.Current.ReplaceAndPlayAsync(SelectedItems),
            AddMusicBehavior.ReplaceList => MusicPlayerViewModel.Current.ReplaceAndPlayAsync(
                items.Cast<MusicItemModel>()),
            _ => throw new IndexOutOfRangeException(
                $"{ConfigManager.PlayerConfig.AddMusicBehavior} is not a valid state of {
                    nameof(ConfigManager.PlayerConfig.AddMusicBehavior)}")
        }).ConfigureAwait(false);
    }

    [RelayCommand]
    private void SelectCurrentMusicItem() { SelectedItems = [MusicPlayerViewModel.Current.CurrentMusicItem.Model]; }

    [RelayCommand]
    private static void AddToPlaylistNext(IList items) {
        if (items.Count <= 0)
            return;

        var musicItems = items.Cast<MusicItemModel>().ToList();

        PlaylistManager.Instance.InsertRange(PlaylistManager.Instance.CurrentItem, musicItems);
    }

    [RelayCommand]
    private async Task AddSelectingTo(MusicListModel? musicListModel) {
        if (SelectedItems is { Count: > 0 } && musicListModel is not null) {
            await MusicListsManager.Instance.AddToMusicList(SelectedItems, musicListModel).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task RemoveSelectingFrom(MusicListModel? musicListModel) {
        if (SelectedItems is { Count: > 0 } && musicListModel?.Name != null) {
            await MusicListsManager.Instance.RemoveToMusicList(SelectedItems, musicListModel).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private protected async Task DeleteMusicItemsAsync(IEnumerable items) {
        MusicItemModel[] musicItems = (items switch {
            IEnumerable<MusicItemModel> itemModels            => itemModels,
            IEnumerable<PlaylistItemModel> playlistItemModels => playlistItemModels.Select(item => item.Model),
            _ => throw new ArgumentOutOfRangeException(
                nameof(items),
                $"{nameof(items)} must be IEnumerable of types of MusicItemModel or PlaylistItemModel.")
        }).ToArray();
        if (musicItems.Length == 0) {
            NotificationService.Info("提示", "请先选择音乐项哦~");

            return;
        }

        var successEnumerable = await MusicItemsManager.All.DeleteAsync(musicItems).ConfigureAwait(false);

        if (successEnumerable?.ToArray() is not { Length: > 0 } successItems)
            return;

        MusicPlayerViewModel.Current.CheckForRemovedItems(successItems);
        successItems.AsParallel().ForAll(item => MusicItemsManager.All.MusicItems.Remove(item.FilePath));
        PlaylistManager.Instance.RemoveAllOf(successItems);
        MusicItems.RemoveAll(successItems);
    }
}