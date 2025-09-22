using Domain.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface ICameraService : IDisposable
    {
        IAsyncEnumerable<SKBitmap> GetFrameStream(CancellationToken ct);
        Task<Photo?> CaptureAsync(CancellationToken ct = default);
        Task InitializeAsync();
        Task StopAsync();
    }
}