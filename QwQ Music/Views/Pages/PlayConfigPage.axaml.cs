using Avalonia.Controls;
using QwQ_Music.ViewModels.Pages;

namespace QwQ_Music.Views.Pages;

public partial class PlayConfigPage : UserControl
{
    public PlayConfigPage()
    {
        InitializeComponent();
        DataContext = new PlayConfigPageViewModel();
    }
}
