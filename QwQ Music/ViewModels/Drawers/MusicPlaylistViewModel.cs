using System.Collections;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Drawers;

public partial class MusicPlaylistViewModel : DataGridViewModelBase {
    public static PlaylistManager PlaylistManager => PlaylistManager.Instance;
    public static DrawerStatusViewModel DrawerStatusViewModel => DrawerStatusViewModel.Default;

    public static MusicItemsManager MusicItemsManager => MusicItemsManager.All;
    
    [RelayCommand]
    private static void RemoveInPlaylist(IList items)
    {
        var musicItems = items.Cast<PlaylistItemModel>().ToList();
        PlaylistManager.Instance.RemoveRange(musicItems);
    }

    [RelayCommand]
    private static void ClearMusicPlayList()
    {
        PlaylistManager.Instance.Clear();
    }
    
    [RelayCommand]
    private void JumpToTop(ListBox listBox)
    {
        // 滚动到第一行（第一行数据）
        if (MusicItems.Count > 0)
        {
            listBox.ScrollIntoView(MusicItems.First());
        }
    }
}
