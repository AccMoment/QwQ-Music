using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services.Databases;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.ViewModels.Panels;

public partial class AllMusicListPanelViewModel : ItemsViewModelBase<MusicListModel> {
    public AllMusicListPanelViewModel() { SetAllItems(MusicListsManager.MusicLists); }
    public static MusicListsManager MusicListsManager => MusicListsManager.Instance;

    protected override bool CustomFilter(ref readonly string value, ref readonly MusicListModel item) {
        //TODO TAGS
        return item.Name.Contains(value, StringComparison.OrdinalIgnoreCase) ||
               item.Description.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private static async Task TogglePlaylist(MusicListModel musicList) {
        if (musicList.IsLoaded) {
            await PlaylistManager.Instance.ReplaceAsync(musicList.Musics!, true).ConfigureAwait(false);
        }

        await PlaylistManager.Instance.ReplaceAsync(
                                 MusicListItemsRepository.Instance.GetAll(musicList.Name)
                                                         .Select(path => MusicItemsManager.All.MusicItems[path]),
                                 true)
                             .ConfigureAwait(false);
    }
}