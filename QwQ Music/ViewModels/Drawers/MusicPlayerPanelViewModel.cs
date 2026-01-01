using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Helpers;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Services.Shader;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.ViewModels.Bases;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.ViewModels.Drawers;

public partial class MusicCoverPageViewModel : NavigationViewModel {
    private const int COLOR_COUNT = 4;

    private static readonly CoverConfig _coverConfig = ConfigManager.UiConfig.CoverConfig;

    private static readonly List<Color> _defaultColors = [
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

    public static string OffsetName => LanguageModel.Lang[nameof(OffsetName)];

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
    public partial List<Color> ColorsList { get; set; } = _defaultColors;

    [ObservableProperty]
    public partial IBrush SpectrumVisualizerBrush { get; set; } = Brushes.White;

    private void CurrentDomain_OnProcessExit(object? sender, EventArgs e) {
        AudioPlayManager.MusicItemChanged -= OnMusicItemChanged;
        AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_OnProcessExit;
    }

    private void OnMusicItemChanged(object? sender, MusicItemChangedEventArgs args) {
        try {
            args.OldItem.Model.DisposeCurrent();

            args.NewItem.Model.LoadCurrentAsync()
                .ConfigureAwait(false)
                .GetAwaiter()
                .OnCompleted(() => {
                    AudioPlayManager.LyricsModel = new LyricsModel {
                        Offset = args.NewItem.Model.LyricOffset, Lyrics = args.NewItem.Model.Lyrics
                    };
                    AudioPlayManager.CoverImage = args.NewItem.Model.CoverImage;
                    UpdateColorsListAsync(args.NewItem.Model)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .OnCompleted(() => Dispatcher.UIThread.Post(UpdateThemeVariantFromColors));
                });
        } catch (Exception ex) {
            LoggerService.Error($"{nameof(OnMusicItemChanged)} 发生错误 : {ex.Message}");
        }
    }

    private async Task UpdateColorsListAsync(MusicItemModel musicItem) {
        // 如果没有封面Id，直接使用默认颜色
        if (string.IsNullOrWhiteSpace(musicItem.CoverId)) {
            ColorsList = _defaultColors;

            return;
        }

        // 尝试从已缓存的颜色中获取
        if (musicItem.CoverColors is { Length: >= COLOR_COUNT }) {
            ColorsList = [.. musicItem.CoverColors.Select(Color.Parse)];

            return;
        }

        // 提取新的颜色
        var colorsList = await GetColorPalette(musicItem.CoverId, COLOR_COUNT);

        // 使用提取的颜色，为null则使用默认颜色
        ColorsList = colorsList ?? _defaultColors;

        // 缓存提取的颜色
        if (colorsList != null) {
            musicItem.CoverColors = colorsList.Select(x => x.ToString()).ToArray();

            await Task.Run(() => {
                MusicItemsManager.Update(
                    musicItem,
                    new Dictionary<string, object?> {
                        [nameof(MusicItemModel.CoverColors)] = string.Join("、", musicItem.CoverColors)
                    });
            });
        }
    }

    private void UpdateThemeVariantFromColors() {
        Debug.Assert(Dispatcher.UIThread.CheckAccess());
        if (ColorsList.Count == 0) {
            DrawerManager.Instance.MusicPlayerPanelThemeVariant = "Default";

            return;
        }

        // 计算平均亮度
        double totalLuminance = ColorsList.Sum(c => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0);
        double avgLuminance = totalLuminance / ColorsList.Count;

        // 根据平均亮度设置主题（反色）
        bool isHighLuminance = avgLuminance > 0.5;

        SpectrumVisualizerBrush = isHighLuminance ? _lightThemeBrush : _darkThemeBrush;
        DrawerManager.Instance.MusicPlayerPanelThemeVariant = isHighLuminance ? "Light" : "Dark";
    }

    private static async Task<List<Color>?> GetColorPalette(string imagePath, int colorCount = 5) {
        // 尝试使用缓存的位图
        var bitmap = await ImageHelper.LoadFromFileAsync(StaticConfig.GetMusicCoverFullPath(imagePath)).ConfigureAwait(false);

        return bitmap == null ?
            null // 缓存不存在直接返回null
            :
            ColorExtraction.GetColorPaletteFromBitmap(
                bitmap,
                colorCount,
                _coverConfig.SelectedColorExtractionAlgorithm,
                _coverConfig.IgnoreWhite,
                _coverConfig.ToLab,
                _coverConfig.UseKMeansPp);
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