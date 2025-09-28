using SkiaSharp;

namespace Domain.Models
{
    public sealed class Photo : IDisposable
    {
        public SKBitmap Bitmap { get; }
        public string? FilePath { get; set; }

        private bool _disposed;

        public Photo(SKBitmap bitmap)
        {
            Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Bitmap?.Dispose();
            GC.SuppressFinalize(this);
        }

        ~Photo() => Dispose();
    }
}
