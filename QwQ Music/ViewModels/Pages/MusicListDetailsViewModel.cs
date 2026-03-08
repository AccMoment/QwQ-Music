using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;
using QwQ_Music.ViewModels.Panels;
using QwQ_Music.Views.Pages;
using QwQ_Music.Views.Panels;

namespace QwQ_Music.ViewModels.Pages;

public partial class MusicListDetailsViewModel : NavigationViewModel {
    private readonly MusicListPanelViewModel _musicListPageViewModel = new();
    private readonly AllMusicListsPanelViewModel _allMusicListPanelViewModel = new();
    public AvaloniaList<UserControl> Panels { get; set; }

    public MusicListDetailsViewModel() : base("歌单") {
        Panels = [
            new AllMusicListsPanel { DataContext = _allMusicListPanelViewModel, },
            new MusicListPage { DataContext = _musicListPageViewModel }
        ];
    }

    [RelayCommand]
    public void SetCurrentItem(MusicListModel? item) {
        _musicListPageViewModel.MusicListModel = item;
        // TODO: 点击卡片会产生NullRefenceException；添加音乐后AllMusicItemsPanel对应关系不准确
    }
}