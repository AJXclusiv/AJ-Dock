using AJDock.App.Services;

namespace AJDock.App.ViewModels;

public sealed class AudioSessionViewModel : ObservableObject
{
    private readonly AudioVolumeService _audioVolumeService;
    private double _volumePercent;
    private bool _isMuted;

    public AudioSessionViewModel(AudioSessionInfo session, AudioVolumeService audioVolumeService)
    {
        _audioVolumeService = audioVolumeService;
        Id = session.Id;
        ProcessId = session.ProcessId;
        DisplayName = session.DisplayName;
        IsActive = session.IsActive;
        _volumePercent = session.VolumePercent;
        _isMuted = session.IsMuted;
    }

    public string Id { get; }
    public int ProcessId { get; }
    public string DisplayName { get; }
    public bool IsActive { get; }

    public double VolumePercent
    {
        get => _volumePercent;
        set
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (Math.Abs(_volumePercent - normalized) < 0.1)
            {
                return;
            }

            SetProperty(ref _volumePercent, normalized);
            _audioVolumeService.SetSessionVolume(Id, normalized);
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetProperty(ref _isMuted, value))
            {
                _audioVolumeService.SetSessionMute(Id, value);
            }
        }
    }
}
