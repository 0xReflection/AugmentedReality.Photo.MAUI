using Domain.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface IOverlayDrawable
    {
        void Draw(SKCanvas canvas, HumanDetectionResult detectionResult, SKBitmap overlayImage);
    }
}
