using Domain.Interfaces;
using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SkiaSharp;

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
            if (frame == null) return HumanDetectionResult.NoPerson;

            using var ms = new System.IO.MemoryStream();
            frame.Encode(ms, SkiaSharp.SKEncodedImageFormat.Jpeg, 80);
            ms.Position = 0;

            return await _detector.DetectPersonAsync(frame, ct);
        }
    }
}