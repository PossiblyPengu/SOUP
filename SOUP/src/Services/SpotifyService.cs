using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Serilog;

namespace SOUP.Services;

/// <summary>
/// Service for controlling Spotify playback using Windows Media Session API for metadata
/// and global media keys for control.
/// </summary>
public class SpotifyService : INotifyPropertyChanged
{
    private static SpotifyService? _instance;
    public static SpotifyService Instance => _instance ??= new SpotifyService();

    // Windows API for sending key events
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    // Virtual key codes for media keys
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private string _trackTitle = "Not Playing";
    private string _artistName = "";
    private bool _isPlaying;
    private bool _hasMedia;
    private BitmapImage? _albumArt;
    private readonly System.Timers.Timer _pollTimer;
    private DateTime _lastUserAction = DateTime.MinValue;
    private const int UserActionCooldownMs = 3000;
    
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;

    public string TrackTitle
    {
        get => _trackTitle;
        private set { if (_trackTitle != value) { _trackTitle = value; OnPropertyChanged(); } }
    }

    public string ArtistName
    {
        get => _artistName;
        private set { if (_artistName != value) { _artistName = value; OnPropertyChanged(); } }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set { if (_isPlaying != value) { _isPlaying = value; OnPropertyChanged(); } }
    }

    public bool HasMedia
    {
        get => _hasMedia;
        private set { if (_hasMedia != value) { _hasMedia = value; OnPropertyChanged(); } }
    }

    public BitmapImage? AlbumArt
    {
        get => _albumArt;
        private set { _albumArt = value; OnPropertyChanged(); }
    }

    private SpotifyService()
    {
        _pollTimer = new System.Timers.Timer(2000);
        _pollTimer.Elapsed += (s, e) => _ = PollMediaSessionAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
            _sessionManager.SessionsChanged += OnSessionsChanged;
            
            await UpdateCurrentSessionAsync();
            
            if (!_pollTimer.Enabled)
            {
                _pollTimer.Start();
            }
            
            Log.Information("SpotifyService initialized with Windows Media Session API");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize Windows Media Session API, falling back to window polling");
            if (!_pollTimer.Enabled)
            {
                _pollTimer.Start();
            }
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        _ = UpdateCurrentSessionAsync();
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        _ = UpdateCurrentSessionAsync();
    }

    private async Task UpdateCurrentSessionAsync()
    {
        try
        {
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }

            // Try to find Spotify session first
            var sessions = _sessionManager?.GetSessions();
            _currentSession = null;
            
            if (sessions != null)
            {
                foreach (var session in sessions)
                {
                    if (session.SourceAppUserModelId?.Contains("Spotify", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        _currentSession = session;
                        break;
                    }
                }
                
                // If no Spotify, use current session
                _currentSession ??= _sessionManager?.GetCurrentSession();
            }

            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                await UpdateMediaInfoAsync();
            }
            else
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    HasMedia = false;
                    TrackTitle = "No media playing";
                    ArtistName = "";
                    IsPlaying = false;
                    AlbumArt = null;
                });
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update current session");
        }
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = UpdateMediaInfoAsync();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = UpdateMediaInfoAsync();
    }

    private async Task PollMediaSessionAsync()
    {
        if ((DateTime.Now - _lastUserAction).TotalMilliseconds < UserActionCooldownMs)
            return;

        await UpdateMediaInfoAsync();
    }

    private async Task UpdateMediaInfoAsync()
    {
        if (_currentSession == null)
        {
            await UpdateCurrentSessionAsync();
            return;
        }

        try
        {
            var mediaProperties = await _currentSession.TryGetMediaPropertiesAsync();
            var playbackInfo = _currentSession.GetPlaybackInfo();

            string? title = mediaProperties?.Title;
            string? artist = mediaProperties?.Artist;
            bool isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            // Get album art
            BitmapImage? albumArt = null;
            if (mediaProperties?.Thumbnail != null)
            {
                try
                {
                    using var stream = await mediaProperties.Thumbnail.OpenReadAsync();
                    albumArt = await ConvertToBitmapImageAsync(stream);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to load album art thumbnail");
                }
            }

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (!string.IsNullOrEmpty(title))
                {
                    HasMedia = true;
                    TrackTitle = title;
                    ArtistName = artist ?? "";
                    IsPlaying = isPlaying;
                    if (albumArt != null)
                    {
                        AlbumArt = albumArt;
                    }
                }
                else
                {
                    HasMedia = false;
                    TrackTitle = "No media playing";
                    ArtistName = "";
                    IsPlaying = false;
                }
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update media info");
        }
    }

    private async Task<BitmapImage?> ConvertToBitmapImageAsync(IRandomAccessStreamWithContentType stream)
    {
        try
        {
            var memoryStream = new MemoryStream();
            var inputStream = stream.AsStreamForRead();
            await inputStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            BitmapImage? bitmap = null;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();
                bitmap.Freeze();
            });
            
            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to convert stream to BitmapImage");
            return null;
        }
    }

    public Task PlayPauseAsync()
    {
        _lastUserAction = DateTime.Now;
        SendMediaKey(VK_MEDIA_PLAY_PAUSE);
        IsPlaying = !IsPlaying;
        return Task.CompletedTask;
    }

    public Task NextTrackAsync()
    {
        _lastUserAction = DateTime.Now;
        SendMediaKey(VK_MEDIA_NEXT_TRACK);
        return Task.CompletedTask;
    }

    public Task PreviousTrackAsync()
    {
        _lastUserAction = DateTime.Now;
        SendMediaKey(VK_MEDIA_PREV_TRACK);
        return Task.CompletedTask;
    }

    private void SendMediaKey(byte keyCode)
    {
        try
        {
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to send media key");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
