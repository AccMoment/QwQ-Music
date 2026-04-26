using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QwQ_Music.Models;

/// 歌词行结构体
public record struct LyricLine(double TimePoint, string Primary, string? Secondary = null) {
    public static readonly LyricLine Empty = new(0, string.Empty);
}

public partial class LyricsModel : ObservableObject {
    public delegate void LyricLineChangedEventHandler(object sender, LyricLine currentLyric, LyricLine? nextLyric);

    public double Offset {
        get => Lyrics.Offset;
        set => Lyrics.Offset = value;
    }

    [ObservableProperty]
    public partial int CurrentIndex { get; set; }

    [ObservableProperty]
    public partial LyricLine Current { get; private set; }

    [ObservableProperty]
    public partial LyricLine Next { get; private set; }

    [ObservableProperty]
    public partial LyricsData Lyrics { get; set; } = LyricsData.Loading;

    /// <summary>
    ///     获取歌词总数
    /// </summary>
    public int Total => Lyrics.Data.Count;

    public event LyricLineChangedEventHandler? LyricLineChanged;

    /// <summary>
    ///     获取当前歌词到下一句歌词的时间间隔
    /// </summary>
    /// <param name="currPos">当前播放时间（秒）</param>
    /// <returns>到下一句歌词的时间间隔（秒），如果没有下一句则返回-1</returns>
    public double GetNextLyricsInterval(double currPos) {
        if (Total == 0 || Total - 1 == CurrentIndex)
            return -1;
        // 计算到下一句歌词的时间间隔（考虑偏移量）
        return Lyrics[CurrentIndex + 1].TimePoint - currPos;
    }

    public void UpdateLyricsIndex(double currPos) {
        if (Lyrics.Data.Count == 0)
            return;
        int newIndex = Math.Max(0, Lyrics.Data.FindLastIndex(line => line.TimePoint <= currPos));

        CurrentIndex = newIndex;
        Current = Lyrics[CurrentIndex];

        Next = CurrentIndex + 1 == Total ? LyricLine.Empty : Lyrics.Data[CurrentIndex + 1];

        // 触发歌词变更事件，同时传递当前歌词和下一句歌词
        LyricLineChanged?.Invoke(this, Current, Next);
    }

    public void Reset(LyricsData? newValue = null) {
        Offset = 0;
        CurrentIndex = 0;
        Current = Lyrics[0];
        Next = Total > 1 ? Lyrics[1] : Lyrics[0];
        if (newValue != null)
            Lyrics = newValue;
    }
}

public class LyricsData {
    public static readonly LyricsData Loading = new() {
        Title = null, Artist = null, Album = null, Data = [new LyricLine(0, "歌词加载中...")]
    };

    protected LyricsData() { }

    private LyricLine DefaultLyricLine => new(0, $"{Title} - {Artist}", Album);

    public LyricLine this[int index] => index < Data.Count - 1 && index > 0 ? Data[index] : DefaultLyricLine;

    // 歌词元数据
    public required string? Title { get; set; }

    public required string? Artist { get; set; }

    public required string? Album { get; set; }

    public string? Creator { get; set; }

    public double Offset {
        get;
        set {
            Data.AsParallel().ForAll(line => line.TimePoint += value - field);
            field = value;
        }
    }

    public List<LyricLine> Data {
        get;
        set {
            field = value;
            field.AsParallel().ForAll(line => line.TimePoint += Offset);
        }
    } = [];

    public static LyricsData Create(string? title, string? artist, string? album, string? creator = null) {
        return new LyricsData {
            Title = title,
            Artist = artist,
            Album = album,
            Creator = creator,
            Data = [new LyricLine(0, $"{title} - {artist}", album)]
        };
    }
}