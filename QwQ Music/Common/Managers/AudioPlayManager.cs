using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Audio;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.Models.Enums;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.Common.Managers;

public class MusicItemChangedEventArgs(PlaylistItemModel oldItem, PlaylistItemModel newItem) : EventArgs {
    public readonly PlaylistItemModel OldItem = oldItem;
    public readonly PlaylistItemModel NewItem = newItem;
}

public partial class AudioPlayManager : ViewModelBase {
    public static AudioPlayManager Instance { get; } = new();

    #region 音频处理

    private async Task SetCurrentMusicItemAsync(PlaylistItemModel musicItem, bool restart) {
        await LoggerService.DebugAsync($"正在切换音频：由《{CurrentMusicItem.Model.Title}》切换到《{musicItem.Model.Title}》。")
                           .ConfigureAwait(false);
        if (VerifyMusicItem(musicItem)) {
            OnPlayingChanged(false);
        }

        try {
            Position = 0;

            if (musicItem != PlaylistItemModel.RefDefault) {
                _audioPreprocessor.UpdateMusicPlayProgress(musicItem.Model, restart);
                await _audioPreprocessor.InitializeAudioTrackAsync(musicItem.Model).ConfigureAwait(false);
                Position = musicItem.Model.Record.TotalSeconds;
                _audioPreprocessor.InitialTime = musicItem.Model.Record;
                LyricOffset = musicItem.Model.LyricOffset;
            }

            PlaylistItemModel oldItem = CurrentMusicItem;
            CurrentMusicItem = musicItem;

            MusicItemChanged?.Invoke(this, new MusicItemChangedEventArgs(oldItem, musicItem));
            await LoggerService.InfoAsync($"已切换到《{musicItem.Model.Title}》。").ConfigureAwait(false);
        } catch (Exception ex) {
            OnPlayingChanged(false);

            await LoggerService.ErrorAsync($"初始化新音轨失败:\n{ex.Message}\n{ex.StackTrace}").ConfigureAwait(false);

            NotificationService.Error(
                "播放失败",
                $"初始化新音轨失败: {ex.Message}\n可能的原因: 当前{musicItem.Model.EncodingFormat}格式不支持解码");
        }
    }

    #endregion

    #region 其他

    /// <summary>
    ///     注册热键功能
    /// </summary>
    private void RegisterHotkeyFunctions() {
        LoggerService.Debug("正在注册快捷键...");
        HotkeyService.RegisterFunctionAction(HotkeyFunction.Previous, () => PreviousSongCommand.Execute(null));

        HotkeyService.RegisterFunctionAction(HotkeyFunction.Next, () => NextSongCommand.Execute(null));

        HotkeyService.RegisterFunctionAction(HotkeyFunction.TogglePlay, () => TogglePlayStateCommand.Execute(null));

        HotkeyService.RegisterFunctionAction(HotkeyFunction.ToggleMute, () => ToggleMuteCommand.Execute(null));

        HotkeyService.RegisterFunctionAction(HotkeyFunction.SwitchPlayMode, () => TogglePlayModeCommand.Execute(null));

        HotkeyService.RegisterFunctionAction(
            HotkeyFunction.VolumeUp,
            () => {
                if (Volume < 100)
                    Volume += 5;
            });

        HotkeyService.RegisterFunctionAction(
            HotkeyFunction.VolumeDown,
            () => {
                if (Volume > 0)
                    Volume -= 5;
            });

        HotkeyService.RegisterFunctionAction(HotkeyFunction.Replay, () => ReplayCommand.Execute(null));

        HotkeyService.RegisterFunctionAction(
            HotkeyFunction.ShowPlaylistInfo,
            () => {
                NotificationService.Info(
                    "你知道吗？",
                    $"当前播放列表有: {PlaylistManager.Instance.Count} 首音乐！\n" +
                    $"现在正在播放第 {PlaylistManager.Instance.CurrentIndex} 首");
            });

        HotkeyService.RegisterFunctionAction(
            HotkeyFunction.ShowAudioInfo,
            () => {
                NotificationService.Info(
                    "你知道吗？",
                    $"{(IsPlaying ? "正在播放" : "已暂停")}的音乐叫做: {CurrentMusicItem.Model.Title} 哦！\n" + $"你的音量是: {Volume}% ");
            });
        LoggerService.Debug("快捷键注册完毕。");
    }

    #endregion

    #region 属性和字段

    public AudioPlayer AudioPlayer { get; }

    private readonly AudioPreprocessor _audioPreprocessor;

    private readonly Timer _lrcTimer;

    public PlaylistItemModel CurrentMusicItem {
        get => PlaylistManager.Instance.CurrentItem;
        set {
            PlaylistManager.Instance.CurrentItem = value;
            OnPropertyChanged();
        }
    }

    public Bitmap CoverImage {
        get;
        set {
            if (field == value)
                return;
            Bitmap temp = field;
            field = value;
            OnPropertyChanged();
            temp.Dispose();
        }
    } = CacheManager.Default;

    public bool IsPlaying {
        get;
        set {
            if (!SetProperty(ref field, value))
                return;

            PlaybackStateChanged?.Invoke(this, value);
        }
    }

    public PlayerConfig PlayerConfig { get; } = ConfigManager.PlayerConfig;

    [ObservableProperty]
    public partial LyricsModel LyricsModel { get; set; } = new();

    private double _position;


    public void UpdateCurrentLyric() {
        LyricsModel.UpdateLyricsIndex(_position);
        // 当播放位置改变时，重新设置歌词定时器
        if (IsPlaying) {
            UpdateLyricsTimer();
        }
    }

    public double Position {
        get => _position;
        set {
            _position = value;
            AudioPlayer.Seek(value);
            OnPropertyChanged();
            UpdateCurrentLyric();
        }
    }

    public int Volume {
        get => PlayerConfig.Volume;
        set {
            int result = Math.Clamp(value, 0, 100);

            if (result == PlayerConfig.Volume)
                return;

            PlayerConfig.Volume = result;
            AudioPlayer.Volume = NormalizeVolume(result);

            IsMuted = result == 0f;
            OnPropertyChanged();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float NormalizeVolume(int volume) { return volume / 100f; }

    public bool IsMuted {
        get => PlayerConfig.IsMuted;
        set {
            if (value == PlayerConfig.IsMuted)
                return;

            PlayerConfig.IsMuted = value;
            AudioPlayer.IsMute = value;

            OnPropertyChanged();
        }
    }

    public float Speed {
        get => PlayerConfig.PlaybackSpeed;
        set {
            float result = Math.Clamp(value, 0.5f, 1.5f);

            if (Math.Abs(PlayerConfig.PlaybackSpeed - result) < 1e-6f)
                return;

            PlayerConfig.PlaybackSpeed = result;
            OnPropertyChanged();
            AudioPlayer.Speed = result;
        }
    }

    public double LyricOffset {
        get => LyricsModel.Offset;
        set {
            LyricsModel.Offset = value;
            CurrentMusicItem.Model.LyricOffset = value;

            OnPropertyChanged();
        }
    }

    #endregion

    #region 事件

    public event EventHandler<bool>? PlaybackStateChanged;

    public event EventHandler<MusicItemChangedEventArgs>? MusicItemChanged;

    #endregion

    #region 初始化与终结

    private AudioPlayManager() {
        AudioPlayer = new AudioPlayer();
        _audioPreprocessor = new AudioPreprocessor(AudioPlayer);

        AudioPlayer.Volume = NormalizeVolume(Volume);
        AudioPlayer.IsMute = IsMuted;
        AudioPlayer.Speed = Speed;

        AudioPlayer.PositionChanged += OnPositionChanged;
        AudioPlayer.PlaybackCompleted += AudioPlayerOnPlaybackCompleted;

        // 初始化歌词滚动定时器
        _lrcTimer = new Timer();
        _lrcTimer.Elapsed += OnLrcTimerElapsed;
        _lrcTimer.AutoReset = false;

        // 注册热键功能
        RegisterHotkeyFunctions();
    }

    public void Shutdown() {
        AudioPlayer.Stop();
        PlayerConfig.LastPlayedFilePath = CurrentMusicItem.Model.FilePath;
        _audioPreprocessor.UpdateMusicPlayProgress(CurrentMusicItem.Model);

        _lrcTimer.Elapsed -= OnLrcTimerElapsed;
        _lrcTimer.Dispose();

        AudioPlayer.Dispose();
    }

    #endregion

    #region 事件处理

    private void OnPositionChanged(object? sender, double positionInSeconds) {
        CurrentMusicItem.Model.Record = TimeSpan.FromSeconds(positionInSeconds);
        _position = positionInSeconds;
        OnPropertyChanged(nameof(Position));
        UpdateCurrentLyric();
    }

    private void UpdateLyricsTimer() {
        if (!IsPlaying)
            return;

        // 停止当前定时器
        _lrcTimer.Stop();

        // 计算到下一句歌词的时间间隔
        double nextInterval = LyricsModel.GetNextLyricsInterval(AudioPlayer.Position);

        if (!(nextInterval > 0))
            return;

        // 设置定时器间隔为到下一句歌词的时间
        _lrcTimer.Interval = nextInterval * 1000; // 转换为毫秒
        _lrcTimer.Start();
    }

    private void OnLrcTimerElapsed(object? sender, ElapsedEventArgs e) {
        // 更新当前歌词
        LyricsModel.UpdateLyricsIndex(AudioPlayer.Position);

        // 计算到下一句歌词的时间间隔
        double nextInterval = LyricsModel.GetNextLyricsInterval(AudioPlayer.Position);

        if (!(nextInterval > 0))
            return;

        // 设置定时器间隔为到下一句歌词的时间
        _lrcTimer.Interval = nextInterval * 1000; // 转换为毫秒
        _lrcTimer.Start();
    }

    private void AudioPlayerOnPlaybackCompleted(object? sender, EventArgs e) {
        try {
            CurrentMusicItem.Model.Record = TimeSpan.Zero;

            // 根据播放模式处理播放完成后的行为
            if (PlayerConfig.PlayMode == PlayMode.SingleLoop) {
                Replay(); // 单曲循环模式下，重新播放当前歌曲
            } else if (PlayerConfig.AutoSwitchNext) {
                NextSongAsync().ContinueWith(LoggerService.HandleException).ConfigureAwait(false);
            } else {
                OnPlayingChanged(false);
            }
        } catch (Exception ex) {
            NotificationService.Error($"音频播放完成后切换下一首音频时遇到错误：{ex.Message}");
            LoggerService.Error($"音频播放完成后切换下一首音频时遇到错误：{ex.Message}\n{ex.StackTrace}");
        }
    }

    #endregion

    #region 播放控制方法

    [RelayCommand]
    public void TogglePlayState() {
        TogglePlayStateAsync().ContinueWith(LoggerService.HandleException).ConfigureAwait(false);
    }

    public async Task TogglePlayStateAsync() {
        if (MusicItemsManager.All.Count == 0) {
            NotificationService.Info("音乐库中一首音乐也没有啦，需要我为你播放一首空空如也吗？");

            return;
        }

        if (CurrentMusicItem == PlaylistItemModel.RefDefault) {
            await SetCurrentMusicItemAsync(PlaylistManager.Instance.First(), true).ConfigureAwait(false);
        }

        if (VerifyMusicItem(CurrentMusicItem)) {
            OnPlayingChanged(!IsPlaying);
        }
    }

    [RelayCommand]
    public void PlayThisMusic(PlaylistItemModel? musicItem) {
        SetThisMusicAsync(musicItem, true).ContinueWith(LoggerService.HandleException).ConfigureAwait(false);
    }

    public async Task SetThisMusicAsync(PlaylistItemModel? musicItem, bool isPlaynow) {
        if (musicItem is not { } item || !VerifyMusicItem(musicItem)) {
            return;
        }

        if (CurrentMusicItem.Equals(item)) {
            OnPlayingChanged(!IsPlaying);
        } else {
            await SetCurrentMusicItemAsync(item, !isPlaynow).ConfigureAwait(false);
            OnPlayingChanged(isPlaynow);
        }
    }

    public async Task PreviousSongAsync() {
        await SetAndPlayAsync(GetMusicItemIndex(PlaylistManager.Instance.CurrentIndex, -1)).ConfigureAwait(false);
    }


    public async Task NextSongAsync() {
        await SetAndPlayAsync(GetMusicItemIndex(PlaylistManager.Instance.CurrentIndex, 1)).ConfigureAwait(false);
    }

    [RelayCommand]
    public void PreviousSong() {
        PreviousSongAsync().ContinueWith(LoggerService.HandleException).ConfigureAwait(false);
    }

    [RelayCommand]
    public void NextSong() { NextSongAsync().ContinueWith(LoggerService.HandleException).ConfigureAwait(false); }

    public void Pause() {
        if (!IsPlaying)
            OnPlayingChanged(false);
    }

    public void Stop() {
        if (!IsPlaying)
            OnPlayingChanged(false);
    }

    [RelayCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ToggleMute() { IsMuted = !IsMuted; }

    [RelayCommand]
    public void Replay() {
        if (!VerifyMusicItem(CurrentMusicItem))
            return;

        Position = 0;
        OnPlayingChanged(true);
    }

    public void CheckForRemovedItems(IEnumerable<MusicItemModel> successItems) {
        if (!successItems.Contains(CurrentMusicItem.Model))
            return;

        if (IsPlaying) {
            IsPlaying = false;
            AudioPlayer.Stop();
        }

        NotificationService.Info($"当前音乐《{CurrentMusicItem.Model.Title}》被移除了哦~");
        NextSongAsync().ContinueWith(LoggerService.HandleException).ConfigureAwait(false);
    }

    #endregion

    #region 辅助方法

    private void OnPlayingChanged(bool isPlayNow) {
        IsPlaying = isPlayNow;

        if (isPlayNow) {
            AudioPlayer.Play();
            UpdateLyricsTimer();
        } else {
            AudioPlayer.Pause();
            _lrcTimer.Stop();
        }
    }

    private bool VerifyMusicItem(PlaylistItemModel? musicItem) {
        if (musicItem is not { } item || !File.Exists(item.Model.FilePath)) {
            NotificationService.Error($"当前音乐不存在，请切换音乐！\n无法找到音乐文件:  {musicItem?.Model.FilePath}");

            return false;
        }

        if (PlaylistManager.Instance.Count == 0) {
            PlaylistManager.Instance.AddRange(MusicItemsManager.All.MusicItems.Values);
            NotificationService.Info($"当前播放列表为空，已自动填充为全部音乐！共 {PlaylistManager.Instance.Count} 首~");
        }

        if (PlaylistManager.Instance.ActualPlaylist.Contains(item))
            return true;

        PlaylistManager.Instance.Add(item.Model);

        NotificationService.Info($"当前音乐《{item.Model.Title}》不在播放列表中，已自动添加到播放列表末尾~");

        return true;
    }

    private async Task SetAndPlayAsync(int index) {
        if (index == -1) {
            OnPlayingChanged(false);
            await SetCurrentMusicItemAsync(PlaylistItemModel.RefDefault, true).ConfigureAwait(false);

            return;
        }

        if (index < 0 || index >= PlaylistManager.Instance.Count)
            return;

        var musicItem = PlaylistManager.Instance.ActualPlaylist[index];

        if (VerifyMusicItem(musicItem)) {
            await SetCurrentMusicItemAsync(musicItem, PlayerConfig.IsRestartPlay).ConfigureAwait(false);
            OnPlayingChanged(true);
        }
    }

    [RelayCommand]
    private void TogglePlayMode() {
        // 循环切换播放模式
        PlayerConfig.PlayMode = (PlayMode)(((int)PlayerConfig.PlayMode + 1) % Enum.GetValues<PlayMode>().Length);

        PlaylistManager.Instance.PlayMode = PlayerConfig.PlayMode;
        LoggerService.Info($"切换到{PlayModeName}模式。");
        OnPropertyChanged(nameof(PlayModeName));
    }

    private int GetMusicItemIndex(int current, int offset) {
        //Count=0时返回-1; =1时返回 0
        int result = PlaylistManager.Instance.Count - 1;
        current += offset;
        // current的边界判断
        // current越上界时，返回最后一项。
        if (result <= 0 || current < 0) {
            return result;
        }

        // 此处复用 result 作为下界。       
        // current越下界时意味着歌单播放完毕。模式为顺序播放时返回-1，其它时返回首项。
        if (current > result) {
            return PlaylistManager.Instance.PlayMode == PlayMode.Sequential ? -1 : 0;
        }

        return current;
    }

    public string PlayModeName => //TODO: i18n
        PlayerConfig.PlayMode switch {
            PlayMode.Sequential => "顺序播放",
            PlayMode.Random     => "随机播放",
            PlayMode.SingleLoop => "单曲循环",
            PlayMode.Loop       => "列表循环",
            _                   => throw new IndexOutOfRangeException($"不存在的播放模式:{PlayerConfig.PlayMode}")
        };

    #endregion
}