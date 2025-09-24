using Domain.Models;
using SkiaSharp;

namespace Domain.Interfaces
{
    public interface IObjectDetectionService
    {
        Task<HumanDetectionResult> DetectPersonAsync(SKBitmap frame, CancellationToken ct = default);
    }
}