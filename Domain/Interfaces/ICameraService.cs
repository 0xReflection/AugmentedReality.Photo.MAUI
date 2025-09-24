using Domain.Models;
using SkiaSharp;

namespace Domain.Interfaces
{
    public interface ICameraService : IDisposable
    {
        IAsyncEnumerable<SKBitmap> GetFrameStream(CancellationToken ct);
        Task<Photo?> CaptureAsync(CancellationToken ct = default);
        Task InitializeAsync();
        Task StopAsync();
        bool IsInitialized { get; }
    }
}