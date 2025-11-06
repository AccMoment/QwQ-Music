using System;
using System.IO;
using System.Threading.Tasks;
using NcmdumpCSharp.Core;
using QwQ_Music.Common.Manager;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Services.MusicTagExtractors;
using QwQ_Music.Models;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace QwQ_Music.Common.Audio;

public class AudioPreprocessor(AudioPlay audioPlay)
{
    public TimeSpan InitialTime { get; set; } = TimeSpan.Zero;

    public async Task InitializeAudioTrack(MusicItemModel musicItem)
    {
        // 根据文件类型初始化音频
        string extension = Path.GetExtension(musicItem.FilePath).ToUpper();
        await UpdateAudioFormat(musicItem);

        if (extension == AudioFileValidator.AudioFormatsExtendToNameMap[AudioFileValidator.ExtendAudioFormats.Ncm])
        {
            await InitializeNcmAudioTrackAsync(musicItem);
        }
        else
        {
            await Task.Run(() => audioPlay.InitializeAudio(musicItem.FilePath, musicItem.Gain));
        }
    }

    private async Task UpdateAudioFormat(MusicItemModel model)
    {
        var track = await MusicTagExtractorFactory.GetTrackAsync(model.FilePath);

        var format = track == null
            ? AudioFormat.DvdHq
            : new AudioFormat
            {
                SampleRate = ConfigManager.PlayerConfig.IsAutoSetSampleRate ? (int)track.SampleRate : ConfigManager.PlayerConfig.SampleRate,
                Channels = track.ChannelsArrangement.NbChannels,
                Format = SampleFormat.F32,
            };

        if (audioPlay.AudioFormat == format)
            return;

        audioPlay.AudioFormat = format;
    }

    private async Task InitializeNcmAudioTrackAsync(MusicItemModel musicItem)
    {
        using var crypt = new NeteaseCrypt(musicItem.FilePath);
        var audioStream = await crypt.DumpToStreamAsync();

        if (audioStream != null)
        {
            // 对于NCM，我们暂时不处理ReplayGain
            audioPlay.InitializeAudio(audioStream, 0);
        }
    }

    public void UpdateMusicPlayProgress(MusicItemModel musicItem, bool restart = false)
    {
        if (restart || IsNearEnd(musicItem))
        {
            musicItem.Current = TimeSpan.Zero;
        }

        if (musicItem.Current != InitialTime)
        {
            Task.Run(() => MusicItemManager.UpdatePlayProgress(musicItem.FilePath, musicItem.Current));
        }
    }

    public static bool IsNearEnd(MusicItemModel musicItem)
    {
        return Math.Abs(musicItem.Duration.TotalSeconds - musicItem.Current.TotalSeconds) < 5;
    }
}
