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
using QwQ_Music.Common.Manager;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Pages;

public partial class AllMusicPageViewModel() : DataGridViewModelBase(MusicItemManager.Default.MusicItems)
{
    private readonly AvaloniaList<MusicItemModel> _filterSource = [];

    [ObservableProperty] public partial double DataGridHorizontalScrollValue { get; set; }

    protected override void OnSearchTextChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            MusicItems = MusicItemManager.Default.MusicItems;

            return;
        }

        var source = string.IsNullOrEmpty(value)
            ? MusicItemManager.Default.MusicItems
            : MusicItemManager.Default.MusicItems.Where(MatchesSearchCriteria);

        _filterSource.Clear();
        _filterSource.AddRange(source);
        MusicItems = _filterSource;

        return;

        bool MatchesSearchCriteria(MusicItemModel item)
        {
            return item.Title.Contains(value, StringComparison.OrdinalIgnoreCase)
             || item.Artists.Contains(value, StringComparison.OrdinalIgnoreCase)
             || item.Album.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private void JumpToTop(DataGrid dataGrid)
    {
        // 滚动到第一行（第一行数据）
        dataGrid.ScrollIntoView(dataGrid.CollectionView.Cast<MusicItemModel>().FirstOrDefault(), null);
    }

    [RelayCommand]
    private static async Task OpenFileAsync()
    {
        if (App.TopLevel == null)
            return;

        var items = await App.TopLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "选择音乐文件",
                AllowMultiple = true,
            }
        );

        if (items.Count == 0)
            return;

        await AudioFileService.ProcessStorageItemsAsync(items);
    }

    [RelayCommand]
    private static async Task OpenFolderAsync()
    {
        if (App.TopLevel == null)
            return;

        var items = await App.TopLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "选择包含音乐的文件夹",
                AllowMultiple = true,
            }
        );

        if (items.Count == 0)
            return;

        await AudioFileService.ProcessStorageItemsAsync(items);
    }

    [RelayCommand]
    private static async Task DropFilesAsync(DragEventArgs? e)
    {
        if (e?.DataTransfer.Contains(DataFormat.File) != true)
            return;

        var items = e.DataTransfer.TryGetFiles();

        if (items == null || items.Length == 0)
            return;

        await AudioFileService.ProcessStorageItemsAsync(items);
    }

    [RelayCommand]
    private async Task ForceRefreshMusicInfo()
    {
        await RefreshMusicItemsAsync(true);
    }

    [RelayCommand]
    private async Task RefreshMusicInfo()
    {
        await RefreshMusicItemsAsync();
    }

    private async Task RefreshMusicItemsAsync(bool forceRefresh = false)
    {
        NotificationService.Info("正在刷新音乐信息...");

        var itemsToRemove = new List<MusicItemModel>();
        var itemsToUpdate = new List<MusicItemModel>();

        await Parallel.ForEachAsync(MusicItemManager.Default.MusicItems,
            async (item, _) =>
            {
                if (!File.Exists(item.FilePath))
                {
                    itemsToRemove.Add(item);

                    return;
                }

                try
                {
                    bool updated = await MusicExtractor.UpdateMusicInfoAsync(item, forceRefresh);

                    if (updated)
                    {
                        itemsToUpdate.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    await LoggerService.ErrorAsync($"刷新音乐信息失败: {ex.Message}\n{ex.StackTrace}");
                    itemsToRemove.Add(item);
                }
            });

        await HandleBatchOperationsAsync(itemsToRemove, itemsToUpdate);

        ShowRefreshSummary(itemsToRemove.Count, itemsToUpdate.Count);
    }

    private async Task HandleBatchOperationsAsync(
        List<MusicItemModel> itemsToRemove,
        List<MusicItemModel> itemsToUpdate
        )
    {
        try
        {
            if (itemsToRemove.Count > 0)
            {
                await DeleteMusicItemsAsync(itemsToRemove);
            }

            if (itemsToUpdate.Count > 0)
            {
                await MusicItemManager.Update(itemsToUpdate);
            }
        }
        catch (Exception ex)
        {
            await LoggerService.ErrorAsync($"更新音乐信息到数据库时发生错误: {ex.Message}\n{ex.StackTrace}");
            NotificationService.Error($"更新音乐信息到数据库失败: {ex.Message}");
        }
    }

    private void ShowRefreshSummary(int removedCount, int updatedCount)
    {
        if (removedCount == 0 && updatedCount == 0)
        {
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
