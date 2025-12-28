using Avalonia.Controls;
using QwQ_Music.ViewModels.Pages;

namespace QwQ_Music.Views.Pages;

public partial class AllMusicPage : UserControl
{
    public AllMusicPage()
    {
        InitializeComponent();
        DataContext = new AllMusicPageViewModel();
    }
}
