using System;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Drawers;

public partial class MusicPlayerTrayViewModel : ViewModelBase
{
    public MusicPlayerTrayViewModel()
    {
        AudioPlayManager.PlaybackStateChanged += AudioPlayManagerOnPlaybackStateChanged;

        NavigateService.CurrentViewChanged += CurrentViewChanged;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_OnProcessExit;
    }

    public static DrawerManager DrawerManager => DrawerManager.Instance;

    public RolledLyricConfig RolledLyric { get; } = ConfigManager.LyricConfig.RolledLyric;

    public double AlbumCoverCurrentAngle { get; set; }

    public double AlbumCoverRecordAngle { get; set; }

    [ObservableProperty] public partial bool IsSoundEffectView { get; set; }

    public static AudioPlayManager AudioPlayManager => AudioPlayManager.Instance;

    private void CurrentViewChanged(string name)
    {
        IsSoundEffectView = name == "音效" || NavigateService.GetParentView(name) == "音效";
    }

    private void CurrentDomain_OnProcessExit(object? sender, EventArgs e)
    {
        AudioPlayManager.PlaybackStateChanged -= AudioPlayManagerOnPlaybackStateChanged;
        NavigateService.CurrentViewChanged -= CurrentViewChanged;
        AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_OnProcessExit;
    }

    [RelayCommand]
    private static void NavigationToSoundEffectView()
    {
        NavigateService.NavigateTo("音效");
    }

    [RelayCommand]
    private static void ResetPlaybackSpeed()
    {
        AudioPlayManager.Speed = 1.0f;
    }

    [RelayCommand]
    private static void OnVolumeBarPointerWheelChanged(PointerWheelEventArgs e)
    {
        // 阻止事件冒泡到父级元素
        e.Handled = true;

        switch (e.Delta.Y)
        {
            // 根据你的需求处理滚轮滚动事件
            case > 0:
                AudioPlayManager.Volume += 2;

                break;
            case < 0:
                AudioPlayManager.Volume -= 2;

                break;
        }
    }

    [RelayCommand]
    private static void OnSpeedBarPointerWheelChanged(PointerWheelEventArgs e)
    {
        e.Handled = true;

        switch (e.Delta.Y)
        {
            case > 0:
                AudioPlayManager.Speed += 0.01f;

                break;
            case < 0:
                AudioPlayManager.Speed -= 0.01f;

                break;
        }
    }

    [RelayCommand]
    private static void PlaySpeedUp()
    {
        AudioPlayManager.Speed += 0.1f;
    }

    [RelayCommand]
    private static void PlaySpeedDown()
    {
        AudioPlayManager.Speed -= 0.1f;
    }

    private void AudioPlayManagerOnPlaybackStateChanged(object? sender, bool e)
    {
        if (e)
            return;

        RecordCurrentAngle();
    }

    private void RecordCurrentAngle()
    {
        AlbumCoverRecordAngle = AlbumCoverCurrentAngle;
        OnPropertyChanged(nameof(AlbumCoverRecordAngle));
    }
}
