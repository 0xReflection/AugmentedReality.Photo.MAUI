

using Domain.Interfaces;
using Domain.Models;
using SkiaSharp;
using System.Runtime.CompilerServices;

namespace Infrastructure.Services
{
    public abstract class CameraService : ICameraService, IDisposable
    {
        protected bool _isInitialized;
        protected bool _disposed;

        public abstract Task InitializeAsync();
        public abstract Task StopAsync();
        public abstract IAsyncEnumerable<SKBitmap> GetFrameStream([EnumeratorCancellation] CancellationToken ct);
        public abstract Task<Photo?> CaptureAsync(CancellationToken ct = default);

        public virtual void Dispose()
        {
            if (_disposed) return;
            if (_isInitialized)
            {
                StopAsync().Wait(2000);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Освобождаем управляемые ресурсы
                }

                _disposed = true;
            }
        }

        ~CameraService()
        {
            Dispose(false);
        }
    }

}