using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Presentation.ViewModel
{
    public partial class MainViewModel : ObservableObject, IAsyncDisposable
    {
        private readonly IRealTimeDetectionService _detectionService;
        private readonly ICameraService _cameraService;
        private readonly IFrameDispatcherService _frameDispatcher;
        private readonly ILogger<MainViewModel> _logger;
        private bool _disposed;
        private DateTime _lastUpdateTime;
        private bool _isFrameUpdating;

        [ObservableProperty] private bool _isDetecting;
        [ObservableProperty] private string _detectionStatus = "Готов к работе";
        [ObservableProperty] private HumanDetectionResult? _detectionResult;
        [ObservableProperty] private float _detectionConfidence;
        [ObservableProperty] private double _currentFps;
        [ObservableProperty] private int _targetFps = 15;
        [ObservableProperty] private string _processingTime = "0ms";
        [ObservableProperty] private bool _isPersonDetected;
        [ObservableProperty] private string _detectionQuality = "";
        [ObservableProperty] private SKBitmap? _currentFrame;

        public MainViewModel(
            IRealTimeDetectionService detectionService,
            ICameraService cameraService,
            IFrameDispatcherService frameDispatcher,
            ILogger<MainViewModel> logger)
        {
            _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
            _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
            _frameDispatcher = frameDispatcher ?? throw new ArgumentNullException(nameof(frameDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _detectionService.OnPersonDetected += OnPersonDetected;
            _detectionService.OnDetectionError += OnDetectionError;
            _detectionService.OnStatusChanged += OnStatusChanged;
            _frameDispatcher.OnFrameForUi += OnFrameForUiReceived;

            TargetFps = _detectionService.TargetFps;
        }

        [RelayCommand]
        private async Task ToggleDetectionAsync()
        {
            if (IsDetecting)
                await StopDetectionAsync();
            else
                await StartDetectionAsync();
        }

        [RelayCommand]
        private void ChangeTargetFps(int fps)
        {
            if (fps >= 5 && fps <= 30)
            {
                TargetFps = fps;
                _detectionService.TargetFps = fps;
            }
        }

        private async Task StartDetectionAsync()
        {
            try
            {
                DetectionStatus = "Запуск камеры...";
                await _cameraService.InitializeAsync();

                DetectionStatus = "Запуск потоковой передачи...";
                await _frameDispatcher.StartFrameDispatchAsync(
                    _cameraService.GetFrameStream(CancellationToken.None),
                    CancellationToken.None);

                DetectionStatus = "Запуск детекции...";
                await _detectionService.StartRealTimeDetectionAsync();

                IsDetecting = true;
                _lastUpdateTime = DateTime.Now;
                DetectionStatus = "Камера активна - наведите на человека";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start detection");
                DetectionStatus = $"Ошибка: {ex.Message}";
                await StopDetectionAsync();
            }
        }

        private async Task StopDetectionAsync()
        {
            try
            {
                DetectionStatus = "Остановка...";

                await _detectionService.StopRealTimeDetectionAsync();
                await _frameDispatcher.StopFrameDispatchAsync();
                await _cameraService.StopAsync();

                IsDetecting = false;
                CurrentFrame?.Dispose();
                CurrentFrame = null;
                DetectionResult = null;
                DetectionConfidence = 0;
                CurrentFps = 0;
                ProcessingTime = "0ms";
                IsPersonDetected = false;
                DetectionQuality = "";
                DetectionStatus = "Готов к работе";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop detection");
                DetectionStatus = $"Ошибка остановки: {ex.Message}";
            }
        }

        private void OnFrameForUiReceived(object? sender, SKBitmap frame)
        {
            if (_isFrameUpdating)
            {
                frame.Dispose();
                return;
            }

            _isFrameUpdating = true;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    CurrentFrame?.Dispose();
                    CurrentFrame = frame;
                    CurrentFps = _detectionService.CurrentFps;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing UI frame");
                    frame.Dispose();
                }
                finally
                {
                    _isFrameUpdating = false;
                }
            });
        }

        private void OnPersonDetected(object? sender, HumanDetectionResult result)
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastUpdateTime).TotalMilliseconds;
            _lastUpdateTime = now;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DetectionResult = result;
                ProcessingTime = $"{elapsed:F0}ms";
                IsPersonDetected = result.HasPerson;

                if (result.HasPerson && result.Human != null)
                {
                    DetectionConfidence = result.Human.Confidence;
                    DetectionQuality = result.Human.Confidence switch
                    {
                        > 0.8f => " Высокая точность",
                        > 0.6f => "Хорошее качество",
                        > 0.4f => " Средняя уверенность",
                        _ => "Низкая уверенность"
                    };
                    DetectionStatus = $"Человек обнаружен: {result.Human.Confidence:P0}";
                }
                else
                {
                    DetectionConfidence = 0;
                    DetectionQuality = "";
                    DetectionStatus = "Человек не обнаружен";
                }
            });
        }

        private void OnDetectionError(object? sender, string errorMessage)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DetectionStatus = $"Ошибка: {errorMessage}";
                _logger.LogWarning("Detection error: {Error}", errorMessage);
            });
        }

        private void OnStatusChanged(object? sender, string statusMessage)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DetectionStatus = statusMessage;
            });
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            try
            {
                _frameDispatcher.OnFrameForUi -= OnFrameForUiReceived;
                _detectionService.OnPersonDetected -= OnPersonDetected;
                _detectionService.OnDetectionError -= OnDetectionError;
                _detectionService.OnStatusChanged -= OnStatusChanged;

                if (IsDetecting)
                    await StopDetectionAsync();

                CurrentFrame?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }

            _disposed = true;
        }
    }
}
