using Avalonia.Controls;
using QwQ_Music.ViewModels.Drawers;

namespace QwQ_Music.Views.Drawers;

public partial class MusicPlayList : Grid
{
    public MusicPlayList()
    {
        InitializeComponent();
        DataContext = new MusicPlaylistViewModel();
    }
}
