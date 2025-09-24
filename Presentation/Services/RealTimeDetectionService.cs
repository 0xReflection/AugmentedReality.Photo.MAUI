#if ANDROID
using Domain.Interfaces;
using Domain.Models;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Presentation.Services
{
    public class RealTimeDetectionService : IRealTimeDetectionService
    {
        private readonly ChannelReader<SKBitmap> _frameReader;
        private readonly IObjectDetectionService _detector;
        private readonly CancellationTokenSource _cts = new();
        private Task _processingTask;
        private readonly object _lockObject = new object();

        private int _processedFrames;
        private DateTime _lastFpsUpdate = DateTime.Now;
        private double _currentFps;
        private HumanDetectionResult _lastResult;
        private bool _disposed = false;

        public event EventHandler<HumanDetectionResult> OnPersonDetected;
        public event EventHandler<string> OnDetectionError;
        public event EventHandler<string> OnStatusChanged;

        public bool IsDetecting { get; private set; }
        public double CurrentFps => _currentFps;
        public int TargetFps { get; set; } = 15;
        public HumanDetectionResult LastDetectionResult => _lastResult;

        public RealTimeDetectionService(ChannelReader<SKBitmap> frameReader, IObjectDetectionService detector)
        {
            _frameReader = frameReader ?? throw new ArgumentNullException(nameof(frameReader));
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        }

        public async Task StartRealTimeDetectionAsync(CancellationToken ct = default)
        {
            lock (_lockObject)
            {
                if (IsDetecting || _disposed) return;

                IsDetecting = true;
                _processedFrames = 0;
                _lastFpsUpdate = DateTime.Now;
            }

            var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token).Token;

            OnStatusChanged?.Invoke(this, "Detection started");

            _processingTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var frame in _frameReader.ReadAllAsync(linkedCt))
                    {
                        if (linkedCt.IsCancellationRequested || _disposed)
                        {
                            frame?.Dispose();
                            break;
                        }

                        await ProcessFrameAsync(frame, linkedCt);
                        UpdateFps();
                    }
                }
                catch (OperationCanceledException)
                {
                    // 
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Detection error: {ex.Message}");
                    OnDetectionError?.Invoke(this, $"Detection error: {ex.Message}");
                }
                finally
                {
                    lock (_lockObject)
                    {
                        IsDetecting = false;
                    }
                    OnStatusChanged?.Invoke(this, "Detection stopped");
                }
            }, linkedCt);

            await Task.Yield(); 
        }

        private async Task ProcessFrameAsync(SKBitmap frame, CancellationToken ct)
        {
            if (frame == null || frame.IsNull) return;

            try
            {
                var startTime = DateTime.Now;
                var result = await _detector.DetectPersonAsync(frame, ct);
                var processingTime = DateTime.Now - startTime;

                _lastResult = result;

                // событие детекции
                OnPersonDetected?.Invoke(this, result);

                // Регулировкафпс
                if (TargetFps > 0)
                {
                    var targetFrameTime = TimeSpan.FromMilliseconds(1000.0 / TargetFps);
                    var remainingTime = targetFrameTime - processingTime;

                    if (remainingTime > TimeSpan.Zero)
                    {
                        await Task.Delay(remainingTime, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Frame processing error: {ex.Message}");
                OnDetectionError?.Invoke(this, $"Frame processing error: {ex.Message}");
            }
            finally
            {
                frame?.Dispose();
            }
        }

        private void UpdateFps()
        {
            _processedFrames++;
            var now = DateTime.Now;
            var elapsed = (now - _lastFpsUpdate).TotalSeconds;

            if (elapsed >= 1.0)
            {
                _currentFps = _processedFrames / elapsed;
                _processedFrames = 0;
                _lastFpsUpdate = now;
            }
        }

        public async Task StopRealTimeDetectionAsync()
        {
            lock (_lockObject)
            {
                if (!IsDetecting) return;
                _cts.Cancel();
            }

            if (_processingTask != null)
            {
                try
                {
                    await _processingTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                _processingTask = null;
            }

            _cts.TryReset(); 
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _ = StopRealTimeDetectionAsync();
            _cts.Dispose();
        }
    }
}
#endif