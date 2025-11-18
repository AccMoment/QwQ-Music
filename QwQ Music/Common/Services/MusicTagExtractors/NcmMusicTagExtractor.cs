using System.IO;
using System.Threading.Tasks;
using ATL;
using NcmdumpCSharp.Core;
using QwQ_Music.Common.Interfaces;

namespace QwQ_Music.Common.Services.MusicTagExtractors;

public class NcmMusicTagExtractor(string filePath) : IMusicTagExtractor
{
    public Track? GetTrack()
    {
        using var crypt = new NeteaseCrypt(filePath);
        using var audioStream = crypt.DumpToStream();

        return CreateTrackFromCrypt(crypt, audioStream);
    }

    public async Task<Track?> GetTrackAsync()
    {
        using var crypt = new NeteaseCrypt(filePath);
        using var audioStream = await crypt.DumpToStreamAsync();

        return CreateTrackFromCrypt(crypt, audioStream);
    }

    private Track? CreateTrackFromCrypt(NeteaseCrypt crypt, Stream? audioStream)
    {
        if (audioStream == null)
        {
            LoggerService.Error($"无法提取音频文件流\n 文件路径: {filePath}");

            return null;
        }

        if (crypt.Metadata == null)
        {
            LoggerService.Error($"Ncm音频元数据为 null\n 文件路径: {filePath}");

            return null;
        }

        var metadata = crypt.Metadata;

        var track = new Track(audioStream)
        {
            Title = metadata.Name,
            Artist = metadata.Artist,
            Album = metadata.Album,
        };

        if (crypt.ImageData != null)
        {
            track.EmbeddedPictures.Add(PictureInfo.fromBinaryData(crypt.ImageData));
        }

        return track;
    }
}
