using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Drawers;

public partial class MusicPlaylistViewModel : ViewModelBase {
    public AvaloniaList<PlaylistItemModel> MusicList => PlaylistManager.Instance.ActualPlaylist;

    public List<PlaylistItemModel> SelectedItems { get; set; } = [];

    [RelayCommand]
    private void Remove(IList items) {
        var musicItems = items.Cast<PlaylistItemModel>().ToList();
        PlaylistManager.Instance.RemoveRange(musicItems);
    }

    [RelayCommand]
    private void ClearMusic() { PlaylistManager.Instance.Clear(); }

    [RelayCommand]
    private void JumpToTop(ListBox listBox) {
        // 滚动到第一行（第一行数据）
        if (MusicList.Count > 0) {
            listBox.ScrollIntoView(MusicList.First());
        }
    }

    [RelayCommand]
    private void ScrollToCurrent() { 
        
    }
}