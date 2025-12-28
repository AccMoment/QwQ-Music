using Avalonia.Controls;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Panels;

namespace QwQ_Music.Views.Panels;

public partial class MusicListPanel : UserControl {
    private readonly MusicListPanelViewModel _dataContext = new();

    public MusicListPanel() {
        InitializeComponent();
        DataContext = _dataContext;
        /* TODO:
            把AllMusicsPanel移除，使用 QWQ_MUSIC_LIST_ALL_MUSIC_LIST作为 Name进行标注，
            以 MusicListDetailsPanel合并 AllMusicsPanel和 AlbumDetailsPanel。
            应该恢复AlbumClassPage，抽取三者（AllMusics、MusicListDetail、AlbumDetail）的相同部分。
        */
    }

    public void SetCurrentList(MusicListModel musicList) { _dataContext.MusicListModel = musicList; }
}