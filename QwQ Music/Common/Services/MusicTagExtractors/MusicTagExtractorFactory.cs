using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ATL;
using QwQ_Music.Common.Interfaces;
using QwQ_Music.Models;

namespace QwQ_Music.Common.Services.MusicTagExtractors;

public static class MusicTagExtractorFactory
{
    public static IMusicTagExtractor GetMusicTagExtractor(string filePath)
    {

        string extension = Path.GetExtension(filePath).ToUpperInvariant();

        // 判断是否为 NCM 格式
        if (extension == AudioFileValidator.AudioFormatsExtendToNameMap[AudioFileValidator.ExtendAudioFormats.Ncm])
        {
            return  new NcmMusicTagExtractor(filePath);
        }

        return  new StandardMusicTagExtractor(filePath);
    }
    

    public static MusicDetailedInfo? ExtractDetailedInfo(Track track)
    {
        return new MusicDetailedInfo(

            // 发布信息
            track.Date,
            track.OriginalReleaseDate,
            track.PublishingDate,

            // 专业信息
            track.ISRC,
            track.CatalogNumber,
            track.ProductId,

            // 其他信息
            track.BPM,
            track.Popularity,
            track.SeriesTitle,
            track.SeriesPart,
            track.LongDescription,
            track.Group,

            // 技术信息
            track.TechnicalInformation.AudioDataOffset,
            track.TechnicalInformation.AudioDataSize
        );
    }

    public static Dictionary<string, string>? ExtractAdditionalFields(Track track)
    {
        return track.AdditionalFields != null ? new Dictionary<string, string>(track.AdditionalFields) : [];
    }


    public static Track? GetTrack(string filePath)
    {
        return GetMusicTagExtractor(filePath).GetTrack();
    }

    public static Task<Track?> GetTrackAsync(string filePath)
    {
        return GetMusicTagExtractor(filePath).GetTrackAsync();
    }
}
