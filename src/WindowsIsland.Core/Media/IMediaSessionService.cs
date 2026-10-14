namespace WindowsIsland.Core.Media;

public interface IMediaSessionService : IDisposable
{
    event EventHandler<MediaTrackInfo?>? MediaStateChanged;
    Task InitializeAsync();
    Task RefreshCurrentSessionAsync();
    Task<bool> TogglePlayPauseAsync();
    Task<bool> SkipNextAsync();
    Task<bool> SkipPreviousAsync();
}
