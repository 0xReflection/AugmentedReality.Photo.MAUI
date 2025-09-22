using Domain.Interfaces;
using Domain.Models;
using SkiaSharp;

namespace Infrastructure.Drawing
{
    //public sealed class BallOverlayDrawable : IOverlayDrawable
    //{
    //    public void Draw(SKCanvas canvas, HumanDetectionResult detectionResult, SKBitmap? overlayImage)
    //    {
    //        if (detectionResult == null || !detectionResult.HasPerson || detectionResult.Human == null)
    //            return;

    //        var human = detectionResult.Human;

    //        float ballSize = Math.Max(human.Width, human.Height) * 0.2f;
    //        ballSize = Math.Clamp(ballSize, 20, 100);

    //        float ballX = human.X + (human.Width / 2) - (ballSize / 2);
    //        float ballY = human.Y - ballSize - 10;

    //        var rect = new SKRect(ballX, ballY, ballX + ballSize, ballY + ballSize);

    //        using var paint = new SKPaint { Color = SKColors.Red, IsAntialias = true };
    //        canvas.DrawCircle(rect.MidX, rect.MidY, ballSize / 2, paint);
    //    }
    //}
}