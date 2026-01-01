using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Services.Databases;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.ViewModels.Dialogs;
using QwQ_Music.Views.Dialogs;
using Ursa.Controls;

namespace QwQ_Music.Common.Managers;

public partial class MusicListsManager : ObservableObject {
    public static MusicListsManager Instance { get; } = new();
    private MusicListsManager() { InitializeAsync().ContinueWith(LoggerService.HandleException).ConfigureAwait(false); }

    public List<MusicListModel> MusicLists { get; set; } = [];

    public MusicListModel? Selected { get; private set; }


    private async Task InitializeAsync() {
        try {
            MusicLists.AddRange(await Task.Run(() => MusicListRepository.Instance.GetAll()).ConfigureAwait(true));
        } catch (Exception e) {
            NotificationService.Error("歌单信息加载失败！\n" + $"{e.Message}");

            await LoggerService.ErrorAsync("歌单信息加载失败！\n" + $"{e.Message}\n{e.StackTrace}").ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     添加歌单信息
    /// </summary>
    /// <param name="model">歌单模型</param>
    private void AddMusicList(MusicListModel model) {
        MusicListRepository.Instance.Insert(model);

        MusicLists.Add(model);
        NotificationService.Success("成功", $"歌单《{model.Name}》创建成功！");
    }

    [RelayCommand]
    private void CreatePlaylistWithMusicItem(IList items) {
        CreateMusicListAsync()
            .ContinueWith(task => {
                if (task is { IsCompletedSuccessfully: true, Result: { } list }) {
                    AddToMusicListAsync(items.Cast<MusicItemModel>().ToList(), list)
                        .ContinueWith(LoggerService.HandleException)
                        .ConfigureAwait(false);
                }
            })
            .ContinueWith(LoggerService.HandleException)
            .ConfigureAwait(false);
    }

    [RelayCommand]
    public void CreateMusicList() {
        CreateMusicListAsync().ContinueWith(LoggerService.HandleException).ConfigureAwait(false);
    }

    private async Task<MusicListModel?> CreateMusicListAsync() {
        var options = new OverlayDialogOptions { Title = "新建歌单" };

        var model = await OverlayDialog.ShowCustomModal<CreateMusicList, CreateMusicListViewModel, MusicListModel>(
                                           new CreateMusicListViewModel(options.Title),
                                           options: options)
                                       .ConfigureAwait(true);

        if (model == null)
            return null;

        try {
            AddMusicList(model);

            string coverFullPath = StaticConfig.GetMusicListCoverFullPath(model.Name);

            if (model.CoverImage == CacheManager.NotExist)
                return model;

            if (!await FileOperationService.SaveImageAsync(model.CoverImage, coverFullPath, true)
                                           .ConfigureAwait(false)) {
                NotificationService.Error($"保存歌词 {model.Name} 的图标失败啦~");
            }

            return model;
        } catch (Exception ex) {
            await LoggerService.ErrorAsync($"创建《{model.Name}》歌单失败！\n" + $"{ex.Message}\n{ex.StackTrace}")
                               .ConfigureAwait(false);

            NotificationService.Error($"创建歌单《{model.Name}》失败！\n" + $"{ex.Message}");

            return null;
        }
    }

    /// <summary>
    ///     批量添加音乐项到指定名称歌单
    /// </summary>
    /// <param name="musicItems">音乐项列表</param>
    /// <param name="musicList">歌单项</param>
    public async Task AddToMusicListAsync(IList<MusicItemModel> musicItems, MusicListModel musicList) {
        if (musicItems.Count == 0)
            return;

        var repo = MusicListItemsRepository.Instance;

        // 过滤掉已存在的音乐项
        var newItems = musicItems.Where(item => repo.Contains(musicList.Name, item.FilePath)).ToList();

        // 如果有已存在的音乐项，显示提示
        if (newItems.Count != musicItems.Count) {
            List<MusicItemModel> existingItems = musicItems.Except(newItems).ToList();
            string existingTitles = string.Join("、", existingItems.Select(item => $"《{item.Title}》"));
            NotificationService.Info("提示", $"歌曲{existingTitles}已存在于歌单 {musicList.Name} 中！");
        }

        var failedItems = new List<MusicItemModel>();

        await Task.Run(() => {
                      foreach (MusicItemModel item in newItems) {
                          try {
                              repo.Insert(musicList, item);
                          } catch (Exception) {
                              failedItems.Add(item);
                          }
                      }
                  })
                  .ConfigureAwait(false);

        List<MusicItemModel> successItems;
        if (failedItems.Count == 0) {
            successItems = newItems;
        } else {
            successItems = newItems.Except(failedItems).ToList();
            string failedTitles = string.Join("、", failedItems.Select(item => $"《{item.Title}》"));
            NotificationService.Error($"添加歌曲{failedTitles}到歌单失败！");
        }

        // 显示添加结果通知
        if (successItems.Count > 0) {
            string successTitles = string.Join("、", successItems.Select(item => $"《{item.Title}》"));
            NotificationService.Success($"已将歌曲{successTitles}添加到歌单：{musicList.Name}！");

            if (musicList.Name == Selected?.Name) {
                Selected.Musics!.AddRange(successItems);
            }
        }
    }

    /// <summary>
    ///     批量从指定名称歌单中移除音乐项
    /// </summary>
    /// <param name="musicItems">音乐项列表</param>
    /// <param name="musicList">歌单项</param>
    public async Task RemoveToMusicList(IList<MusicItemModel> musicItems, MusicListModel musicList) {
        if (musicItems.Count == 0)
            return;

        var failedItems = new List<MusicItemModel>();

        await Task.Run(() => {
                      var repo = MusicListItemsRepository.Instance;

                      foreach (var item in musicItems) {
                          try {
                              repo.Remove(musicList, item);
                          } catch (Exception) {
                              failedItems.Add(item);
                          }
                      }
                  })
                  .ConfigureAwait(false);
        IList<MusicItemModel> successItems;
        if (failedItems.Count == 0) {
            successItems = musicItems;
        } else {
            successItems = musicItems.Except(failedItems).ToList();
            string failedTitles = string.Join("、", failedItems.Select(item => $"《{item.Title}》"));
            NotificationService.Error($"从歌单移除歌曲{failedTitles}失败！");
        }

        // 显示移除结果通知
        if (successItems.Count > 0) {
            string successTitles = string.Join("、", successItems.Select(item => $"《{item.Title}》"));

            NotificationService.Success($"已将歌曲{successTitles}从歌单 {musicList.Name} 中移除！");

            if (musicList.Name == Selected?.Name) {
                foreach (MusicItemModel successItem in successItems) {
                    Selected.Musics!.Remove(successItem);
                }
            }
        }
    }

    [RelayCommand]
    private void DeleteMusicList(MusicListModel musicList) {
        MessageBox.ShowOverlayAsync(
                      $"你真的要删除歌单《{musicList.Name}》吗?",
                      "警告",
                      icon: MessageBoxIcon.Warning,
                      button: MessageBoxButton.YesNo)
                  .ContinueWith(task => {
                      if (task is not { IsCompletedSuccessfully: true, Result: MessageBoxResult.Yes })
                          return;
                      try {
                          MusicListRepository.Instance.Delete(musicList.Name);
                          // 删除封面图片文件
                          if (!musicList.IsCoverExist)
                              return;
                          string coverFullPath = StaticConfig.GetMusicListCoverFullPath(musicList.Name);
                          if (File.Exists(coverFullPath)) {
                              File.Delete(coverFullPath);
                          }

                          // 从图片缓存中移除
                          if (!musicList.IsCoverExist) {
                              CacheManager.ImageCache.Remove(musicList.Name);
                          }

                          MusicLists.Remove(musicList);
                          NotificationService.Success($"歌单《{musicList.Name}》删除成功！");
                      } catch (Exception ex) {
                          LoggerService.Error($"删除歌单失败:\n{ex.Message}\n{ex.StackTrace}");
                          NotificationService.Error($"删除歌单《{musicList.Name}》失败！\n{ex.Message}");
                          throw;
                      }
                  })
                  .ContinueWith(LoggerService.HandleException)
                  .ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task AddToMusicListAsync((MusicItemModel musicItem, MusicListModel musicList) argument) {
        await AddToMusicListAsync([argument.musicItem], argument.musicList).ConfigureAwait(false);
    }

    [RelayCommand]
    private static async Task EditMusicListName(MusicListModel musicList) {
        var options = new OverlayDialogOptions { Title = "修改名称" };

        string? result = await OverlayDialog.ShowCustomModal<EditText, EditTextViewModel, string>(
                                                new EditTextViewModel(musicList.Name, options.Title, 64),
                                                options: options)
                                            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(result))
            return;

        try {
            MusicListRepository.Instance.Update(musicList.Name, [nameof(musicList.Name)], [musicList.Name]);
            musicList.Name = result;

            NotificationService.Success($"修改歌单 {musicList.Name}的名称成功了");
        } catch (Exception e) {
            await LoggerService.ErrorAsync($"修改歌单 {musicList.Name} 的名称失败了:\n{e.Message}\n{e.StackTrace}")
                               .ConfigureAwait(false);

            NotificationService.Error($"修改歌单 {musicList.Name} 的名称失败了");
        }
    }

    [RelayCommand]
    private static async Task EditMusicListDescription(MusicListModel musicList) {
        var options = new OverlayDialogOptions { Title = "修改描述", Mode = DialogMode.Info };

        string? result = await OverlayDialog.ShowCustomModal<EditText, EditTextViewModel, string>(
                                                new EditTextViewModel(musicList.Description, options.Title),
                                                options: options)
                                            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(result))
            return;

        try {
            MusicListRepository.Instance.Update(
                musicList.Name,
                [nameof(musicList.Description)],
                [musicList.Description]);

            musicList.Name = result;

            NotificationService.Success($"修改歌单 {musicList.Name}的描述成功了");
        } catch (Exception e) {
            await LoggerService.ErrorAsync($"修改歌单 {musicList.Name} 的描述失败了:\n{e.Message}\n{e.StackTrace}")
                               .ConfigureAwait(false);

            NotificationService.Error($"修改歌单 {musicList.Name} 的描述失败了");
        }
    }

    [RelayCommand]
    private static async Task EditMusicListCover(MusicListModel musicList) {
        if (App.TopLevel == null)
            return;

        var options = new OverlayDialogOptions { Title = "图片裁剪" };

        var bitmap = await FileOperationService.OpenImageFile(App.TopLevel).ConfigureAwait(false);

        if (bitmap == null)
            return;

        var newCover = await OverlayDialog.ShowCustomModal<ImageCropping, ImageCroppingViewModel, Bitmap>(
                                              new ImageCroppingViewModel(bitmap),
                                              options: options)
                                          .ConfigureAwait(false);

        if (newCover == null)
            return;

        musicList.CoverImage = newCover;

        string coverFullPath = StaticConfig.GetMusicListCoverFullPath(musicList.Name);

        if (await FileOperationService.SaveImageAsync(newCover, coverFullPath, true).ConfigureAwait(false)) {
            NotificationService.Success($"修改歌单 {musicList.Name} 的图标成功啦~");
        } else {
            NotificationService.Error($"修改歌单 {musicList.Name} 的图标失败啦~");
        }
    }
}