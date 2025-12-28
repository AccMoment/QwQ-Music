using CommunityToolkit.Mvvm.Input;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Windows;

public partial class DesktopPlayControlWindowViewModel : ViewModelBase
{
    public static Common.Managers.AudioPlayManager AudioPlayManager { get; } = Common.Managers.AudioPlayManager.Instance;

    public Common.Managers.DrawerManager DrawerManager { get; } = Common.Managers.DrawerManager.Instance;
    
    [RelayCommand]
    private void ShowMusicPlayerPage()
    {
        ApplicationViewModel.ShowMainWindow(true);
        DrawerManager.IsMusicPlayerPanelVisible = true;
    }
}
