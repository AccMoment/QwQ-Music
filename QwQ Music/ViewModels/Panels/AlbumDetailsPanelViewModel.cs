using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Manager;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Utilities;
using QwQ_Music.Common.Utilities.StringUtilities;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;
using QwQ_Music.ViewModels.Dialogs;
using QwQ_Music.Views.Dialogs;
using Ursa.Controls;

namespace QwQ_Music.ViewModels.Panels;

public partial class AlbumDetailsPanelViewModel : DataGridViewModelBase
{
    private readonly AvaloniaList<MusicItemModel> _allMusicItems = [];

    [ObservableProperty]
    public partial AlbumItemModel AlbumItemModel { get; private set; } =
        new("Error", "#警告！你已进入未知空域，请立即离开此处（");

    public Bitmap? CoverImage
    {
        get => field ?? CacheManager.Default;
        set
        {
            if (field == value) return;

            field?.Dispose();

            field = value;
            OnPropertyChanged();
        }
    }

    public void UpdateAlbumItemModel(AlbumItemModel albumItemModel)
    {
        AlbumItemModel = albumItemModel;

        if (AlbumItemModel.Description != null)
            return;

        if (AlbumItemModel.Name == "未知专辑")
        {
            AlbumItemModel.Description = "咱不知道它专辑，获取专辑信息这事做不到呜呜！";

            return;
        }

        AlbumItemModel.Description = "专辑信息等待获取中...";

        GetAlbumDetailByNameAsync(albumItemModel);
        
        UpdateAlbumDetails();
    }

    private void UpdateAlbumDetails()
    {
        _allMusicItems.Clear();
        _allMusicItems.AddRange(SearchMusicItems(AlbumItemModel));
        
        OnSearchTextChanged(SearchText);

        if (_allMusicItems.Count == 0)
        {
            NotificationService.Warning("当前专辑内容为空，可能是专辑音乐被全部删除！");

            return;
        }

        UpdateCoverImage(_allMusicItems.First());
    }

    private async void UpdateCoverImage(MusicItemModel musicItem)
    {
        try
        {
            var bitmap = await MusicExtractor.GetCoverFromAudioAsync(musicItem.FilePath);

            if (bitmap == null)
                return;

            CoverImage = ConfigManager.UiConfig.CoverConfig.AllowNonSquareCover
                ? bitmap
                : await Dispatcher.UIThread.InvokeAsync(() => BitmapCropper.Crop(bitmap, 1.0));
        }
        catch (Exception e)
        {
            NotificationService.Error("加载大专辑封面时出错！");
            await LoggerService.ErrorAsync($"更新专辑详情页封面时出错 : \n{e.Message}\n{e.StackTrace}");
        }
    }

    private static IEnumerable<MusicItemModel> SearchMusicItems(AlbumItemModel albumItem)
    {
        // 找到该专辑对应的所有音乐项
        var albumMusicItems = MusicItemManager
            .Default.MusicItems.Where(music => music.Album == albumItem.Name && music.Artists == albumItem.Artist);

        return albumMusicItems;
    }

    private async void GetAlbumDetailByNameAsync(AlbumItemModel album)
    {
        try
        {
            using var crawler = new NetEaseAlbumCrawler();
            var albumDetail = await crawler.GetAlbumDetailByNameAsync(album.Name, album.Artist);

            AlbumItemModel.Description = StringCleaner.ToPlainText(albumDetail.Description);
            AlbumItemModel.PublishTime = albumDetail.PublishTime;
            AlbumItemModel.Company = albumDetail.Company;
        }
        catch (NetEaseAlbumCrawlerException ex)
        {
            await LoggerService.ErrorAsync($"爬虫异常: {ex.Message}");
        }
        catch (Exception ex)
        {
            await LoggerService.ErrorAsync($"其他异常: {ex.Message}");
        }
    }

    protected override void OnSearchTextChanged(string? value)
    {
        var source = string.IsNullOrEmpty(value)
            ? _allMusicItems
            : _allMusicItems.Where(MatchesSearchCriteria);

        MusicItems.Clear();
        MusicItems.AddRange(source);

        return;

        bool MatchesSearchCriteria(MusicItemModel item)
        {
            return item.Title.Contains(value, StringComparison.OrdinalIgnoreCase)
             || item.Artists.Contains(value, StringComparison.OrdinalIgnoreCase)
             || item.Album.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private async Task PlayAlbumMusic()
    {
        if (MusicItems.Count < 0)
            return;

        try
        {
            MusicPlayList.Toggle(MusicItems);

            await MusicPlayerViewModel.PlayThisMusic(MusicPlayList.First());
        }
        catch (Exception ex)
        {
            // 可以在这里添加错误日志记录
            NotificationService.Error("错误", $"播放专辑中的音乐时出错: {ex.Message}");
            await LoggerService.ErrorAsync($"播放专辑中的音乐时出错:\n {ex.Message}\n{ex.StackTrace}");
        }
    }

    [RelayCommand]
    private async Task ViewCompleteIntroduction()
    {
        if (AlbumItemModel.Description == null)
            return;

        var options = new OverlayDialogOptions
        {
            Title = "专辑简介",
            Mode = DialogMode.Info,
        };

        await OverlayDialog.ShowCustomModal<ViewText, ViewTextViewModel, object>(
            new ViewTextViewModel(AlbumItemModel.Description, options.Title), options: options);
    }

    [RelayCommand]
    private static void BackAllAlbum()
    {
        NavigateService.NavigateTo("全部专辑");
        
    }
}
