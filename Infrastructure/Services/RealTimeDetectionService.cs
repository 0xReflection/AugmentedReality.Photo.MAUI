using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public sealed class RealTimeDetectionService : IRealTimeDetectionService, IDisposable
    {
        private readonly IFrameDispatcherService _frameDispatcher;
        private readonly IObjectDetectionService _detectionService;
        private readonly ILogger<RealTimeDetectionService> _logger;

        private CancellationTokenSource _cts;
        private bool _disposed;
        private HumanDetectionResult _lastDetectionResult;
        private Stopwatch _fpsStopwatch;
        private int _frameCount;
        private DateTime _lastFpsUpdate;
        private double _currentFps;
        private int _targetFps = 15;

        public event EventHandler<HumanDetectionResult> OnPersonDetected;
        public event EventHandler<string> OnDetectionError;
        public event EventHandler<string> OnStatusChanged;

        public bool IsDetecting { get; private set; }
        public double CurrentFps => _currentFps;
        public int TargetFps
        {
            get => _targetFps;
            set
            {
                if (value >= 1 && value <= 60)
                {
                    _targetFps = value;
                    _logger.LogInformation($"Target FPS set to: {value}");
                }
            }
        }
        public HumanDetectionResult LastDetectionResult => _lastDetectionResult;

        public RealTimeDetectionService(IFrameDispatcherService frameDispatcher, IObjectDetectionService detectionService, ILogger<RealTimeDetectionService> logger)
        {
            _frameDispatcher = frameDispatcher;
            _detectionService = detectionService;
            _logger = logger;

            _fpsStopwatch = new Stopwatch();
            _lastFpsUpdate = DateTime.Now;

            // Подписка на кадры для AI-потока
            _frameDispatcher.OnFrameForAi += OnFrameForAiReceived;
        }

        // AI-поток: TensorFlow Lite inference
        private async void OnFrameForAiReceived(object sender, SKBitmap frame)
        {
            if (!IsDetecting || _cts.Token.IsCancellationRequested)
            {
                frame.Dispose();
                return;
            }

            using (frame)
            {
                try
                {
                    var result = await _detectionService.DetectPersonAsync(frame, _cts.Token);
                    _lastDetectionResult = result;
                    OnPersonDetected?.Invoke(this, result);
                }
                catch (Exception ex)
                {
                    OnDetectionError?.Invoke(this, ex.Message);
                }
            }

            _frameCount++;
        }

        // Автостарт сканирования при запуске
        public async Task StartRealTimeDetectionAsync(CancellationToken ct = default)
        {
            if (IsDetecting) return;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            IsDetecting = true;
            _frameCount = 0;

            // сразу запускаем FPS-мониторинг
            _ = Task.Run(() => MonitorFpsAsync(_cts.Token), _cts.Token);

            // подписка на кадры включена в конструкторе, AI поток уже готов
        }

        // Поток FPS для UI
        private async Task MonitorFpsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && IsDetecting)
            {
                await Task.Delay(1000, ct);

                var now = DateTime.Now;
                var elapsed = (now - _lastFpsUpdate).TotalSeconds;
                if (elapsed >= 1.0)
                {
                    _currentFps = _frameCount / elapsed;
                    _frameCount = 0;
                    _lastFpsUpdate = now;
                }
            }
        }

        public async Task StopRealTimeDetectionAsync()
        {
            if (!IsDetecting) return;

            _cts.Cancel();
            IsDetecting = false;

            await Task.CompletedTask;
            OnStatusChanged?.Invoke(this, "Detection stopped");
        }

        public void Dispose()
        {
            if (_disposed) return;

            _frameDispatcher.OnFrameForAi -= OnFrameForAiReceived;
            _cts?.Cancel();
            _disposed = true;
        }
    }
}
