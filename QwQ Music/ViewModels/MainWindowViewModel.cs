using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public static DrawerStatusViewModel DrawerStatusViewModel => DrawerStatusViewModel.Default;
}
