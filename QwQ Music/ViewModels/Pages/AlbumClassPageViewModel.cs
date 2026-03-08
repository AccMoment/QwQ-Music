using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;
using QwQ_Music.ViewModels.Panels;
using QwQ_Music.Views.Panels;

namespace QwQ_Music.ViewModels.Pages;

public partial class AlbumClassPageViewModel : NavigationViewModel {
    private readonly AllAlbumsPanelViewModel _allAlbumsPanelViewModel = new();
    private readonly MusicListPanelViewModel _musicListPanelViewModel = new();
    public AvaloniaList<Control> Panels { get; set; }

    public AlbumClassPageViewModel() : base("专辑") {
        Panels = [
            new AllAlbumsPanel { DataContext = _allAlbumsPanelViewModel },
            new AlbumDetailsPanel { DataContext = _musicListPanelViewModel }
        ];
    }

    [RelayCommand]
    private void BackToAllAlbumsPage() { NavigationIndex = 1; }

    [RelayCommand]
    private void ToggleItem(MusicListModel model) { _musicListPanelViewModel.MusicListModel = model; }
}