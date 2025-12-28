using Avalonia.Collections;
using Avalonia.Controls;
using QwQ_Music.ViewModels.Bases;
using QwQ_Music.Views.Panels;

namespace QwQ_Music.ViewModels.Pages;

public partial class MusicListClassPageViewModel(string name) : NavigationViewModel(name) {
    public AvaloniaList<UserControl> Panels { get; set; } = [new AllMusicListPanel(), new MusicListPanel()];

}