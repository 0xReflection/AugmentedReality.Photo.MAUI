using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace Presentation.ViewModel
{
    public partial class MainViewModel : ObservableObject, IAsyncDisposable
    {
        private readonly IRealTimeDetectionService _detectionService;
        private readonly ICameraService _cameraService;
        private readonly IFrameDispatcherService _frameDispatcher;
        private readonly ILogger<MainViewModel> _logger;

        private bool _disposed = false;
        private bool _isFrameUpdating = false;
        private CancellationTokenSource _cts;
        private Task _frameDispatchTask;
        private DateTime _lastProcessingTimeUpdate = DateTime.Now;
        private readonly object _lockObject = new object();

        [ObservableProperty]
        private bool _isDetecting;

        [ObservableProperty]
        private string _detectionStatus = "Готов к работе";

        [ObservableProperty]
        private HumanDetectionResult _detectionResult;

        [ObservableProperty]
        private float _detectionConfidence;

        [ObservableProperty]
        private double _currentFps;

        [ObservableProperty]
        private int _targetFps = 15;

        [ObservableProperty]
        private string _processingTime = "0ms";

        [ObservableProperty]
        private bool _isPersonDetected;

        [ObservableProperty]
        private string _detectionQuality = "";

        [ObservableProperty]
        private SKBitmap _currentFrame;

        [ObservableProperty]
        private bool _isCameraActive;

        [ObservableProperty]
        private bool _isProcessing;

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
            {
                await StopDetectionAsync();
            }
            else
            {
                await StartDetectionAsync();
            }
        }

        [RelayCommand]
        private void ChangeTargetFps(int fps)
        {
            if (fps >= 1 && fps <= 30)
            {
                TargetFps = fps;
                _detectionService.TargetFps = fps;
                DetectionStatus = $"FPS установлен на {fps}";
            }
        }

        [RelayCommand]
        private async Task CapturePhotoAsync()
        {
            if (!IsDetecting) return;

            try
            {
                IsProcessing = true;
                var photo = await _cameraService.CaptureAsync(_cts?.Token ?? default);

                if (photo?.Bitmap != null)
                {
                    DetectionStatus = "Фото захвачено";
                    photo.Bitmap.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing photo");
                DetectionStatus = "Ошибка захвата фото";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task RestartCameraAsync()
        {
            if (!IsDetecting) return;

            try
            {
                IsProcessing = true;
                DetectionStatus = "Перезапуск камеры...";

                await StopDetectionAsync();
                await Task.Delay(500);
                await StartDetectionAsync();

                DetectionStatus = "Камера перезапущена";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting camera");
                DetectionStatus = "Ошибка перезапуска";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task<bool> EnsureCameraPermissionAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (status == PermissionStatus.Granted) return true;

                status = await Permissions.RequestAsync<Permissions.Camera>();
                return status == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Camera permission error");
                return false;
            }
        }

        private async Task StartDetectionAsync()
        {
            lock (_lockObject)
            {
                if (IsDetecting || _disposed) return;
            }

            if (!await EnsureCameraPermissionAsync())
            {
                DetectionStatus = "Нет разрешения камеры";
                return;
            }

            _cts = new CancellationTokenSource();

            try
            {
                DetectionStatus = "Инициализация камеры";
                IsProcessing = true;

                await _cameraService.InitializeAsync();
                IsCameraActive = true;

                DetectionStatus = "Запуск потоков";

             
                _frameDispatchTask = ProcessCameraFramesAsync(_cts.Token);

                DetectionStatus = "Запуск детекции";
                await _detectionService.StartRealTimeDetectionAsync(_cts.Token);

                lock (_lockObject)
                {
                    IsDetecting = true;
                }

                IsProcessing = false;
                DetectionStatus = "Камера активна - наведите на человека";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start detection");
                DetectionStatus = $"Ошибка: {ex.Message}";
                await StopDetectionAsync();
            }
        }

        private async Task ProcessCameraFramesAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var frame in _cameraService.GetFrameStream(ct).WithCancellation(ct))
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        _frameDispatcher.EnqueueFrame(frame);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error enqueueing frame");
                        frame?.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Camera frame processing error");
                OnDetectionError(this, $"Camera error: {ex.Message}");
            }
        }

        private async Task StopDetectionAsync()
        {
            lock (_lockObject)
            {
                if (!IsDetecting && _cts == null) return;
                _cts?.Cancel();
            }

            try
            {
                await _detectionService.StopRealTimeDetectionAsync();
                await _frameDispatcher.StopFrameDispatchAsync();
                await _cameraService.StopAsync();

                if (_frameDispatchTask != null)
                {
                    await _frameDispatchTask;
                    _frameDispatchTask = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during stop");
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CurrentFrame?.Dispose();
                    CurrentFrame = null;
                    DetectionResult = HumanDetectionResult.NoPerson;
                    DetectionConfidence = 0;
                    CurrentFps = 0;
                    ProcessingTime = "0ms";
                    IsPersonDetected = false;
                    DetectionQuality = "";
                    IsDetecting = false;
                    IsCameraActive = false;
                    IsProcessing = false;
                    DetectionStatus = "Готов к работе";
                });

                _cts?.Dispose();
                _cts = null;
            }
        }

        private void OnFrameForUiReceived(object sender, SKBitmap frame)
        {
            if (_isFrameUpdating || _disposed || frame == null || frame.IsNull)
            {
                frame?.Dispose();
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
                    _logger.LogError(ex, "Error updating UI frame");
                    frame.Dispose();
                }
                finally
                {
                    _isFrameUpdating = false;
                }
            });
        }

        private void OnPersonDetected(object sender, HumanDetectionResult result)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    DetectionResult = result;
                    IsPersonDetected = result.HasPerson;

                    if (result.HasPerson && result.Human != null)
                    {
                        DetectionConfidence = result.Human.Confidence;
                        DetectionQuality = result.Human.Confidence switch
                        {
                            > 0.8f => "Высокая точность",
                            > 0.6f => "Хорошее качество",
                            > 0.4f => "Средняя уверенность",
                            _ => "Низкая уверенность"
                        };
                        DetectionStatus = $"Человек: {result.Human.Confidence:P0}";
                    }
                    else
                    {
                        DetectionConfidence = 0;
                        DetectionQuality = "";
                        DetectionStatus = "Человек не обнаружен";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating detection result");
                }
            });
        }

        private void OnDetectionError(object sender, string error)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DetectionStatus = $"Ошибка: {error}";
            });
        }

        private void OnStatusChanged(object sender, string status)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DetectionStatus = status;
            });
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            _disposed = true;
            _frameDispatcher.OnFrameForUi -= OnFrameForUiReceived;
            _detectionService.OnPersonDetected -= OnPersonDetected;
            _detectionService.OnDetectionError -= OnDetectionError;
            _detectionService.OnStatusChanged -= OnStatusChanged;

            await StopDetectionAsync();
            CurrentFrame?.Dispose();
        }
    }
}