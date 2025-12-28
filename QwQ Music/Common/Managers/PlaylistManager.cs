using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.Models.Enums;

namespace QwQ_Music.Common.Managers;

public partial class PlaylistManager : ObservableObject {
    public static PlaylistManager Instance { get; } = new();

    private PlaylistManager() {
        _ = ReplaceAsync(
                PlaylistRepository.ReadAsync()
                                  .ConfigureAwait(false)
                                  .GetAwaiter()
                                  .GetResult()
                                  .Select(path => MusicItemsManager.All.MusicItems[path]),
                false)
            .ContinueWith(LoggerService.HandleException)
            .ContinueWith(async Task (_) => {
                await AudioPlayManager.Instance.SetThisMusicAsync(
                                          ActualPlaylist.FirstOrDefault(
                                              item => item.Model.FilePath ==
                                                      AudioPlayManager.Instance.PlayerConfig.LastPlayedFilePath,
                                              PlaylistItemModel.RefDefault),
                                          false)
                                      .ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    public PlaylistItemModel CurrentItem { get; set; } = PlaylistItemModel.RefDefault;

    public PlayMode PlayMode {
        get;
        set {
            if (field == value)
                return;
            field = value;
            // ReSharper disable once InvertIf
            if (value == PlayMode.Random) {
                ActualPlaylist.Clear();
                ActualPlaylist.AddRange(SequentialPlaylist.Shuffle());
            }
        }
    } = PlayMode.Sequential;

    public int CurrentIndex => ActualPlaylist.IndexOf(CurrentItem);

    public AvaloniaList<PlaylistItemModel> ActualPlaylist { get; } = [];

    public readonly List<PlaylistItemModel> SequentialPlaylist = [];


    public int Count => SequentialPlaylist.Count;

    public PlaylistItemModel First() {
        if (ActualPlaylist.Count == 0) {
            ReplaceAsync(MusicItemsManager.All.MusicItems.Values, true).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        return ActualPlaylist.First();
    }

    public PlaylistItemModel Insert(PlaylistItemModel anchor, MusicItemModel musicItem) {
        return InsertRange(anchor, [musicItem]).First();
    }

    public IEnumerable<PlaylistItemModel> InsertRange(
        PlaylistItemModel anchor,
        IEnumerable<MusicItemModel> musicItems) {
        var items = musicItems.Select(item => new PlaylistItemModel(item)).ToArray();
        SequentialPlaylist.InsertRange(SequentialPlaylist.IndexOf(anchor), items);
        ActualPlaylist.InsertRange(ActualPlaylist.IndexOf(anchor), items);
        return items;
    }

    public PlaylistItemModel Add(MusicItemModel musicItem) { return AddRange([musicItem]).First(); }

    public IEnumerable<PlaylistItemModel> AddRange(IEnumerable<MusicItemModel> musicItems) {
        return InsertRange(ActualPlaylist.Last(), musicItems);
    }

    public PlaylistItemModel InsertToNext(MusicItemModel musicItem) {
        return InsertRange(CurrentItem, [musicItem]).First();
    }

    public IEnumerable<PlaylistItemModel> InsertToNext(IEnumerable<MusicItemModel> musicItems) {
        return InsertRange(CurrentItem, musicItems);
    }

    [RelayCommand]
    public void AddSelectedToNext(IEnumerable<MusicItemModel> musicItems) { InsertRange(CurrentItem, musicItems); }

    public void Remove(PlaylistItemModel musicItem) { RemoveRange([musicItem]); }

    [RelayCommand]
    public void RemoveRange(IEnumerable<PlaylistItemModel> musicItems) {
        PlaylistItemModel[] items = musicItems.ToArray();
        items.AsParallel().ForAll(item => SequentialPlaylist.Remove(item));
        ActualPlaylist.RemoveAll(items);
    }

    public void RemoveAllOf(IEnumerable<MusicItemModel> musicItems) {
        MusicItemModel[] items = musicItems.ToArray();
        PlaylistItemModel[] playlistItems = SequentialPlaylist.AsParallel()
                                                              .Where(item => Enumerable.Contains(items, item.Model))
                                                              .ToArray();
        playlistItems.AsParallel().ForAll(item => SequentialPlaylist.Remove(item));
        ActualPlaylist.RemoveAll(playlistItems);
    }

    public void Clear() {
        SequentialPlaylist.Clear();
        ActualPlaylist.Clear();
        PlaylistItemModel.Reset();
    }

    public async Task ReplaceAsync(IEnumerable<MusicItemModel> musicItems, bool isPlayNow) {
        AudioPlayManager.Instance.Pause();
        Clear();
        SequentialPlaylist.AddRange(musicItems.Select(item => new PlaylistItemModel(item)));
        ActualPlaylist.AddRange(PlayMode is PlayMode.Random ? SequentialPlaylist.Shuffle() : SequentialPlaylist);
        if (isPlayNow) {
            await AudioPlayManager.Instance.PlayThisMusicAsync(Instance.First()).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public Task ReplaceAndPlayAsync(IEnumerable<MusicItemModel> musicItems) { return ReplaceAsync(musicItems, true); }
}