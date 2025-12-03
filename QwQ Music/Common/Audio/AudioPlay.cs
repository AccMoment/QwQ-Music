using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using QwQ_Music.Common.Interfaces;
using QwQ_Music.Common.Manager;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.ViewModels;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;
using SoundFlow.Visualization;
using Timer = System.Timers.Timer;

namespace QwQ_Music.Common.Audio;

/// <summary>
///     基于SoundFlow实现的音频播放器
/// </summary>
public class AudioPlay : IAudioPlay {
    private readonly PlayComponent _soundModifier = ConfigManager.SoundModifierConfig.PlayComponent;
    private static readonly MiniAudioEngine AudioEngine = Task.Run(() => new MiniAudioEngine()).Result;
    public bool IsDisposed { get; private set; }

    private AudioPlaybackDevice PlayerDevice {
        get {
            if (field?.IsDisposed ?? true) {
                field = AudioEngine.InitializePlaybackDevice(
                    AudioEngine.PlaybackDevices.FirstOrDefault(x => x.IsDefault),
                    AudioFormat,
                    ConfigManager.PlayerConfig.DeviceConfig);
            }

            return field;
        }
    }


    private Timer FadeOutTimer {
        get {
            // ReSharper disable once InvertIf
            if (field is null) {
                field = new Timer();
                field.Elapsed += FadeOutAwaiter;
            }

            return field;
        }
    }

// 添加一个字段来跟踪当前的淡出定时器
    private DispatcherTimer ProgressTimer =>
        field ??= new DispatcherTimer(TimeSpan.FromMilliseconds(1000), DispatcherPriority.Render, OnProgressTimerTick);

    private StreamDataProvider? _soundDataProvider;
    private SoundPlayer? _soundPlayer;
    private SpectrumAnalyzer? _spectrumAnalyzer;

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
        get {
            if (_soundPlayer != null)
                return _soundPlayer.Time;

            return -1;
        }
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
            field = Math.Clamp(value, 0.0f, 1.0f);

            _soundPlayer?.Volume = field;
        }
    } = 1.0f;

    /// <inheritdoc />
    public float Speed {
        get;
        set {
            if (value <= 0f)
                return;

            field = value;

            _soundPlayer?.PlaybackSpeed = field;
        }
    } = 1.0f;

    /// <summary>
    ///     开始播放
    /// </summary>
    public void Play() {
        if (_soundPlayer == null)
            return;

        // 检查淡入效果器是否启用
        if (_soundModifier.FadeModifier.Enabled) {
            // 应用淡入效果
            ResetTimer(FadeOutTimer, -1);
            _soundModifier.FadeModifier.BeginFadeIn();
        }

        PlayerDevice.Start();
        _soundPlayer.Play();
        SpecTimer.Start();

        ProgressTimer.Start();
    }

    /// <summary>
    ///     暂停播放
    /// </summary>
    public void Pause() {
        if (_soundPlayer is not { State: PlaybackState.Playing })
            return;

        // 检查淡出效果器是否启用
        if (_soundModifier.FadeModifier.Enabled) {
            // 应用淡出效果
            _soundModifier.FadeModifier.BeginFadeOut();

            // 重置淡出计时器，等待淡出效果完成
            ResetTimer(FadeOutTimer, _soundModifier.FadeModifier.FadeOutTimeMs);
            FadeOutTimer.Start();
        } else {
            // 直接暂停
            _soundPlayer.Pause();
            ProgressTimer.Stop();
            SpecTimer.Stop();
        }
    }

    /// <summary>
    ///     停止播放并释放资源
    /// </summary>
    public void Stop() {
        ProgressTimer.Stop();
        SpecTimer.Stop();
        PlayerDevice.Stop();
        _soundDataProvider?.Dispose();
        if (_soundPlayer is null)
            return;
        PlayerDevice.MasterMixer.RemoveComponent(_soundPlayer);
        _soundPlayer.Stop();
    }

    /// <summary>
    ///     跳转到指定位置（单位：秒）
    /// </summary>
    public void Seek(double positionInSeconds) {
        _soundPlayer?.Seek((float)Math.Clamp(positionInSeconds, 0, _soundPlayer.Duration));
    }

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
    public void InitializeAudio(string filePath, double replayGain) {
        InitializeAudio(File.OpenRead(filePath), replayGain);
    }

    /// <summary>
    ///     频谱数据更新事件
    /// </summary>
    public event EventHandler<float[]>? SpectrumDataUpdated;

    /// <summary>
    ///     淡出定时器事件处理
    /// </summary>
    private void FadeOutAwaiter(object? sender, EventArgs e) {
        // 实际执行暂停操作
        if (_soundPlayer is not { State: PlaybackState.Playing })
            return;
        _soundPlayer.Pause();
        ProgressTimer.Stop();
        SpecTimer.Stop();
    }

    public void InitializeAudio(Stream audioStream, double replayGain) {
        Stop();
        try {
            InitializeNewTrack(audioStream, replayGain);
        } catch (Exception ex) {
            Console.WriteLine($"初始化音轨失败: {ex}");
        }
    }

    private static void ResetTimer(Timer timer, double milliseconds) {
        timer.Stop();
        if (milliseconds > 0)
            timer.Interval = milliseconds;
    }

    /// <summary>
    ///     初始化新音轨
    /// </summary>
    private void InitializeNewTrack(Stream audioStream, double replayGain) {
        _soundDataProvider = new StreamDataProvider(AudioEngine, AudioFormat, audioStream);

        _soundPlayer =
            new SoundPlayer(AudioEngine, AudioFormat, _soundDataProvider) {
                Volume = Volume, Mute = IsMute, PlaybackSpeed = Speed,
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
    }

    private void OnSpectrumVisualizer(object? sender, EventArgs eventArgs) {
        if (_spectrumAnalyzer == null || !ConfigManager.UiConfig.SpectrumConfig.IsEnabled || !DrawerStatusViewModel.Default.IsMusicPlayerPanelVisible)
            return;

        var spectrumData = _spectrumAnalyzer.SpectrumData;

        if (spectrumData.Length <= 0)
            return;

        // 触发频谱数据更新事件
        SpectrumDataUpdated?.Invoke(this, spectrumData.ToArray());

/*
#if DEBUG
        // 调试输出（可选）
        Console.Write("Spectrum: ");

        for (int i = 0; i < Math.Min(10, spectrumData.Length); i++)
        {
            Console.Write($"{spectrumData[i]:F2} ");
        }

        Console.WriteLine();
#endif
*/
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
        // 触发播放完成事件
        PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     进度定时器事件处理
    /// </summary>
    private void OnProgressTimerTick(object? sender, EventArgs e) {
        if (_soundPlayer is not { State: PlaybackState.Playing })
            return;

        // 触发位置变化事件
        PositionChanged?.Invoke(this, _soundPlayer.Time);
    }
}