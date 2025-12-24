using QwQ_Music.Common.Managers;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Windows;

public class DesktopLyricsWindowViewModel : ViewModelBase
{
    public static MusicPlayerViewModel MusicPlayerViewModel => MusicPlayerViewModel.Current;

    public static DesktopLyricConfig LyricConfig => ConfigManager.LyricConfig.DesktopLyric;
}
