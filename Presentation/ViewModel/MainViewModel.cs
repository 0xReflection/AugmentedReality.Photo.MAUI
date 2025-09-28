using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Interfaces;
using SkiaSharp;

namespace Presentation.ViewModel;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IRealTimeDetectionService _detectionService;
    private readonly ICameraService _cameraService;
    private CancellationTokenSource _cts;
    private SKBitmap _latestFrame;
    private DateTime _lastUiUpdate = DateTime.UtcNow;

    [ObservableProperty] private string status = "Stopped";
    [ObservableProperty] private double fps;
    [ObservableProperty] private bool personDetected;
    [ObservableProperty] private double confidence;

    public SKBitmap LatestFrame => _latestFrame;

    public MainViewModel(IRealTimeDetectionService detectionService, ICameraService cameraService)
    {
        _detectionService = detectionService;
        _cameraService = cameraService;

        _detectionService.OnStatusChanged += (s, st) => Status = st.ToString();
        _detectionService.OnDetectionError += (s, ex) => Status = $"Error: {ex.Message}";
        _detectionService.OnPersonDetected += (s, result) =>
        {
            PersonDetected = result.IsDetected;
            Confidence = result.Confidence;
        };
    }

    [RelayCommand]
    public async Task StartCameraAsync()
    {
        if (_detectionService.IsDetecting) return;

        _cts = new CancellationTokenSource();
        await _detectionService.StartRealTimeDetectionAsync(_cts.Token);

        _ = Task.Run(async () =>
        {
            await foreach (var frame in _cameraService.GetFrameStream(_cts.Token))
            {
                if (_cts.Token.IsCancellationRequested) break;

                var now = DateTime.UtcNow;
                if ((now - _lastUiUpdate).TotalMilliseconds < 30) continue;
                _lastUiUpdate = now;

                _latestFrame?.Dispose();
                _latestFrame = frame.Copy();
                Fps = _detectionService.CurrentFps;
            }
        }, _cts.Token);
    }

    [RelayCommand]
    public async Task StopCameraAsync()
    {
        _cts?.Cancel();
        if (_detectionService.IsDetecting)
            await _detectionService.StopRealTimeDetectionAsync();

        _latestFrame?.Dispose();
        _latestFrame = null;
    }

    [RelayCommand]
    public async Task SavePhotoAsync()
    {
        if (_latestFrame == null) return;

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            $"photo_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg");

        using var img = SKImage.FromBitmap(_latestFrame);
        using var data = img.Encode(SKEncodedImageFormat.Jpeg, 90);
        await using var fs = File.OpenWrite(path);
        data.SaveTo(fs);

#if ANDROID
        var mediaScanIntent = new Android.Content.Intent(Android.Content.Intent.ActionMediaScannerScanFile);
        var contentUri = Android.Net.Uri.FromFile(new Java.IO.File(path));
        mediaScanIntent.SetData(contentUri);
        Android.App.Application.Context.SendBroadcast(mediaScanIntent);
#endif
    }
}
