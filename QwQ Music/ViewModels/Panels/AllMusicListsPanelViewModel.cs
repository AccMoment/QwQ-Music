using System;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Services.Databases;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.ViewModels.Panels;

public partial class AllMusicListsPanelViewModel : ItemsViewModelBase<MusicListModel> {
    public AllMusicListsPanelViewModel() {
        Update(null, EventArgs.Empty);
        MusicListsManager.CollectionChanged += Update;
    }

    private void Update(object? sender, EventArgs args) {
        SetCurrentList(nameof(MusicListsManager), MusicListsManager.MusicLists);
    }

    public static MusicListsManager MusicListsManager => MusicListsManager.Instance;

    protected override bool CustomFilter(ref readonly string value, ref readonly MusicListModel item) {
        //TODO TAGS
        return item.Name.Contains(value, StringComparison.OrdinalIgnoreCase) ||
               item.Description.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private static void TogglePlaylist(MusicListModel musicList) {
        if (musicList.Name == PlaylistManager.Instance.CurrentListName)
            if (musicList.IsLoaded) {
                PlaylistManager.Instance.ReplaceAsync(musicList.Name, musicList.Musics!, 0, true)
                               .ContinueWith(LoggerService.HandleException)
                               .ConfigureAwait(false);
                return;
            }

        var (count, paths) = MusicListItemsRepository.Instance.GetAllAsync((musicList.Name, musicList.Creator))
                                                     .ConfigureAwait(false)
                                                     .GetAwaiter()
                                                     .GetResult();
        PlaylistManager.Instance.ReplaceAsync(
                           musicList.Name,
                           paths.Select(path => MusicItemsManager.All.MusicItems[path]),
                           count,
                           0,
                           true)
                       .ContinueWith(LoggerService.HandleException)
                       .ConfigureAwait(false);
    }

    ~AllMusicListsPanelViewModel() { MusicListsManager.CollectionChanged -= Update; }
}