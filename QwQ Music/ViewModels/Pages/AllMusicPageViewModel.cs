using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.ViewModels.Pages;

public partial class AllMusicPageViewModel : MusicItemsViewModelBase {
    public AllMusicPageViewModel() {
        SetAllItems(MusicItemsManager.All.MusicItems.Values.ToList());
        MusicItemsManager.All.MusicItemsChanged += OnMusicsChanged;
        return;
        void OnMusicsChanged(object? sender, MusicItemsChangedEventArgs e) { ChangeAllItems(e.OldItems, e.NewItems); }
    }

    // public new IList SelectedItems {
    //     set => base.SelectedItems = value.Cast<MusicItemModel>().ToList();
    // }

    [ObservableProperty]
    public partial double DataGridHorizontalScrollValue { get; set; }

    public static MusicListsManager MusicListsManager => MusicListsManager.Instance;


    [RelayCommand]
    private static void JumpToTop(DataGrid dataGrid) {
        // 滚动到第一行（第一行数据）
        dataGrid.ScrollIntoView(dataGrid.CollectionView.Cast<MusicItemModel>().FirstOrDefault(), null);
    }

    [RelayCommand]
    private static async Task OpenFileAsync() {
        if (App.TopLevel == null)
            return;

        var items = await App.TopLevel.StorageProvider.OpenFilePickerAsync(
                                 new FilePickerOpenOptions { Title = "选择音乐文件", AllowMultiple = true })
                             .ConfigureAwait(false);

        if (items.Count == 0)
            return;

        await AudioFileService.ProcessStorageItemsAsync(items).ConfigureAwait(false);
    }

    [RelayCommand]
    private static async Task OpenFolderAsync() {
        if (App.TopLevel == null)
            return;

        var items = await App.TopLevel.StorageProvider.OpenFolderPickerAsync(
                                 new FolderPickerOpenOptions { Title = "选择包含音乐的文件夹", AllowMultiple = true })
                             .ConfigureAwait(false);

        if (items.Count == 0)
            return;

        await AudioFileService.ProcessStorageItemsAsync(items).ConfigureAwait(false);
    }

    [RelayCommand]
    private static async Task DropFilesAsync(DragEventArgs? e) {
        if (e?.DataTransfer.Contains(DataFormat.File) != true)
            return;

        var items = e.DataTransfer.TryGetFiles();

        if (items == null || items.Length == 0)
            return;

        await AudioFileService.ProcessStorageItemsAsync(items).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ForceRefreshMusicInfo() { await RefreshMusicItemsAsync(true).ConfigureAwait(false); }

    [RelayCommand]
    private async Task RefreshMusicInfo() { await RefreshMusicItemsAsync().ConfigureAwait(false); }

    private async Task RefreshMusicItemsAsync(bool forceRefresh = false) {
        NotificationService.Info("正在刷新音乐信息...");

        var itemsToRemove = new List<MusicItemModel>();
        var itemsToUpdate = new List<MusicItemModel>();

        await Parallel.ForEachAsync(
                          MusicItemsManager.All.MusicItems.Values,
                          async (item, _) => {
                              if (!File.Exists(item.FilePath)) {
                                  itemsToRemove.Add(item);

                                  return;
                              }

                              try {
                                  bool updated = await item.UpdateMetaDataAsync(forceRefresh).ConfigureAwait(false);

                                  if (updated) {
                                      itemsToUpdate.Add(item);
                                  }
                              } catch (Exception ex) {
                                  await LoggerService.ErrorAsync($"刷新音乐信息失败: {ex.Message}\n{ex.StackTrace}")
                                                     .ConfigureAwait(false);
                                  itemsToRemove.Add(item);
                              }
                          })
                      .ConfigureAwait(false);

        await HandleBatchOperationsAsync(itemsToRemove, itemsToUpdate).ConfigureAwait(false);

        ShowRefreshSummary(itemsToRemove.Count, itemsToUpdate.Count);
    }

    private async Task HandleBatchOperationsAsync(
        List<MusicItemModel> itemsToRemove,
        List<MusicItemModel> itemsToUpdate) {
        try {
            if (itemsToRemove.Count > 0) {
                await DeleteMusicItemsAsync(itemsToRemove).ConfigureAwait(false);
            }

            if (itemsToUpdate.Count > 0) {
                await MusicItemsManager.Update(itemsToUpdate).ConfigureAwait(false);
            }
        } catch (Exception ex) {
            await LoggerService.ErrorAsync($"更新音乐信息到数据库时发生错误: {ex.Message}\n{ex.StackTrace}").ConfigureAwait(false);
            NotificationService.Error($"更新音乐信息到数据库失败: {ex.Message}");
        }
    }

    private static void ShowRefreshSummary(int removedCount, int updatedCount) {
        if (removedCount == 0 && updatedCount == 0) {
            NotificationService.Success("所有音乐文件信息都是最新的！");

            return;
        }

        var messageParts = new List<string>();

        if (removedCount > 0)
            messageParts.Add($"删除了 {removedCount} 个不存在的音乐文件");

        if (updatedCount > 0)
            messageParts.Add($"更新了 {updatedCount} 个音乐文件的信息");

        NotificationService.Success($"刷新完成！\n{string.Join("\n", messageParts)}");
    }

    private async Task DeleteMusicItemsAsync(IEnumerable items) {
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

        var successEnumerable = await MusicItemsManager.All.RemoveAsync(musicItems).ConfigureAwait(false);

        if (successEnumerable?.ToArray() is not { Length: > 0 } successItems)
            return;

        AudioPlayManager.Instance.CheckForRemovedItems(successItems);
        successItems.AsParallel().ForAll(item => MusicItemsManager.All.MusicItems.Remove(item.FilePath));
        PlaylistManager.Instance.RemoveAllOf(successItems);
        FilteredList.RemoveAll(successItems);
    }
}