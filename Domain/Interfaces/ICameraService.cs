using Domain.Models;
using SkiaSharp;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface ICameraService : IAsyncDisposable
    {
        bool IsInitialized { get; }
        Task InitializeAsync(CancellationToken ct = default);
        IAsyncEnumerable<SKBitmap> GetFrameStream(CancellationToken ct = default);
        Task StopAsync();
        Task<Photo> CaptureAsync(CancellationToken ct = default);
    }
}
