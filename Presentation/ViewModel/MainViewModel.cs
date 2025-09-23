using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel; // Permissions, MainThread

namespace Presentation.ViewModel
{
    public partial class MainViewModel : ObservableObject, IAsyncDisposable
    {
        private readonly IRealTimeDetectionService _detectionService;
        private readonly ICameraService _cameraService;
        private readonly IFrameDispatcherService _frameDispatcher;
        private readonly ILogger<MainViewModel> _logger;

        private bool _disposed;
        private bool _isFrameUpdating;
        private CancellationTokenSource? _cts;
        private Task? _frameDispatchTask;

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

            // Подписываемся на события единожды
            _detectionService.OnPersonDetected += OnPersonDetected;
            _detectionService.OnDetectionError += OnDetectionError;
            _detectionService.OnStatusChanged += OnStatusChanged;
            _frameDispatcher.OnFrameForUi += OnFrameForUiReceived;

            // Синхронизируем target fps
            _targetFps = _detectionService.TargetFps;
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
                try
                {
                    _detectionService.TargetFps = fps;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set target fps");
                }
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
                _logger.LogError(ex, "Permission check/request failed");
                return false;
            }
        }

        private async Task StartDetectionAsync()
        {
            if (IsDetecting) return;

            // Проверка пермишнов
            if (!await EnsureCameraPermissionAsync())
            {
                DetectionStatus = "Нет доступа к камере";
                return;
            }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                DetectionStatus = "Запуск камеры...";
                // Инициализация камеры — может бросить исключение
                await _cameraService.InitializeAsync();

                DetectionStatus = "Запуск потоковой передачи...";

                // Запускаем фоновую задачу, которая читает кадры и передаёт диспетчеру
                _frameDispatchTask = Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var frame in _cameraService.GetFrameStream(_cts.Token).WithCancellation(_cts.Token))
                        {
                            if (_cts.Token.IsCancellationRequested)
                            {
                                frame.Dispose();
                                break;
                            }

                            try
                            {
                                // Передаём кадр диспетчеру (он сам должен копировать/распределять)
                                _frameDispatcher.EnqueueFrame(frame);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Frame dispatch enqueue failed");
                                frame.Dispose();
                            }
                        }
                    }
                    catch (OperationCanceledException) { /* expected on cancel */ }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while reading frames from camera");
                    }
                }, _cts.Token);

                DetectionStatus = "Запуск детекции...";
                await _detectionService.StartRealTimeDetectionAsync(_cts.Token);

                IsDetecting = true;
                DetectionStatus = "Камера активна - наведите на человека";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start detection");
                DetectionStatus = $"Ошибка: {ex.Message}";

                // Попытка аккуратно откатиться
                try { _cts?.Cancel(); } catch { }
                try { await _frameDispatcher.StopFrameDispatchAsync(); } catch { }
                try { await _cameraService.StopAsync(); } catch { }
            }
        }

        private async Task StopDetectionAsync()
        {
            if (!IsDetecting && (_cts == null)) return;

            DetectionStatus = "Остановка...";
            try
            {
                _cts?.Cancel();

                try
                {
                    await _detectionService.StopRealTimeDetectionAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping detection service");
                }

                try
                {
                    await _frameDispatcher.StopFrameDispatchAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping frame dispatcher");
                }

                try
                {
                    await _cameraService.StopAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping camera service");
                }

                if (_frameDispatchTask != null)
                {
                    try { await _frameDispatchTask; } catch (Exception ex) { _logger.LogWarning(ex, "Frame dispatch task failed on stop"); }
                    _frameDispatchTask = null;
                }
            }
            finally
            {
                // Очистка UI состояния
                CurrentFrame?.Dispose();
                CurrentFrame = null;
                DetectionResult = null;
                DetectionConfidence = 0;
                CurrentFps = 0;
                ProcessingTime = "0ms";
                IsPersonDetected = false;
                DetectionQuality = "";
                IsDetecting = false;
                DetectionStatus = "Готов к работе";

                _cts?.Dispose();
                _cts = null;
            }
        }

        private void OnFrameForUiReceived(object? sender, SKBitmap frame)
        {
            if (frame == null) return;

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
                    // В нашем договоре: диспетчер предоставляет фреймы — используем их как есть
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

        private void OnPersonDetected(object? sender, HumanDetectionResult result)
        {
            if (result == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
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
            _disposed = true;

            // Отписываемся
            try
            {
                _frameDispatcher.OnFrameForUi -= OnFrameForUiReceived;
                _detectionService.OnPersonDetected -= OnPersonDetected;
                _detectionService.OnDetectionError -= OnDetectionError;
                _detectionService.OnStatusChanged -= OnStatusChanged;
            }
            catch { /* safe */ }

            try
            {
                await StopDetectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during DisposeAsync StopDetection");
            }

            CurrentFrame?.Dispose();
        }
    }
}
