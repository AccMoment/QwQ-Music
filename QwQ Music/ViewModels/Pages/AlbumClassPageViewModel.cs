using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;
using QwQ_Music.Views.Panels;

namespace QwQ_Music.ViewModels.Pages;

public partial class AlbumClassPageViewModel() : NavigationViewModel("专辑") {
    public AvaloniaList<Control> Panels { get; set; } = [new AllAlbumsPanel(), new AlbumDetailsPanel()];

    [RelayCommand]
    private void ToggleItem(AlbumModel model) { NavigationIndex = 1; }
}