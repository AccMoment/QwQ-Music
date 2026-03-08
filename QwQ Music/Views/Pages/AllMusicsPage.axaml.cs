using Avalonia.Controls;
using QwQ_Music.ViewModels.Pages;

namespace QwQ_Music.Views.Pages;

public partial class AllMusicsPage : UserControl {
    public AllMusicsPage() {
        InitializeComponent();
        DataContext = new AllMusicPageViewModel();
    }
}