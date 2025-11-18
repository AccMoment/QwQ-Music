using System.Threading.Tasks;
using ATL;
using QwQ_Music.Common.Interfaces;

namespace QwQ_Music.Common.Services.MusicTagExtractors;

public class StandardMusicTagExtractor(string filePath) : IMusicTagExtractor
{
    public Track? GetTrack()
    {
        return new Track(filePath);
    }

    public Task<Track?> GetTrackAsync()
    {
        return Task.Run(GetTrack);
    }
}
