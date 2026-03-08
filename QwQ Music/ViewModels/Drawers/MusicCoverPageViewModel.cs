using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Services.Shader;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.ViewModels.Bases;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.ViewModels.Drawers;

public partial class MusicCoverPageViewModel : NavigationViewModel {
    private const int _COLOR_COUNT = 4;

    private static readonly CoverConfig _coverConfig = ConfigManager.UiConfig.CoverConfig;

    private static readonly Color[] _defaultColors = [
        Color.Parse("#FFE2D9"), Color.Parse("#F3ECFE"), Color.Parse("#DFE7FF"), Color.Parse("#E4F2FF")
    ];

    private readonly IBrush _darkThemeBrush = Brush.Parse("#22FFFFFF");

    private readonly IBrush _lightThemeBrush = Brush.Parse("#88FFFFFF");

    public MusicCoverPageViewModel() : base("播放") {
        if (AudioPlayManager.CurrentMusicItem != PlaylistItemModel.RefDefault) {
            OnMusicItemChanged(
                this,
                new MusicItemChangedEventArgs(PlaylistItemModel.RefDefault, AudioPlayManager.CurrentMusicItem));
        }

        AudioPlayManager.MusicItemChanged += OnMusicItemChanged;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_OnProcessExit;
    }

    public static DrawerManager DrawerManager => DrawerManager.Instance;

    public static string OffsetName => I18NService.Lang[nameof(OffsetName)];

    public static AudioPlayManager AudioPlayManager => AudioPlayManager.Instance;

    public static RolledLyricConfig RolledLyric { get; } = ConfigManager.LyricConfig.RolledLyric;

    public static SpectrumConfig SpectrumConfig { get; } = ConfigManager.UiConfig.SpectrumConfig;

    public static string ShaderCode => ShaderConstants.WaveWarpShader;

    public double SelectLyricsTimePoint {
        get;
        set {
            if (SetProperty(ref field, value)) {
                AudioPlayManager.Position = field;
            }
        }
    }

    [ObservableProperty]
    public partial Color[] Colors { get; set; } = _defaultColors;

    [ObservableProperty]
    public partial IBrush SpectrumVisualizerBrush { get; set; } = Brushes.White;

    private void CurrentDomain_OnProcessExit(object? sender, EventArgs e) {
        AudioPlayManager.MusicItemChanged -= OnMusicItemChanged;
        AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_OnProcessExit;
    }

    private void OnMusicItemChanged(object? sender, MusicItemChangedEventArgs args) {
        UpdateColorsListAsync(args.NewItem.Model).ContinueWith(LoggerService.HandleException).ConfigureAwait(false);
        OnPropertyChanged(nameof(Colors));
        Dispatcher.UIThread.Post(UpdateThemeVariantFromColors);
    }

    private async Task UpdateColorsListAsync(MusicItemModel musicItem) {
        // 如果没有封面Id，直接使用默认颜色
        if (string.IsNullOrWhiteSpace(musicItem.AlbumId)) {
            Colors = _defaultColors;

            return;
        }

        // 尝试从已缓存的颜色中获取
        if (musicItem.CoverColors is { Length: >= _COLOR_COUNT }) {
            Colors = [.. musicItem.CoverColors.Select(Color.Parse)];

            return;
        }

        // 提取新的颜色
        var colorsList = await GetColorPaletteAsync(musicItem.Thumbnail, _COLOR_COUNT).ConfigureAwait(false);

        // 使用提取的颜色，为null则使用默认颜色
        Colors = colorsList ?? _defaultColors;

        // 缓存提取的颜色
        if (colorsList != null) {
            musicItem.CoverColors = colorsList.Select(x => x.ToString()).ToArray();


            await MusicItemsManager.UpdateAsync(
                                       musicItem,
                                       new Dictionary<string, object?> {
                                           [nameof(MusicItemModel.CoverColors)] = string.Join(
                                               "、",
                                               musicItem.CoverColors)
                                       })
                                   .ConfigureAwait(false);
        }
    }

    private void UpdateThemeVariantFromColors() {
        Debug.Assert(Dispatcher.UIThread.CheckAccess());
        if (Colors.Length == 0) {
            DrawerManager.Instance.MusicPlayerPanelThemeVariant = "Default";

            return;
        }

        // 计算平均亮度
        double totalLuminance = Colors.Sum(c => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0);
        double avgLuminance = totalLuminance / Colors.Length;

        // 根据平均亮度设置主题（反色）
        bool isHighLuminance = avgLuminance > 0.5;

        SpectrumVisualizerBrush = isHighLuminance ? _lightThemeBrush : _darkThemeBrush;
        DrawerManager.Instance.MusicPlayerPanelThemeVariant = isHighLuminance ? "Light" : "Dark";
    }

    private static async Task<Color[]?> GetColorPaletteAsync(Bitmap cover, int colorCount = 5) {
        return !CacheManager.IsValid(cover) ?
            null : // 缓存不存在直接返回null
            await ColorExtraction.GetColorPaletteFromBitmapAsync(
                                     cover,
                                     colorCount,
                                     _coverConfig.SelectedColorExtractionAlgorithm,
                                     _coverConfig.IgnoreWhite,
                                     _coverConfig.ToLab,
                                     _coverConfig.UseKMeansPp)
                                 .ConfigureAwait(false);
    }

    [RelayCommand]
    private static void OnVolumeBarPointerWheelChanged(PointerWheelEventArgs e) {
        // 阻止事件冒泡到父级元素
        e.Handled = true;

        switch (e.Delta.Y) {
            // 根据你的需求处理滚轮滚动事件
            case > 0:
                AudioPlayManager.Volume += 2;

                break;
            case < 0:
                AudioPlayManager.Volume -= 2;

                break;
        }
    }
}