using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.ViewModels.Bases;
using QwQ_Music.ViewModels.Windows;
using QwQ_Music.Windows;
using static QwQ_Music.Common.Services.I18NService;

namespace QwQ_Music.ViewModels.Pages;

public partial class LyricConfigPageViewModel : ViewModelBase {
    private DesktopLyricsWindow? _desktopLyricsWindow;

    public LyricConfigPageViewModel() {
        ToggleWindowDisplayStatus(LyricIsEnabled);
        ToggleDesktopPlayControlService(DesktopPlayControlIsEnabled);
        OnPropertyChanged(nameof(LyricWidth));

        AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
    }

    public bool LyricIsEnabled {
        get => LyricConfig.DesktopLyric.LyricIsEnabled;
        set {
            if (LyricIsEnabled == value)
                return;

            LyricConfig.DesktopLyric.LyricIsEnabled = value;
            OnPropertyChanged();

            ToggleWindowDisplayStatus(value);
        }
    }

    public bool LyricIsDualLang {
        get => LyricConfig.DesktopLyric.LyricIsDualLang;
        set {
            if (LyricIsDualLang == value)
                return;

            LyricConfig.DesktopLyric.LyricIsDualLang = value;
            OnPropertyChanged();
        }
    }

    public bool DesktopPlayControlIsEnabled {
        get => LyricConfig.DesktopLyric.DesktopPlayControlIsEnabled;
        set {
            if (DesktopPlayControlIsEnabled == value)
                return;

            LyricConfig.DesktopLyric.DesktopPlayControlIsEnabled = value;
            OnPropertyChanged();

            ToggleDesktopPlayControlService(value);
        }
    }

    public bool LockLyricWindow {
        get => LyricConfig.DesktopLyric.LockLyricWindow;
        set {
            if (LockLyricWindow == value)
                return;

            LyricConfig.DesktopLyric.LockLyricWindow = value;
            _desktopLyricsWindow?.SetPenetrate(value);
        }
    }

    public double LyricWidth {
        get => LyricConfig.DesktopLyric.LyricWidth;
        set => LyricConfig.DesktopLyric.LyricWidth = value;
    }

    public bool LyricIsDoubleLine {
        get => LyricConfig.DesktopLyric.LyricIsDoubleLine;
        set {
            if (LyricIsDoubleLine == value)
                return;

            LyricConfig.DesktopLyric.LyricIsDoubleLine = value;
        }
    }

    public static LyricConfig LyricConfig { get; } = ConfigManager.LyricConfig;

    private void CurrentDomainOnProcessExit(object? sender, EventArgs e) {
        AppDomain.CurrentDomain.ProcessExit -= CurrentDomainOnProcessExit;
        Dispatcher.UIThread.Post(() => {
            CloseLyricWindow();
            DesktopPlayControlService.Stop();
        });
    }

    private static void ToggleDesktopPlayControlService(bool value) {
        if (value)
            // 启动桌面播放控制服务
            DesktopPlayControlService.Start();
        else
            DesktopPlayControlService.Stop();
    }

    private void ToggleWindowDisplayStatus(bool value) {
        if (value)
            ShowLyricWindow();
        else
            CloseLyricWindow();
    }

    private void ShowLyricWindow() {
        _desktopLyricsWindow = new DesktopLyricsWindow {
            DataContext = new DesktopLyricsWindowViewModel(), Width = LyricConfig.DesktopLyric.LyricWidth
        };
        _desktopLyricsWindow.Show();
        _desktopLyricsWindow.SetPenetrate(LyricConfig.DesktopLyric.LockLyricWindow);
    }

    private void CloseLyricWindow() {
        _desktopLyricsWindow?.Close();
        _desktopLyricsWindow = null;
    }

    [RelayCommand]
    private void SetWindowPosition(string position) {
        if (_desktopLyricsWindow == null) {
            NotificationService.Error("请先启动歌词窗口~");

            return;
        }

        if (_desktopLyricsWindow.Screens.Primary == null) {
            NotificationService.Error("无法获取屏幕宽高~");

            return;
        }

        int screenWidth = _desktopLyricsWindow.Screens.Primary.WorkingArea.Width;
        int screenHeight = _desktopLyricsWindow.Screens.Primary.WorkingArea.Height;
        double scaling = _desktopLyricsWindow.Screens.Primary.Scaling;
        double windowWidth = _desktopLyricsWindow.Width * scaling;
        double windowHeight = _desktopLyricsWindow.Height * scaling;

        var positions = new Dictionary<string, Func<PixelPoint>> {
            ["↖"] = () => new PixelPoint(0, 0),

            // ReSharper disable once PossibleLossOfFraction
            ["↑"] = () => new PixelPoint((int)(screenWidth / 2 - windowWidth / 2), 0),
            ["↗"] = () => new PixelPoint((int)(screenWidth - windowWidth), 0),
            ["↙"] = () => new PixelPoint(0, (int)(screenHeight - windowHeight)),

            // ReSharper disable once PossibleLossOfFraction
            ["↓"] = () => new PixelPoint((int)(screenWidth / 2 - windowWidth / 2), (int)(screenHeight - windowHeight)),
            ["↘"] = () => new PixelPoint((int)(screenWidth - windowWidth), (int)(screenHeight - windowHeight))
        };

        if (positions.TryGetValue(position, out Func<PixelPoint>? getPosition))
            _desktopLyricsWindow.Position = getPosition();

        // 如果不是已知位置，则保持原位置
    }

    #region 多语言

    public static string IsEnabledV => Lang[nameof(IsEnabledV)];

    public static string IsDoubleLineV => Lang[nameof(IsDoubleLineV)];

    public static string IsDualLangV => Lang[nameof(IsDualLangV)];

    // ReSharper disable once InconsistentNaming
    public static string PositionXV => Lang[nameof(PositionXV)];

    // ReSharper disable once InconsistentNaming
    public static string PositionYV => Lang[nameof(PositionYV)];

    public static string WidthV => Lang[nameof(WidthV)];

    public static string HeightV => Lang[nameof(HeightV)];

    public static string LyricMainTopColorV => Lang[nameof(LyricMainTopColorV)];

    public static string LyricMainBottomColorV => Lang[nameof(LyricMainBottomColorV)];

    public static string LyricMainBorderColorV => Lang[nameof(LyricMainBorderColorV)];

    public static string LyricAltTopColorV => Lang[nameof(LyricAltTopColorV)];

    public static string LyricAltBottomColorV => Lang[nameof(LyricAltBottomColorV)];

    public static string LyricAltBorderColorV => Lang[nameof(LyricAltBorderColorV)];

    #endregion
}