using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services.Databases;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.ViewModels.Panels;

public partial class AllMusicListPanelViewModel : ViewModelBase
{
    private readonly AvaloniaList<MusicListModel> _filterSource = [];

    public static MusicPlayerViewModel MusicPlayerViewModel => MusicPlayerViewModel.Current;

    public static MusicListsManager MusicListsManager => MusicListsManager.Instance;

    [ObservableProperty] public partial AvaloniaList<MusicListModel> MusicLists { get; set; } = MusicListsManager.SongLists;

    public string? SearchText
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            OnSearchTextChanged(value);
        }
    }

    private void OnSearchTextChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            MusicLists = MusicListsManager.SongLists;

            return;
        }

        var source = string.IsNullOrEmpty(value)
            ? MusicListsManager.SongLists
            : MusicListsManager.SongLists.Where(MatchesSearchCriteria);

        _filterSource.Clear();
        _filterSource.AddRange(source);
        MusicLists = _filterSource;

        return;

        bool MatchesSearchCriteria(MusicListModel item)
        {
            return item.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
             || item.Description.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private static async Task TogglePlaylist(MusicListModel? musicList)
    {
        if (string.IsNullOrEmpty(musicList?.Name))
            return;
        

        PlaylistManager.Instance.Replace(MusicListItemsRepository.Instance.GetAll(musicList.Name).Select(path=>MusicItemsManager.All.MusicItems[path]));

        await MusicPlayerViewModel.PlayThisMusicAsync(PlaylistManager.Instance.First()).ConfigureAwait(false);
    }
}
