using Avalonia.Controls;
using QwQ_Music.ViewModels.Panels;

namespace QwQ_Music.Views.Panels;

public partial class AlbumDetailsPanel : UserControl {
    public AlbumDetailsPanel() {
        InitializeComponent();
        DataContext = new MusicListPanelViewModel();
    }
}