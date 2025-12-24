using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Audio.SoundModifier;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Utilities;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.ViewModels.Bases;
using QwQ_Music.Views.Dialogs;
using Ursa.Controls;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.ViewModels.Pages;

public partial class PlayConfigPageViewModel : ViewModelBase {
    public PlayConfigPageViewModel() { NavigateService.ComeToOneselfEvents["播放"] = ComeToOneselfEvent; }

    public PlayerConfig PlayerConfig { get; } = ConfigManager.PlayerConfig;

    public static MusicItemsManager MusicItemsManager => MusicItemsManager.All;

    public PlayComponent PlayComponent { get; } = ConfigManager.SoundModifierConfig.PlayComponent;

    public Dictionary<FadeModifier.FadeCurve, string> FadeCurves { get; } = new() {
        [FadeModifier.FadeCurve.Cosine] = "余弦渐变",
        [FadeModifier.FadeCurve.Exponential] = "指数渐变",
        [FadeModifier.FadeCurve.Linear] = "线性渐变"
    };

    [RelayCommand]
    public async Task OpenCurrentStreamInfo() {
        // ReSharper disable once UseConfigureAwaitFalse
        await OverlayDialog.ShowCustomModal<CurrentStreamInfo, ViewModelBase?, DialogResult>(
            null,
            options: new OverlayDialogOptions { Title = "详细信息", CanLightDismiss = true, Mode = DialogMode.Info });
    }

    private void ComeToOneselfEvent() {
        NumberOfCompletedCalc = MusicItemsManager.MusicItems.Values.Count(item => item.Gain != 0);
    }

    #region 回放增益

    [ObservableProperty]
    public partial int NumberOfCompletedCalc { get; set; }

    public static Dictionary<MusicReplayGainStandard, string> MusicReplayGainStandards { get; set; } = new() {
        [MusicReplayGainStandard.Streaming] = "流媒体优化（-16 LUFS）",
        [MusicReplayGainStandard.EbuR128] = "EBU R128广播标准（-23 LUFS）",
        [MusicReplayGainStandard.ReplayGain2] = "ReplayGain 2.0标准（-18 LUFS）",
        [MusicReplayGainStandard.Custom] = "自定义目标响度"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMusicReplayGainStandardDescription))]
    public partial MusicReplayGainStandard SelectedMusicReplayGainStandard { get; set; } =
        MusicReplayGainStandard.Streaming;

    public string SelectedMusicReplayGainStandardDescription =>
        MusicReplayGainStandards[SelectedMusicReplayGainStandard];

    [ObservableProperty]
    public partial CancellationTokenSource? CancellationTokenSource { get; set; }

    [RelayCommand]
    public void ForceRefreshMusicItemsTags() {
        Task.Run(() => MusicItemsManager.MusicItems.Values.AsParallel()
                                        .ForAll(item => item.UpdateMetaDataAsync(true)
                                                            .ContinueWith(LoggerService.HandleException)
                                                            .ConfigureAwait(false)))
            .ContinueWith(LoggerService.HandleException)
            .ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ClearCallbackGain() {
        if (await MessageBox.ShowOverlayAsync(
                                "你真的要清空已经计算的回放增益值吗？",
                                "警告",
                                icon: MessageBoxIcon.Warning,
                                button: MessageBoxButton.YesNo)
                            .ConfigureAwait(true) !=
            MessageBoxResult.Yes)
            return;

        var musicItems = MusicItemsManager.MusicItems.Values.Where(item => item.Gain > 0);

        await Task.Run(() => {
                      foreach (MusicItemModel musicItem in musicItems) {
                          musicItem.Gain = 0;
                          MusicItemsManager.Update(
                              musicItem,
                              new Dictionary<string, object?> { [nameof(MusicItemModel.Gain)] = musicItem.Gain });
                      }
                  })
                  .ConfigureAwait(false);

        NumberOfCompletedCalc = 0;

        NotificationService.Info("回放增益值已清空！");
    }

    [RelayCommand]
    private async Task ToggleCalculation() {
        if (MusicItemsManager.Count <= 0 || NumberOfCompletedCalc == MusicItemsManager.Count) {
            NotificationService.Info("已经没有需要计算回放增益的音乐啦~");

            return;
        }

        await StartNewCalculationAsync().ConfigureAwait(false);
    }

    private async Task StartNewCalculationAsync() {
        CancellationTokenSource = new CancellationTokenSource();

        try {
            List<MusicItemModel> itemsToProcess =
                MusicItemsManager.MusicItems.Values.Where(item => item.Gain <= 0).ToList();
            ProcessItems(itemsToProcess, CancellationTokenSource.Token);
            NotificationService.Info("回放增益计算结束！");
        } catch (OperationCanceledException) {
            NotificationService.Info("回放增益计算已取消！");
        } catch (Exception e) {
            NotificationService.Error($"计算任务出错退出！\n{e.Message}");
            await LoggerService.ErrorAsync($"计算任务出错退出！\n{e.Message}\n{e.StackTrace}").ConfigureAwait(false);
        } finally {
            CleanupTask();
        }
    }

    private void ProcessItems(List<MusicItemModel> items, CancellationToken cancellationToken) {
        items.AsParallel()
             .WithCancellation(cancellationToken)
             .ForAll(item => {
                 try {
                     using var audioGainCalculator = new AudioGainCalculator();
                     ProcessSingleItemAsync(audioGainCalculator, item)
                         // ReSharper disable once MethodSupportsCancellation
                         .ContinueWith(LoggerService.HandleException)
                         .ConfigureAwait(false);
                 } catch (Exception ex) {
                     NotificationService.Error($"计算{item.Title}的回放增益时出现错误：\n{ex.Message}");
                     LoggerService.ErrorAsync($"计算{item.Title}的回放增益时出现错误：\n{ex.Message}\n{ex.StackTrace}")
                                  // ReSharper disable once MethodSupportsCancellation
                                  .ContinueWith(LoggerService.HandleException)
                                  .ConfigureAwait(false);
                 }
             });
    }

    private async Task ProcessSingleItemAsync(AudioGainCalculator audioGainCalculator, MusicItemModel item) {
        double gain = await audioGainCalculator.CalculateGainAsync(
                                                   item,
                                                   SelectedMusicReplayGainStandard,
                                                   PlayerConfig.CustomMusicReplayGainStandard)
                                               .ConfigureAwait(false);

        MusicItemsManager.Update(item, new Dictionary<string, object?> { [nameof(MusicItemModel.Gain)] = gain });

        item.Gain = gain;
        NumberOfCompletedCalc++;
    }

    [RelayCommand]
    public void CancelCalculation() { CancellationTokenSource?.Cancel(); }

    private void CleanupTask() {
        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
    }

    #endregion
}