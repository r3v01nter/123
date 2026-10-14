namespace WindowsIsland.Core.Media;

public sealed class MediaTrackInfo
{
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string AlbumTitle { get; init; } = string.Empty;
    public string SourceAppId { get; init; } = string.Empty;
    public byte[]? ThumbnailBytes { get; init; }
    public bool IsPlaying { get; init; }
    public bool CanPlay { get; init; }
    public bool CanPause { get; init; }
    public bool CanSkipNext { get; init; }
    public bool CanSkipPrevious { get; init; }
    public TimeSpan Position { get; init; }
    public TimeSpan EndTime { get; init; }

    /// <summary>
    /// 用于封面流防抖比对的唯一曲目指纹
    /// </summary>
    public string TrackKey => $"{SourceAppId}|{Title}|{Artist}|{AlbumTitle}";
}
