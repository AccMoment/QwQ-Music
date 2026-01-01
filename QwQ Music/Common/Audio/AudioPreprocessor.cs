using System;
using System.Threading.Tasks;
using NcmdumpCSharp.Core;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using SoundFlow.Enums;
using SoundFlow.Structs;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.Common.Audio;

public class AudioPreprocessor(AudioPlayer audioPlayer) {
    public TimeSpan InitialTime { get; set; } = TimeSpan.Zero;

    public async Task InitializeAudioTrackAsync(MusicItemModel musicItem) {
        await LoggerService.InfoAsync($"正在初始化《{musicItem.Title}》的音频流...").ConfigureAwait(false);
        // 根据文件类型初始化音频

        UpdateAudioFormat(musicItem);

        if (musicItem.Extension ==
            AudioFileValidator.AudioFormatsExtendToNameMap[AudioFileValidator.ExtendAudioFormats.Ncm]) {
            await InitializeNcmAudioTrackAsync(musicItem).ConfigureAwait(false);
        } else {
            await audioPlayer.InitializeAudioAsync(musicItem.Data, musicItem.Gain).ConfigureAwait(false);
        }

        await LoggerService.InfoAsync($"《{musicItem.Title}》音频流初始化完毕。").ConfigureAwait(false);
    }

    private void UpdateAudioFormat(MusicItemModel model) {
        var format = new AudioFormat {
            SampleRate =
                ConfigManager.PlayerConfig.IsAutoReSample ?
                    ConfigManager.PlayerConfig.SampleRate :
                    (int)model.SampleRate,
            Channels = model.Channels,
            // Layout = ChannelLayout.Stereo,//TODO
            Format = SampleFormat.F32
        };
        LoggerService.Info($"已更新音频格式。旧格式：{audioPlayer.AudioFormat}，新格式：{format}。");
        audioPlayer.AudioFormat = format;
    }

    private async Task InitializeNcmAudioTrackAsync(MusicItemModel musicItem) {
        using var crypt = new NeteaseCrypt(musicItem.FilePath);

        if (await crypt.DumpToStreamAsync().ConfigureAwait(false) is { } audioStream) {
            // 对于NCM，我们暂时不处理ReplayGain
            await audioPlayer.InitializeAudioAsync(audioStream, 0).ConfigureAwait(false);
        }
    }

    public void UpdateMusicPlayProgress(MusicItemModel musicItem, bool restart = false) {
        if (restart || IsEnded(musicItem)) {
            musicItem.Record = TimeSpan.Zero;
        }

        if (musicItem.Record != InitialTime) {
            MusicItemsManager.UpdatePlayProgress(musicItem, musicItem.Record);
        }
    }

    private static bool IsEnded(MusicItemModel musicItem) {
        return Math.Abs(musicItem.Duration.TotalSeconds - musicItem.Record.TotalSeconds) < 2;
    }
}