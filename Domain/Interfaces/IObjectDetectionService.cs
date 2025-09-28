using Domain.Models;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface IObjectDetectionService : IDisposable
    {
        bool IsInitialized { get; }
        Task<bool> InitializeAsync(CancellationToken ct = default);
        Task<HumanDetectionResult> DetectAsync(SKBitmap frame, CancellationToken ct = default);
        event EventHandler<Exception> OnError;
    }
}
