using CommunityToolkit.Mvvm.Input;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Windows;

public partial class DesktopPlayControlWindowViewModel : ViewModelBase
{
    public static MusicPlayerViewModel MusicPlayerViewModel { get; } = MusicPlayerViewModel.Default;

    public DrawerStatusViewModel DrawerStatusViewModel { get; } = DrawerStatusViewModel.Default;
    
    [RelayCommand]
    private void ShowMusicPlayerPage()
    {
        ApplicationViewModel.ShowMainWindow(true);
        DrawerStatusViewModel.IsMusicPlayerPanelVisible = true;
    }
}
