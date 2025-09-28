using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
        int QueueSize { get; }
        long TotalFramesProcessed { get; }
    }
}
