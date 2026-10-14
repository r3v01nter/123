using System.Runtime.InteropServices;
using Windows.Media.Control;

namespace WindowsIsland.Core.Media;

public sealed class MediaSessionService : IMediaSessionService
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly object _debounceSync = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private CancellationTokenSource? _debounceCts;
    private string _lastTrackKey = string.Empty;
    private byte[]? _cachedThumbnail;
    private bool _initialized;
    private bool _disposed;

    public event EventHandler<MediaTrackInfo?>? MediaStateChanged;

    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) return;

        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        ObjectDisposedException.ThrowIf(_disposed, this);

        manager.CurrentSessionChanged += OnSessionsChanged;
        manager.SessionsChanged += OnSessionsChanged;
        _manager = manager;
        _initialized = true;

        await BindAndRefreshAsync();
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, object args)
        => ScheduleDebouncedRefresh();

    private void OnSessionStateChanged(GlobalSystemMediaTransportControlsSession sender, object args)
        => ScheduleDebouncedRefresh();

    private void ScheduleDebouncedRefresh()
    {
        CancellationTokenSource cts;
        CancellationToken token;

        lock (_debounceSync)
        {
            if (_disposed) return;

            cts = new CancellationTokenSource();
            token = cts.Token;

            var previous = _debounceCts;
            _debounceCts = cts;
            previous?.Cancel();
        }

        _ = RunDebouncedRefreshAsync(cts, token);
    }

    private async Task RunDebouncedRefreshAsync(CancellationTokenSource cts, CancellationToken token)
    {
        try
        {
            await Task.Delay(100, token);
            await BindAndRefreshAsync();
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) when (_disposed) { }
        finally
        {
            lock (_debounceSync)
            {
                if (ReferenceEquals(_debounceCts, cts))
                    _debounceCts = null;
            }

            cts.Dispose();
        }
    }

    public Task RefreshCurrentSessionAsync() => BindAndRefreshAsync();

    private async Task BindAndRefreshAsync()
    {
        if (_disposed || _manager is null) return;

        await _lock.WaitAsync();
        try
        {
            if (_disposed || _manager is null) return;

            var target = SelectBestSession(_manager);
            if (!ReferenceEquals(_currentSession, target))
            {
                DetachCurrentSession();
                _currentSession = target;
                if (_currentSession is not null)
                {
                    _currentSession.MediaPropertiesChanged += OnSessionStateChanged;
                    _currentSession.PlaybackInfoChanged += OnSessionStateChanged;
                    _currentSession.TimelinePropertiesChanged += OnSessionStateChanged;
                }
            }

            if (_currentSession is null)
            {
                _lastTrackKey = string.Empty;
                _cachedThumbnail = null;
                MediaStateChanged?.Invoke(this, null);
                return;
            }

            var info = await ReadTrackInfoSafeAsync(_currentSession);
            if (!_disposed)
                MediaStateChanged?.Invoke(this, info);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static GlobalSystemMediaTransportControlsSession? SelectBestSession(
        GlobalSystemMediaTransportControlsSessionManager manager)
    {
        try
        {
            foreach (var session in manager.GetSessions())
            {
                if (session.GetPlaybackInfo()?.PlaybackStatus ==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    return session;
            }

            return manager.GetCurrentSession();
        }
        catch (COMException)
        {
            return null;
        }
    }

    private async Task<MediaTrackInfo?> ReadTrackInfoSafeAsync(
        GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            if (props is null || playback is null) return null;

            string appId = session.SourceAppUserModelId ?? string.Empty;
            string title = props.Title ?? string.Empty;
            string artist = props.Artist ?? string.Empty;
            string album = props.AlbumTitle ?? string.Empty;
            string key = $"{appId}|{title}|{artist}|{album}";

            if (key != _lastTrackKey || _cachedThumbnail is null)
            {
                _lastTrackKey = key;
                _cachedThumbnail = await ReadThumbnailBytesAsync(props.Thumbnail);
            }

            var controls = playback.Controls;
            return new MediaTrackInfo
            {
                Title = title,
                Artist = artist,
                AlbumTitle = album,
                SourceAppId = appId,
                ThumbnailBytes = _cachedThumbnail,
                IsPlaying = playback.PlaybackStatus ==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                CanPlay = controls?.IsPlayEnabled ?? false,
                CanPause = controls?.IsPauseEnabled ?? false,
                CanSkipNext = controls?.IsNextEnabled ?? false,
                CanSkipPrevious = controls?.IsPreviousEnabled ?? false,
                Position = timeline?.Position ?? TimeSpan.Zero,
                EndTime = timeline?.EndTime ?? TimeSpan.Zero
            };
        }
        catch (Exception ex) when (ex is COMException or ObjectDisposedException)
        {
            return null;
        }
    }

    private static async Task<byte[]?> ReadThumbnailBytesAsync(
        Windows.Storage.Streams.IRandomAccessStreamReference? thumbRef)
    {
        if (thumbRef is null) return null;

        try
        {
            using var winrtStream = await thumbRef.OpenReadAsync();
            using var netStream = winrtStream.AsStreamForRead();
            using var memoryStream = new MemoryStream();
            await netStream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
        catch (Exception ex) when (ex is COMException or IOException or ObjectDisposedException)
        {
            return null;
        }
    }

    public Task<bool> TogglePlayPauseAsync() =>
        ExecuteControlAsync(session => session.TryTogglePlayPauseAsync().AsTask());

    public Task<bool> SkipNextAsync() =>
        ExecuteControlAsync(session => session.TrySkipNextAsync().AsTask());

    public Task<bool> SkipPreviousAsync() =>
        ExecuteControlAsync(session => session.TrySkipPreviousAsync().AsTask());

    private async Task<bool> ExecuteControlAsync(
        Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action)
    {
        var session = _currentSession;
        if (_disposed || session is null) return false;

        try
        {
            return await action(session);
        }
        catch (Exception ex) when (ex is COMException or ObjectDisposedException)
        {
            return false;
        }
    }

    private void DetachCurrentSession()
    {
        if (_currentSession is null) return;

        try
        {
            _currentSession.MediaPropertiesChanged -= OnSessionStateChanged;
            _currentSession.PlaybackInfoChanged -= OnSessionStateChanged;
            _currentSession.TimelinePropertiesChanged -= OnSessionStateChanged;
        }
        catch (COMException) { }
        finally
        {
            _currentSession = null;
        }
    }

    public void Dispose()
    {
        lock (_debounceSync)
        {
            if (_disposed) return;
            _disposed = true;
            _debounceCts?.Cancel();
        }

        if (_manager is not null)
        {
            try
            {
                _manager.CurrentSessionChanged -= OnSessionsChanged;
                _manager.SessionsChanged -= OnSessionsChanged;
            }
            catch (COMException) { }

            _manager = null;
        }

        DetachCurrentSession();

        // Do not dispose the semaphore here: an in-flight refresh may still need to release it.
    }
}
