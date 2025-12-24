using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.Models.Enums;

namespace QwQ_Music.Common.Managers;

public readonly record struct PlaylistItemModel {
    public static readonly PlaylistItemModel RefDefault = new(MusicItemModel.Default, 0);

    private PlaylistItemModel(MusicItemModel model, ulong id) {
        Model = model;
        Id = id;
    }

    public PlaylistItemModel(MusicItemModel model) { Model = model; }

    private static ulong IdAllocator {
        get => field++;
        set;
    } = 1;

    public MusicItemModel Model { get; }
    public readonly ulong Id = IdAllocator;

    public static void Reset() { IdAllocator = 1; }
}

public class PlaylistManager : ObservableObject {
    public static readonly PlaylistManager Instance = new();

    private PlaylistManager() {
        Replace(
            PlaylistRepository.ReadAsync()
                              .ConfigureAwait(false)
                              .GetAwaiter()
                              .GetResult()
                              .Select(path => MusicItemsManager.All.MusicItems[path]));
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

    public int CurrentIndex => SequentialPlaylist.IndexOf(CurrentItem);

    public AvaloniaList<PlaylistItemModel> ActualPlaylist { get; } = [];

    public readonly List<PlaylistItemModel> SequentialPlaylist = [];


    public int Count => SequentialPlaylist.Count;

    public PlaylistItemModel First() {
        if (ActualPlaylist.Count == 0) {
            Replace(MusicItemsManager.All.MusicItems.Values);
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

    public void Remove(PlaylistItemModel musicItem) { RemoveRange([musicItem]); }

    public void RemoveRange(IEnumerable<PlaylistItemModel> musicItems) {
        PlaylistItemModel[] items = musicItems.ToArray();
        items.AsParallel().ForAll(item => SequentialPlaylist.Remove(item));
        ActualPlaylist.RemoveAll(items);
    }

    public void RemoveAllOf(IEnumerable<MusicItemModel> musicItems) {
        MusicItemModel[] items = musicItems.ToArray();
        PlaylistItemModel[] playlistItems =
            SequentialPlaylist.AsParallel().Where(item => items.Contains(item.Model)).ToArray();
        playlistItems.AsParallel().ForAll(item => SequentialPlaylist.Remove(item));
        ActualPlaylist.RemoveAll(playlistItems);
    }

    public void Clear() {
        SequentialPlaylist.Clear();
        ActualPlaylist.Clear();
        PlaylistItemModel.Reset();
    }

    public void Replace(IEnumerable<MusicItemModel> musicItems) {
        Clear();
        SequentialPlaylist.AddRange(musicItems.Select(item => new PlaylistItemModel(item)));
        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
        if (PlayMode is PlayMode.Random)
            ActualPlaylist.AddRange(SequentialPlaylist.Shuffle());
        else
            ActualPlaylist.AddRange(SequentialPlaylist);
    }
}