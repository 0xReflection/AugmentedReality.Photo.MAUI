using Domain.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface IObjectDetectionService
    {
        Task<HumanDetectionResult> DetectPersonAsync(SKBitmap frame, CancellationToken ct = default);
    }
}
