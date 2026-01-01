using Avalonia.Controls;
using QwQ_Music.ViewModels.Drawers;

namespace QwQ_Music.Views.Drawers;

public partial class MusicPlayList : UserControl {
    public MusicPlayList() {
        InitializeComponent();
        DataContext = new MusicPlaylistViewModel();
    }
}