using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public static Common.Managers.DrawerManager DrawerManager => Common.Managers.DrawerManager.Instance;
}
