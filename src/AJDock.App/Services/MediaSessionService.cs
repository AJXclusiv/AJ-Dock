using Windows.Media.Control;

namespace AJDock.App.Services;

public sealed class MediaSessionService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public async Task<bool> IsAnyMediaPlayingAsync()
    {
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var currentSession = _manager.GetCurrentSession();
            if (currentSession is not null && IsPlaying(currentSession))
            {
                return true;
            }

            return _manager.GetSessions().Any(IsPlaying);
        }
        catch (Exception exception)
        {
            AppLog.Write("Could not read Windows media session playback state.", exception);
            return false;
        }
    }

    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession session)
    {
        return session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
    }
}
