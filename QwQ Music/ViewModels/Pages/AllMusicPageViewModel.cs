using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
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

public partial class AllMusicPageViewModel : DataGridViewModelBase {

    [ObservableProperty]
    public partial double DataGridHorizontalScrollValue { get; set; }

    public static MusicListsManager MusicListsManager => MusicListsManager.Instance;

    public static MusicItemsManager MusicItemsManager => MusicItemsManager.All;
    
    
    protected override void OnSearchTextChanged(string? value) {
        if (string.IsNullOrEmpty(value)) {
            MusicItems = new AvaloniaList<MusicItemModel>(MusicItemsManager.All.MusicItems.Values);

            return;
        }

        var source = string.IsNullOrEmpty(value) ?
            MusicItemsManager.All.MusicItems.Values :
            MusicItemsManager.All.MusicItems.Values.Where(MatchesSearchCriteria);

        MusicItems = new AvaloniaList<MusicItemModel>(source);

        return;

        bool MatchesSearchCriteria(MusicItemModel item) {
            return item.Title.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                   item.Artists.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                   item.Album.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
    }

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
            new FilePickerOpenOptions { Title = "选择音乐文件", AllowMultiple = true }).ConfigureAwait(false);

        if (items.Count == 0)
            return;

        await AudioFileService.ProcessStorageItemsAsync(items).ConfigureAwait(false);
    }

    [RelayCommand]
    private static async Task OpenFolderAsync() {
        if (App.TopLevel == null)
            return;

        var items = await App.TopLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "选择包含音乐的文件夹", AllowMultiple = true }).ConfigureAwait(false);

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
}