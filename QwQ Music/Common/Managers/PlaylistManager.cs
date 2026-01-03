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
                MusicItemsManager.All.Name,
                PlaylistRepository.ReadAsync()
                                  .ConfigureAwait(false)
                                  .GetAwaiter()
                                  .GetResult()
                                  .Select(path => MusicItemsManager.All.MusicItems[path]),
                isPlayNow: false)
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

    // ReSharper disable once InconsistentNaming
    public const string CUSTOM = nameof(CUSTOM);

    // ReSharper disable once InconsistentNaming
    private const string UNKNOWN = nameof(UNKNOWN);

    public string CurrentListName { get; private set; } = UNKNOWN;

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
            var items = MusicItemsManager.All.MusicItems.Values;
            ReplaceAsync(MusicItemsManager.All.Name, items, items.First(), true)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        return ActualPlaylist.First();
    }

    public PlaylistItemModel Insert(PlaylistItemModel anchor, MusicItemModel musicItem) {
        return InsertRange(anchor, [musicItem]).First();
    }

    public IEnumerable<PlaylistItemModel> InsertRange(
        PlaylistItemModel anchor,
        IEnumerable<MusicItemModel> musicItems) {
        CurrentListName = CUSTOM;
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
        CurrentListName = CUSTOM;
        PlaylistItemModel[] items = musicItems.ToArray();
        items.AsParallel().ForAll(item => SequentialPlaylist.Remove(item));
        ActualPlaylist.RemoveAll(items);
    }

    public void RemoveAllOf(IEnumerable<MusicItemModel> musicItems) {
        CurrentListName = CUSTOM;
        MusicItemModel[] items = musicItems.ToArray();
        PlaylistItemModel[] playlistItems = SequentialPlaylist.AsParallel()
                                                              .Where(item => Enumerable.Contains(items, item.Model))
                                                              .ToArray();
        playlistItems.AsParallel().ForAll(item => SequentialPlaylist.Remove(item));
        ActualPlaylist.RemoveAll(playlistItems);
    }

    public void Clear() {
        CurrentListName = CUSTOM;
        SequentialPlaylist.Clear();
        ActualPlaylist.Clear();
        PlaylistItemModel.Reset();
    }

    public async Task ReplaceAsync(
        string name,
        IEnumerable<MusicItemModel> musicItems,
        MusicItemModel? target = null,
        bool isPlayNow = false) {
        List<MusicItemModel> items = musicItems.ToList();
        if (name is not CUSTOM and not UNKNOWN && CurrentListName == name && ActualPlaylist.Count == items.Count) {
            return;
        }

        CurrentListName = name;
        AudioPlayManager.Instance.Pause();
        Clear();
        SequentialPlaylist.AddRange(items.Select(item => new PlaylistItemModel(item)).ToList());
        ActualPlaylist.AddRange(PlayMode is PlayMode.Random ? SequentialPlaylist.Shuffle() : SequentialPlaylist);
        if (items.Count == 0)
            return;
        PlaylistItemModel item =
            target is null ? ActualPlaylist[0] : ActualPlaylist.Single(item => item.Model == target);
        await AudioPlayManager.Instance.SetThisMusicAsync(item, isPlayNow).ConfigureAwait(false);
    }
}