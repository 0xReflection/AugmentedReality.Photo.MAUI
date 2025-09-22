using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Diagnostics;

namespace Infrastructure.Services
{
    public sealed class RealTimeDetectionService : IRealTimeDetectionService
    {
        private readonly IFrameDispatcherService _frameDispatcher;
        private readonly IObjectDetectionService _detectionService;
        private readonly ILogger<RealTimeDetectionService> _logger;

        private bool _disposed;
        private HumanDetectionResult _lastDetectionResult;
        private CancellationTokenSource _processingCts;
        private readonly Stopwatch _fpsStopwatch;
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

        public RealTimeDetectionService(
            IFrameDispatcherService frameDispatcher,
            IObjectDetectionService detectionService,
            ILogger<RealTimeDetectionService> logger)
        {
            _frameDispatcher = frameDispatcher;
            _detectionService = detectionService;
            _logger = logger;

            _fpsStopwatch = new Stopwatch();
            _lastFpsUpdate = DateTime.Now;

            _frameDispatcher.OnFrameForAi += OnFrameForAiReceived;
        }

        public async Task StartRealTimeDetectionAsync(CancellationToken ct = default)
        {
            if (IsDetecting)
            {
                _logger.LogWarning("Detection is already running");
                return;
            }

            try
            {
                _processingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                IsDetecting = true;
                _frameCount = 0;
                _currentFps = 0;

                OnStatusChanged?.Invoke(this, $"Запуск детекции ({TargetFps} FPS)...");
                _logger.LogInformation($"Starting real-time detection with target FPS: {TargetFps}");

                _ = Task.Run(() => MonitorFpsAsync(_processingCts.Token), _processingCts.Token);

                OnStatusChanged?.Invoke(this, "Детекция активна");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start detection");
                OnDetectionError?.Invoke(this, $"Ошибка запуска: {ex.Message}");
                IsDetecting = false;
                throw;
            }
        }

        private async void OnFrameForAiReceived(object sender, SKBitmap frame)
        {
            if (!IsDetecting || _processingCts.Token.IsCancellationRequested)
            {
                frame?.Dispose();
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                using (frame)
                {
                    if (ShouldSkipFrame())
                        return;

                    var result = await _detectionService.DetectPersonAsync(frame, _processingCts.Token);
                    _lastDetectionResult = result;

                    OnPersonDetected?.Invoke(this, result);

                    var processingTime = stopwatch.ElapsedMilliseconds;
                    if (processingTime > 1000 / TargetFps)
                        _logger.LogWarning($"Slow processing: {processingTime}ms (target: {1000 / TargetFps}ms)");
                }
            }
            catch (OperationCanceledException)
            {
                // Игнорируем
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AI frame");
                OnDetectionError?.Invoke(this, $"Ошибка обработки: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                _frameCount++;
            }
        }

        private bool ShouldSkipFrame()
        {
            if (_currentFps > TargetFps * 1.2)
                return _frameCount % 2 == 0;

            return false;
        }

        private async Task MonitorFpsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && IsDetecting)
            {
                try
                {
                    await Task.Delay(1000, ct);

                    var now = DateTime.Now;
                    var elapsedSeconds = (now - _lastFpsUpdate).TotalSeconds;

                    if (elapsedSeconds >= 1.0)
                    {
                        _currentFps = _frameCount / elapsedSeconds;
                        _frameCount = 0;
                        _lastFpsUpdate = now;

                        _logger.LogDebug($"Current FPS: {_currentFps:F1}, Target: {TargetFps}");

                        if (_currentFps < TargetFps * 0.7)
                            OnStatusChanged?.Invoke(this, $"Низкий FPS: {_currentFps:F1}/s");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in FPS monitoring");
                }
            }
        }

        public async Task StopRealTimeDetectionAsync()
        {
            if (!IsDetecting) return;

            try
            {
                _processingCts.Cancel();
                IsDetecting = false;

                _logger.LogInformation("Detection stopped");
                OnStatusChanged?.Invoke(this, "Детекция остановлена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping detection");
                OnDetectionError?.Invoke(this, $"Ошибка остановки: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _frameDispatcher.OnFrameForAi -= OnFrameForAiReceived;
                _processingCts?.Cancel();
                _processingCts?.Dispose();

                if (IsDetecting)
                {
                    StopRealTimeDetectionAsync().Wait(3000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
