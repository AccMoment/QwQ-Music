using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Manager;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QwQ_Music.ViewModels.Pages;

public partial class AllMusicPageViewModel() : DataGridViewModelBase(MusicItemManager.Default.MusicItems)
{
    private readonly AvaloniaList<MusicItemModel> _filterSource = [];
    
    [ObservableProperty]
    public partial double DataGridHorizontalScrollValue { get; set; }

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
        if (e?.Data.Contains(DataFormats.Files) != true)
            return;

        var items = e.Data.GetFiles()?.ToList();

        if (items == null || items.Count == 0)
            return;

        await AudioFileService.ProcessStorageItemsAsync(items);
    }

    [RelayCommand]
    private async Task RefreshMusicInfo()
    {
        // 显示加载提示
        NotificationService.Info("正在刷新音乐信息...");

        var musicItemsToRemove = new List<MusicItemModel>();
        var musicItemsToUpdate = new List<MusicItemModel>();

        // 遍历所有音乐项进行检查
        foreach (var musicItem in MusicItemManager.Default.MusicItems.ToList())
        {
            // 检查文件是否存在
            if (!File.Exists(musicItem.FilePath))
            {
                musicItemsToRemove.Add(musicItem);

                continue;
            }

            // 重新提取音乐信息
            try
            {
                bool result = await MusicExtractor.UpdateMusicInfoAsync(musicItem);

                // 检查信息是否发生变化
                if (result)
                {
                    musicItemsToUpdate.Add(musicItem);
                }
            }
            catch (Exception ex)
            {
                await LoggerService.ErrorAsync($"刷新音乐信息失败: \n{musicItem.Title}\n{ex.Message}");

                // 如果提取失败，标记为需要删除
                musicItemsToRemove.Add(musicItem);
            }
        }

        try
        {
            // 批量删除不存在的音乐项
            if (musicItemsToRemove.Count > 0)
            {
                await DeleteMusicItemsAsync(musicItemsToRemove);
            }

            // 批量更新发生变化的音乐项
            if (musicItemsToUpdate.Count > 0)
            {
                await MusicItemManager.Update(musicItemsToUpdate);
            }
        }
        catch (Exception ex)
        {
            await LoggerService.ErrorAsync($"刷新音乐信息时发生错误: {ex.Message}\n{ex.StackTrace}");
            NotificationService.Error($"刷新音乐信息失败: {ex.Message}");
        }

        // 显示刷新结果
        int removedCount = musicItemsToRemove.Count;
        int updatedCount = musicItemsToUpdate.Count;

        if (removedCount > 0 || updatedCount > 0)
        {
            string message = "";

            if (removedCount > 0)
                message += $"删除了 {removedCount} 个不存在的音乐文件\n";

            if (updatedCount > 0)
                message += $"更新了 {updatedCount} 个音乐文件的信息";

            NotificationService.Success($"刷新完成！\n{message}");
        }
        else
        {
            NotificationService.Success("所有音乐文件信息都是最新的！");
        }
    }
}
