using Domain.Interfaces;
using Domain.Models;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Presentation.Services
{
    public sealed class RealTimeDetectionService : IRealTimeDetectionService
    {
        private readonly ICameraService _camera;
        private readonly IFrameDispatcherService _dispatcher;
        private readonly IObjectDetectionService _detector;
        private CancellationTokenSource _cts;
        private DateTime _last = DateTime.UtcNow;
        private int _frames;
        private double _currentFps;

        public int TargetFps { get; set; } = 30;
        public double CurrentFps => _currentFps;
        public bool IsDetecting { get; private set; }

        public event EventHandler<HumanDetectionResult> OnPersonDetected;
        public event EventHandler<Exception> OnDetectionError;
        public event EventHandler<DetectionStatus> OnStatusChanged;
        public event EventHandler<HumanDetectionResult> OnFrameProcessed;

        public RealTimeDetectionService(ICameraService camera, IFrameDispatcherService dispatcher, IObjectDetectionService detector)
        {
            _camera = camera;
            _dispatcher = dispatcher;
            _detector = detector;

            _dispatcher.OnFrameForAi += async (s, frame) =>
            {
                if (!IsDetecting) return;
                try
                {
                    using var copy = frame.Copy();
                    var result = await _detector.DetectAsync(copy);
                    OnPersonDetected?.Invoke(this, result);
                    OnFrameProcessed?.Invoke(this, result);
                    CountFps();
                }
                catch (Exception ex)
                {
                    OnDetectionError?.Invoke(this, ex);
                }
            };
        }

        public async Task StartRealTimeDetectionAsync(CancellationToken ct = default)
        {
            if (IsDetecting) return;
            OnStatusChanged?.Invoke(this, DetectionStatus.Starting);

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            if (!_camera.IsInitialized) await _camera.InitializeAsync(_cts.Token);
            await _detector.InitializeAsync(_cts.Token);

            _ = _dispatcher.StartFrameDispatchAsync(_camera.GetFrameStream(_cts.Token), _cts.Token);

            IsDetecting = true;
            OnStatusChanged?.Invoke(this, DetectionStatus.Running);
        }

        public async Task StopRealTimeDetectionAsync()
        {
            if (!IsDetecting) return;
            OnStatusChanged?.Invoke(this, DetectionStatus.Stopping);

            _cts?.Cancel();
            await _dispatcher.StopFrameDispatchAsync();
            await _camera.StopAsync();

            IsDetecting = false;
            _currentFps = 0;

            OnStatusChanged?.Invoke(this, DetectionStatus.Stopped);
        }

        private void CountFps()
        {
            _frames++;
            var now = DateTime.UtcNow;
            if ((now - _last).TotalSeconds >= 1)
            {
                _currentFps = _frames / (now - _last).TotalSeconds;
                _frames = 0;
                _last = now;
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _detector?.Dispose();
        }
    }
}
