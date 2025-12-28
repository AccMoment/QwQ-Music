using Avalonia.Controls;
using QwQ_Music.ViewModels.Pages;

namespace QwQ_Music.Views.Pages;

public partial class MusicListClassPage : Panel {
    public required string PanelName {
        init => DataContext = new MusicListClassPageViewModel(value);
    }

    public MusicListClassPage() { InitializeComponent(); }
}