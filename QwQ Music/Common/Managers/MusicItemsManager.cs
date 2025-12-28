using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Services.Databases;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Dialogs;
using QwQ_Music.Views.Dialogs;
using Ursa.Controls;

namespace QwQ_Music.Common.Managers;

public class MusicItemsChangedEventArgs : EventArgs {
    public required List<MusicItemModel>? OldItems { get; init; }
    public required List<MusicItemModel>? NewItems { get; init; }
}

public partial class MusicItemsManager : ObservableObject {
    private MusicItemsManager() { InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult(); }
    public static MusicItemsManager All { get; } = new();

    public event EventHandler<MusicItemsChangedEventArgs>? MusicItemsChanged;

    public Dictionary<string, MusicItemModel> MusicItems { get; private set; } = new(); //Initialized in .ctor
    public MusicItemModel this[string key] => MusicItems[key];
    public int Count => MusicItems.Count;

    private async Task InitializeAsync() {
        try {
            MusicItems = new Dictionary<string, MusicItemModel>(
                await Task.Run(() => MusicItemRepository.Instance.GetAll()
                                                        .ToDictionary(item => item.FilePath, item => item))
                          .ConfigureAwait(false));

            if (MusicItems.Count != 0)
                return;

            NotificationService.Info("好像...一首歌都没有（ \n " + "Tips : 可以点击右上角加号从文件中添加音乐哦！");
        } catch (Exception ex) {
            await LoggerService.ErrorAsync($"初始化音乐项出错: \n{ex.Message}\n{ex.StackTrace}").ConfigureAwait(false);
            NotificationService.Error($"初始化音乐项出错: {ex.Message}");
        }
    }

    public async Task<IEnumerable<MusicItemModel>> AddAsync(IList<MusicItemModel> musicItems) {
        var successItems = new List<MusicItemModel>();

        await Task.Run(() => {
                      var repo = MusicItemRepository.Instance;

                      foreach (var musicItem in musicItems) {
                          try {
                              musicItem.InsertTime = DateTime.UtcNow;
                              repo.Insert(musicItem);

                              successItems.Add(musicItem);
                          } catch (Exception e) {
                              LoggerService.Error($"歌曲《{musicItem.Title}》保存到数据库失败！\n{e.Message}\n{e.StackTrace}");
                          }
                      }
                  })
                  .ConfigureAwait(false);

        // 批量添加到UI集合
        Dispatcher.UIThread.Post(() => successItems.ForEach(item => MusicItems.Add(item.FilePath, item)));

        var failedItems = musicItems.Except(successItems).ToList();

        if (successItems.Count > 0) {
            string existingTitles = string.Join("、", musicItems.Select(items => $"《{items.Title}》"));

            NotificationService.Success($"歌曲 {existingTitles} 添加成功啦~");
        }

        if (failedItems.Count > 0) {
            string failedTitles = string.Join("、", failedItems.Select(item => $"《{item.Title}》"));
            NotificationService.Error($"歌曲 {failedTitles} 添加失败了！");
        }

        MusicItemsChanged?.Invoke(this, new MusicItemsChangedEventArgs() { OldItems = null, NewItems = successItems });
        return successItems;
    }

    public static void Update(MusicItemModel musicItem) {
        try {
            MusicItemRepository.Instance.Update(musicItem);
        } catch (Exception e) {
            LoggerService.Error($"更新歌曲{musicItem.Title}到数据库失败！\n{e.Message}\n{e.StackTrace}");
            NotificationService.Error($"更新歌曲{musicItem.Title}到数据库失败！\n{e.Message}");
        }
    }

    public static async Task Update(IList<MusicItemModel> musicItems) {
        var successItems = new List<MusicItemModel>();

        await Task.Run(() => {
                      var repo = MusicItemRepository.Instance;
                      foreach (MusicItemModel musicItem in musicItems) {
                          try {
                              repo.Update(musicItem);
                              successItems.Add(musicItem);
                          } catch (Exception e) {
                              LoggerService.Error($"更新歌曲{musicItem.Title}到数据库失败！\n{e.Message}\n{e.StackTrace}");
                          }
                      }
                  })
                  .ConfigureAwait(false);

        List<MusicItemModel> failedItems = musicItems.Except(successItems).ToList();

        // 显示删除结果通知
        if (successItems.Count > 0) {
            string successTitles = string.Join("、", successItems.Select(item => $"《{item.Title}》"));
            NotificationService.Success($"{successTitles}更新成功了！");
        }

        if (failedItems.Count > 0) {
            string failedTitles = string.Join("、", failedItems.Select(item => $"《{item.Title}》"));
            NotificationService.Error($"更新{failedTitles}失败了！");
        }
    }

    public static void Update(MusicItemModel musicItem, Dictionary<string, object?> fields) {
        try {
            MusicItemRepository.Instance.Update(musicItem.FilePath, fields);
        } catch (Exception e) {
            LoggerService.ErrorAsync($"更新歌曲《{musicItem.Title}》信息到数据库时发生错误 : \n{e}");
            NotificationService.Error($"更新歌曲《{musicItem.Title}》信息到数据库时发生错误 : \n{e.Message}");
        }
    }

    public static void UpdatePlayProgress(MusicItemModel musicItem, TimeSpan current) {
        try {
            Task.Run(() => {
                    MusicItemRepository.Instance.Update(
                        musicItem.FilePath,
                        new Dictionary<string, object?> { [nameof(MusicItemModel.Record)] = current.ToString() });
                })
                .ContinueWith(LoggerService.HandleException)
                .ConfigureAwait(false);
        } catch (Exception e) {
            LoggerService.Error($"保存歌曲《{musicItem.Title}》的播放进度到数据库时发生错误 : \n{e}");
            NotificationService.Error($"保存歌曲《{musicItem.Title}》的播放进度到数据库时发生错误 : \n{e.Message}");
        }
    }

    public async Task<IEnumerable<MusicItemModel>?> RemoveAsync(IEnumerable<MusicItemModel> musicItems) {
        var items = musicItems.ToArray();
        if (items.Length == 0)
            return null;
        // 构建确认提示信息
        string titles = string.Join("、", items.Select(item => $"《{item.Title}》"));

        var result = await MessageBox.ShowOverlayAsync(
                                         $"你真的要删除以下音乐吗？\n{titles}",
                                         "警告",
                                         icon: MessageBoxIcon.Warning,
                                         button: MessageBoxButton.YesNo)
                                     .ConfigureAwait(true);

        if (result != MessageBoxResult.Yes)
            return null;

        var successItems = new List<MusicItemModel>();

        await Task.Run(() => {
                      var repo = MusicItemRepository.Instance;

                      foreach (var musicItem in items) {
                          try {
                              repo.Delete(musicItem.FilePath);
                              successItems.Add(musicItem);

                              musicItem.RemoveCover();
                          } catch (Exception e) {
                              LoggerService.Error($"从数据库中删除歌曲{musicItem.Title}失败！\n{e.Message}\n{e.StackTrace}");

                              NotificationService.Error($"歌曲{musicItem.Title}删除失败！\n{e.Message}");
                          }
                      }
                  })
                  .ConfigureAwait(true);

        successItems.ForEach(item => MusicItems.Remove(item.FilePath));

        var failedItems = items.Except(successItems).ToArray();

        // 显示删除结果通知
        if (successItems.Count > 0) {
            string successTitles = string.Join("、", successItems.Select(item => $"《{item.Title}》"));
            NotificationService.Success($"{successTitles}已经从音乐库中移除了！");
        }

        if (failedItems.Length > 0) {
            string failedTitles = string.Join("、", failedItems.Select(item => $"《{item.Title}》"));
            NotificationService.Error($"删除{failedTitles}失败了！");
        }

        MusicItemsChanged?.Invoke(this, new MusicItemsChangedEventArgs() { OldItems = successItems, NewItems = null });
        return successItems;
    }

    [RelayCommand]
    public void ClearRecords(IEnumerable items) {
        IEnumerable<MusicItemModel> models = items switch {
            IEnumerable<MusicItemModel> musicItems => musicItems,
            IEnumerable<PlaylistItemModel> playlistItems => playlistItems.Select(item => item.Model),
            _ => throw new ArgumentException($"未知的音频模型类型集合{items.GetType()}", nameof(items))
        };
        foreach (MusicItemModel model in models) {
            model.ClearRecord();
        }
    }

    [RelayCommand]
    public static async Task ShowDetailedInfo(MusicItemModel musicItem) {
        var options = new OverlayDialogOptions { Title = "详细信息", CanLightDismiss = true, Mode = DialogMode.Info };

        await OverlayDialog.ShowCustomModal<AudioDetailedInfo, AudioDetailedInfoViewModel, DialogResult>(
                               new AudioDetailedInfoViewModel(musicItem, musicItem.Track),
                               options: options)
                           .ConfigureAwait(false);
    }

    [RelayCommand]
    public static void OpenInExplorer(MusicItemModel musicItem) {
        if (string.IsNullOrEmpty(musicItem.FilePath) || !File.Exists(musicItem.FilePath)) {
            NotificationService.Error($"无法打开《{musicItem.Title}》文件位置：{musicItem.FilePath}文件不存在");
            return;
        }

        try {
            FileOperationService.OpenInFileManager(musicItem.FilePath);
        } catch (Exception e) {
            LoggerService.Error($"打开文件位置失败: {e.Message}");
            NotificationService.Error($"打开《{musicItem.Title}》文件位置时报错：{e.Message}");
        }
    }

    [RelayCommand]
    public static async Task RemoveItemsAsync(IList<MusicItemModel> items) {
        await All.RemoveAsync(items).ConfigureAwait(false);
    }
}