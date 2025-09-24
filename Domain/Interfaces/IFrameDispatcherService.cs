using SkiaSharp;

namespace Domain.Interfaces
{
    public interface IFrameDispatcherService : IDisposable
    {
        event EventHandler<SKBitmap> OnFrameForUi;
        event EventHandler<SKBitmap> OnFrameForAi;

        Task StartFrameDispatchAsync(IAsyncEnumerable<SKBitmap> frameSource, CancellationToken ct);
        Task StopFrameDispatchAsync();
        bool IsDispatching { get; }
        int Fps { get; }
        void EnqueueFrame(SKBitmap frame);
    }
}