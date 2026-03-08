using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Panels;

public partial class AlbumPanelViewModel : MusicItemsViewModelBase {
    public AlbumModel? AlbumModel {
        get;
        set {
            if (field == value)
                return;
            _ = value?.LoadCurrentAsync()
                     .ContinueWith(LoggerService.HandleException)
                     .ContinueWith(_ => SetCurrentList(value.Name, value.Musics!))
                     .ConfigureAwait(false);
            field?.DisposeCurrent();
            field = value;
        }
    }

    [RelayCommand]
    public static void Navigate(string name) { NavigateService.NavigateTo(name); }
}