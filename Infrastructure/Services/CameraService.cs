using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Infrastructure.Services
{
    public abstract class CameraService : ICameraService
    {
        protected bool _isInitialized;
        protected bool _disposed;
        protected readonly ILogger<CameraService>? _logger;
        protected readonly object _disposeLock = new object();
        private CancellationTokenSource _globalCts;

        protected CameraService(ILogger<CameraService>? logger = null)
        {
            _logger = logger;
            _globalCts = new CancellationTokenSource();
        }

        public abstract Task InitializeAsync();
        public abstract Task StopAsync();
        public abstract IAsyncEnumerable<SKBitmap> GetFrameStream([EnumeratorCancellation] CancellationToken ct);
        public abstract Task<Photo?> CaptureAsync(CancellationToken ct = default);

        public bool IsInitialized => _isInitialized;

        public virtual async Task<bool> InitializeWithRetryAsync(int maxRetries = 3, int retryDelayMs = 1000)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await InitializeAsync();
                    _logger?.LogInformation("Camera initialized successfully on attempt {Attempt}", attempt);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Camera initialization failed on attempt {Attempt}/{MaxRetries}", attempt, maxRetries);

                    if (attempt == maxRetries)
                    {
                        _logger?.LogError(ex, "All camera initialization attempts failed");
                        return false;
                    }

                    await Task.Delay(retryDelayMs * attempt);
                }
            }
            return false;
        }

        public virtual async Task RestartAsync()
        {
            _logger?.LogInformation("Restarting camera service...");
            await StopAsync();
            await Task.Delay(500); // Пауза для стабильности
            await InitializeAsync();
            _logger?.LogInformation("Camera service restarted successfully");
        }

        public virtual bool IsCameraAvailable()
        {
            return _isInitialized && !_disposed;
        }

        public virtual async Task<CameraStatus> GetStatusAsync()
        {
            if (_disposed)
                return CameraStatus.Disposed;

            if (!_isInitialized)
                return CameraStatus.NotInitialized;

            try
            {
                var testCapture = await CaptureAsync(_globalCts.Token);
                return testCapture != null ? CameraStatus.Ready : CameraStatus.Error;
            }
            catch
            {
                return CameraStatus.Error;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_disposeLock)
            {
                if (_disposed) return;

                _logger?.LogInformation("Disposing camera service...");

                if (disposing)
                {
                    try
                    {
                        if (_isInitialized)
                        {
                            var stopTask = StopAsync();
                            if (!stopTask.Wait(TimeSpan.FromSeconds(3)))
                            {
                                _logger?.LogWarning("Camera stop operation timed out");
                            }
                        }

                        _globalCts?.Cancel();
                        _globalCts?.Dispose();
                        _globalCts = null;

                        _logger?.LogInformation("Camera service disposed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error during camera service disposal");
                    }
                }

                ReleaseUnmanagedResources();
                _disposed = true;
            }
        }

        protected virtual void ReleaseUnmanagedResources()
        {
            // Переопределить в наследниках для освобождения ресурсов
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name, "Camera service has been disposed");
        }

        protected void ThrowIfNotInitialized()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Camera service is not initialized");
        }

        protected CancellationToken CreateLinkedToken(CancellationToken userToken)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(userToken, _globalCts.Token).Token;
        }

        ~CameraService()
        {
            Dispose(false);
        }
    }

    public enum CameraStatus
    {
        NotInitialized,
        Ready,
        Error,
        Disposed
    }

    public class CameraConfiguration
    {
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 1000;
        public int OperationTimeoutMs { get; set; } = 10000;
        public bool EnableDiagnostics { get; set; } = true;
    }

    public class CameraOperationResult<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }

        public static CameraOperationResult<T> FromSuccess(T data) => new() { Success = true, Data = data };
        public static CameraOperationResult<T> FromError(string error, Exception? ex = null) => new() { Success = false, ErrorMessage = error, Exception = ex };
    }
}
