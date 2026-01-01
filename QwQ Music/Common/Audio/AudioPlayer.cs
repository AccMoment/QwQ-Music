using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using QwQ_Music.Common.Helpers;
using QwQ_Music.Common.Interfaces;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Models.ConfigModels;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Providers;
using SoundFlow.Structs;
using SoundFlow.Visualization;
using DrawerManager = QwQ_Music.Common.Managers.DrawerManager;
using Timer = System.Timers.Timer;

namespace QwQ_Music.Common.Audio;

public enum MediaPlaybackStatus {
    Changing, Fading, Playing, Paused, Stopped
}

public enum MediaPlaybackMode {
    Repeat, List, ListRepeat, Shuffle
}

/// <summary>
///     基于SoundFlow实现的音频播放器
/// </summary>
public class AudioPlayer : IAudioPlayer {
    private readonly PlayComponent _soundModifier = ConfigManager.SoundModifierConfig.PlayComponent;
    private static readonly MiniAudioEngine AudioEngine = Task.Run(() => new MiniAudioEngine()).Result;
    private static readonly ISystemMediaControlImpl SystemMedia = SystemMediaControl.CreateSystemMediaControl();
    public Stream? Current { get; private set; }

    public bool IsDisposed;

    public MediaPlaybackStatus Status = MediaPlaybackStatus.Stopped;

    private AudioPlaybackDevice PlayerDevice {
        get {
            if (field?.IsDisposed ?? true) {
                field = InitializeDevice();
            }

            return field;
        }
        set;
    }

    private StreamDataProvider? _soundDataProvider;
    private SoundPlayer? _soundPlayer;
    private SpectrumAnalyzer? _spectrumAnalyzer;

    private Timer FadeOutTimer {
        get {
            // ReSharper disable once InvertIf
            if (field is null) {
                field = new Timer { AutoReset = false };
                field.Elapsed += FadeOutAwaiter;
            }

            return field;
        }
    }

    private DispatcherTimer UpdateTimer =>
        field ??= new DispatcherTimer(TimeSpan.FromMilliseconds(1000), DispatcherPriority.Render, OnProgressTimerTick);

    private DispatcherTimer SpecTimer =>
        field ??= new DispatcherTimer(
            TimeSpan.FromMilliseconds(ConfigManager.UiConfig.SpectrumConfig.UpdateIntervalMs),
            DispatcherPriority.Render,
            OnSpectrumVisualizer);

    /// <summary>
    ///     音频格式
    /// </summary>
    public AudioFormat AudioFormat { get; set; } = AudioFormat.DvdHq;

    /// <inheritdoc />
    public event EventHandler<double>? PositionChanged;

    /// <inheritdoc />
    public event EventHandler? PlaybackCompleted;

    /// <inheritdoc />
    public double Position {
        get => _soundPlayer?.Time ?? -1;
        set => Seek(value);
    }

    /// <inheritdoc />
    public bool IsMute {
        get;
        set {
            field = value;

            _soundPlayer?.Mute = value;
        }
    }

    /// <inheritdoc />
    public float Volume {
        get;
        set {
            field = value;
            _soundPlayer?.Volume = value;
        }
    }

    /// <inheritdoc />
    public float Speed {
        get;
        set {
            field = value;
            _soundPlayer?.PlaybackSpeed = value;
        }
    }

    /// <summary>
    ///     开始播放
    /// </summary>
    public void Play() {
        Debug.Assert(_soundPlayer is not null);
        if (Current is null) {
            LoggerService.Warning("当前音频流已不可用");
            return;
        }

        if (Status is MediaPlaybackStatus.Playing) {
            LoggerService.Warning("重复的播放请求");
            return;
        }

        // 检查淡入效果器是否启用
        if (_soundModifier.FadeModifier.Enabled) {
            Status = MediaPlaybackStatus.Fading;
            // 应用淡入效果
            ResetTimer(FadeOutTimer);
            _soundModifier.FadeModifier.BeginFadeIn();
        }

        Status = MediaPlaybackStatus.Playing;
        PlayerDevice.Start();
        _soundPlayer.Play();
        SpecTimer.Start();

        UpdateTimer.Start();
    }

    /// <summary>
    ///     暂停播放
    /// </summary>
    public void Pause(bool instant = false) {
        if (Status is not MediaPlaybackStatus.Playing and not MediaPlaybackStatus.Fading) {
            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (Status is MediaPlaybackStatus.Paused)
                LoggerService.Warning("重复的暂停请求");
            else
                LoggerService.Warning($"错误的预期状态。预期{nameof(MediaPlaybackStatus.Paused)}，实际为{Status.ToString()}");
            return;
        }

        // 检查淡出效果器是否启用
        if (!instant && _soundModifier.FadeModifier.Enabled) {
            Status = MediaPlaybackStatus.Fading;
            // 应用淡出效果
            _soundModifier.FadeModifier.BeginFadeOut();
            // 重置淡出计时器，等待淡出效果完成
            ResetTimer(FadeOutTimer, _soundModifier.FadeModifier.FadeOutTimeMs);
            FadeOutTimer.Start();
        } else {
            // 直接暂停
            Debug.Assert(_soundPlayer is not null);
            _soundPlayer?.Pause();
            UpdateTimer.Stop();
            SpecTimer.Stop();
        }
    }

    /// <summary>
    ///     停止播放并释放资源
    /// </summary>
    public void Stop() {
        Pause();
        _soundPlayer?.PlaybackEnded -= OnPlaybackCompleted;
        UpdateTimer.Stop();
        SpecTimer.Stop();
        try {
            TimeoutHelper.Timeout(1500, PlayerDevice.Stop, () => PlayerDevice = null!); // 此处可能导致超长时间卡顿并阻塞UI，使用超时计时器来处理
        } catch (TimeoutException) {
            LoggerService.Error("播放设备停止超时，已放弃。");
        }

        Current?.Dispose();
        _soundDataProvider?.Dispose();
        if (_soundPlayer is null)
            return;
        Debug.Assert(PlayerDevice.MasterMixer.Components.Count <= 1);
        foreach (SoundComponent comp in PlayerDevice.MasterMixer.Components) {
            PlayerDevice.MasterMixer.RemoveComponent(comp);
        }

        _soundPlayer.Dispose();
        Status = MediaPlaybackStatus.Stopped;
    }

    /// <summary>
    ///     跳转到指定位置（单位：秒）
    /// </summary>
    public void Seek(double positionInSeconds) {
        _soundPlayer?.Seek((float)Math.Clamp(positionInSeconds, 0, _soundPlayer.Duration));
    }

    public void InitializeAudio(string filePath, double replayGain) {
        InitializeAudioAsync(filePath, replayGain).Wait();
    }

    public void InitializeAudio(Stream stream, double replayGain) { InitializeAudioAsync(stream, replayGain).Wait(); }

    /// <summary>
    ///     释放所有资源
    /// </summary>
    public void Dispose() {
        Stop();
        IsDisposed = true;
        FadeOutTimer.Dispose();
        PlayerDevice.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async Task InitializeAudioAsync(string filePath, double replayGain) {
        await InitializeAudioAsync(File.OpenRead(filePath), replayGain);
    }

    /// <summary>
    ///     频谱数据更新事件
    /// </summary>
    public event EventHandler<float[]>? SpectrumDataUpdated;

    /// <summary>
    ///     淡出定时器事件处理
    /// </summary>
    private void FadeOutAwaiter(object? sender, EventArgs e) {
        if (Status is MediaPlaybackStatus.Playing) {
            LoggerService.Warning("警告：已忽略的暂停");
            return;
        }

        Debug.Assert(_soundPlayer is not null);
        Status = MediaPlaybackStatus.Paused;
        _soundPlayer.Pause();
        UpdateTimer.Stop();
        SpecTimer.Stop();
    }

    public async Task InitializeAudioAsync(Stream audioStream, double replayGain) {
        Stop();
        if (AudioFormat != PlayerDevice.Format)
            TimeoutHelper.Timeout(2000, PlayerDevice.Dispose, () => PlayerDevice = null!);
        Current = audioStream;
        try {
            await InitializeNewTrackAsync(audioStream, replayGain).ConfigureAwait(false);
        } catch (Exception ex) {
            await LoggerService.ErrorAsync($"初始化音轨失败: {ex}").ConfigureAwait(false);
        }
    }

    private static void ResetTimer(Timer timer, double milliseconds = 1000) {
        timer.Stop();
        if (milliseconds > 0)
            timer.Interval = milliseconds;
    }

    /// <summary>
    ///     初始化新音轨
    /// </summary>
    private async Task InitializeNewTrackAsync(Stream audioStream, double replayGain) {
        Status = MediaPlaybackStatus.Changing;
        _soundDataProvider = new StreamDataProvider(AudioEngine, AudioFormat, audioStream);
        await LoggerService.DebugAsync($"Volume:{Volume},Speed:{Speed}").ConfigureAwait(false);
        _soundPlayer =
            new SoundPlayer(AudioEngine, AudioFormat, _soundDataProvider) {
                Volume = Volume, Mute = IsMute, PlaybackSpeed = Speed
            };

        // 设置播放完成事件
        _soundPlayer.PlaybackEnded += OnPlaybackCompleted;

        InitializeModifiers(_soundPlayer, replayGain);

        PlayerDevice.MasterMixer.AddComponent(_soundPlayer);

        // Spectrum
        // ReSharper disable once InvertIf
        if (ConfigManager.UiConfig.SpectrumConfig.IsEnabled) {
            _spectrumAnalyzer = new SpectrumAnalyzer(AudioFormat, ConfigManager.UiConfig.SpectrumConfig.FFTSize);

            _soundPlayer.AddAnalyzer(_spectrumAnalyzer);

            SpecTimer.Interval = TimeSpan.FromMilliseconds(ConfigManager.UiConfig.SpectrumConfig.UpdateIntervalMs);
        }

        Status = MediaPlaybackStatus.Paused;
    }

    private void OnSpectrumVisualizer(object? sender, EventArgs eventArgs) {
        if (_spectrumAnalyzer == null ||
            !ConfigManager.UiConfig.SpectrumConfig.IsEnabled ||
            !DrawerManager.Instance.IsMusicPlayerPanelVisible)
            return;

        var spectrumData = _spectrumAnalyzer.SpectrumData;

        if (spectrumData.Length <= 0)
            return;

        // 触发频谱数据更新事件
        SpectrumDataUpdated?.Invoke(this, spectrumData.ToArray());
    }

    /// <summary>
    ///     初始化效果链
    /// </summary>
    private void InitializeModifiers(SoundPlayer soundPlayer, double replayGain) {
        _soundModifier.ReplayGainModifier.Gain = (float)replayGain;
        soundPlayer.AddModifier(_soundModifier.ReplayGainModifier);

        _soundModifier.FadeModifier.Reset();
        _soundModifier.FadeModifier.SampleRate = soundPlayer.Format.SampleRate;
        soundPlayer.AddModifier(_soundModifier.FadeModifier);

        foreach (var soundModifier in SoundModifierManager.Default.SoundModifiers) {
            soundModifier.Initialize(AudioFormat);

            if (soundModifier.Modifier != null) {
                soundPlayer.AddModifier(soundModifier.Modifier);
            }
        }
    }

    /// <summary>
    ///     播放完成事件处理
    /// </summary>
    private void OnPlaybackCompleted(object? sender, EventArgs e) {
        Status = MediaPlaybackStatus.Stopped;
        UpdateTimer.Stop();
        SpecTimer.Stop();
        // 触发播放完成事件
        PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     进度定时器事件处理
    /// </summary>
    private void OnProgressTimerTick(object? sender, EventArgs e) {
        Debug.Assert(_soundPlayer is not null);
        Debug.Assert(Status is MediaPlaybackStatus.Playing or MediaPlaybackStatus.Fading);

        if (!PlayerDevice.IsRunning ||
            (PlayerDevice.Info is { } info &&
             info.Name != ConfigManager.PlayerConfig.DefaultDevice &&
             !info.IsDefault)) {
            ReloadDevice();
        }

        // 触发位置变化事件
        PositionChanged?.Invoke(this, _soundPlayer.Time);
    }

    private AudioPlaybackDevice InitializeDevice() {
        return AudioEngine.InitializePlaybackDevice(
            AudioEngine.PlaybackDevices.FirstOrDefault(
                x => x.Name == ConfigManager.PlayerConfig.DefaultDevice,
                AudioEngine.PlaybackDevices.Single(x => x.IsDefault)),
            AudioFormat,
            ConfigManager.PlayerConfig.DeviceConfig);
    }

    public void ReloadDevice() {
        bool isPlaying = Status is MediaPlaybackStatus.Playing;
        if (isPlaying) {
            Pause();
        }

        if (ConfigManager.PlayerConfig.DefaultDevice is not null) {
            PlayerDevice = AudioEngine.SwitchDevice(
                PlayerDevice,
                AudioEngine.PlaybackDevices.FirstOrDefault(
                    x => x.Name == ConfigManager.PlayerConfig.DefaultDevice,
                    AudioEngine.PlaybackDevices.Single(x => x.IsDefault)),
                ConfigManager.PlayerConfig.DeviceConfig);
        } else {
            PlayerDevice = AudioEngine.SwitchDevice(
                PlayerDevice,
                AudioEngine.PlaybackDevices.Single(x => x.IsDefault),
                ConfigManager.PlayerConfig.DeviceConfig);
        }

        if (isPlaying) {
            Play();
        }
    }
}