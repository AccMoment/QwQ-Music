using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Panels;

public partial class MusicListPanelViewModel : MusicItemsViewModelBase {
    public MusicListModel? MusicListModel {
        get;
        set {
            if (field == value)
                return;
            _ = value?.LoadCurrentAsync()
                     .ContinueWith(LoggerService.HandleException)
                     .ContinueWith(_ => SetCurrentList(value.Name, value.Musics!))
                     .ConfigureAwait(false);
            field = value;
            var old = field;
            old?.DisposeCurrent();
        }
    }

    [RelayCommand]
    public static void Navigate(string name) { NavigateService.NavigateTo(name); }
}