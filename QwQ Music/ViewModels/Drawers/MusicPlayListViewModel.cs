using System.Collections;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Drawers;

public partial class MusicPlayListViewModel() : DataGridViewModelBase(MusicPlayList.PlayList)
{
    public static DrawerStatusViewModel DrawerStatusViewModel => DrawerStatusViewModel.Default;

    [RelayCommand]
    private static void RemoveInPlaylist(IList items)
    {
        var musicItems = items.Cast<MusicItemModel>().ToList();

        MusicPlayList.Remove(musicItems);
    }

    [RelayCommand]
    private static void ClearMusicPlayList()
    {
        MusicPlayList.Clear();
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
