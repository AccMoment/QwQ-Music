using Avalonia.Controls;
using QwQ_Music.ViewModels.Pages;

namespace QwQ_Music.Views.Pages;

public partial class MusicListDetailsPage : Panel {
    public MusicListDetailsPage() {
        InitializeComponent();
        DataContext = new MusicListDetailsViewModel();
    }
}