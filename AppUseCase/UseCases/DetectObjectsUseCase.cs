using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AppUseCase.UseCases
{
    public interface IDetectObjectsUseCase
    {
        Task<HumanDetectionResult?> ExecuteAsync(SKBitmap frame, CancellationToken ct);
    }

    public sealed class DetectObjectsUseCase : IDetectObjectsUseCase
    {
        private readonly IObjectDetectionService _detector;
        private readonly ILogger<DetectObjectsUseCase> _logger;

        public DetectObjectsUseCase(IObjectDetectionService detector, ILogger<DetectObjectsUseCase> logger)
        {
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HumanDetectionResult?> ExecuteAsync(SKBitmap frame, CancellationToken ct)
        {
            if (frame == null || frame.IsNull || frame.Width == 0 || frame.Height == 0)
                return HumanDetectionResult.NoPerson;

            try
            {
                if (!_detector.IsInitialized)
                {
                    _logger.LogWarning("Detector not initialized, attempting initialization...");
                    if (!await _detector.InitializeAsync(ct))
                    {
                        _logger.LogError("Detector initialization failed");
                        return HumanDetectionResult.NoPerson;
                    }
                }

                return await _detector.DetectAsync(frame, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Detection operation cancelled");
                return HumanDetectionResult.NoPerson;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DetectObjectsUseCase");
                return HumanDetectionResult.NoPerson;
            }
        }
    }
}