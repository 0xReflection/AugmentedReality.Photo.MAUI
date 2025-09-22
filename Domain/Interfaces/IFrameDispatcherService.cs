using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface IFrameDispatcherService : IDisposable
    {
        // Событие для UI - кадр готов к отображению
        event EventHandler<SKBitmap> OnFrameForUi;

        // Событие для AI - кадр готов для обработки
        event EventHandler<SKBitmap> OnFrameForAi;

        // Запуск диспетчера
        Task StartFrameDispatchAsync(IAsyncEnumerable<SKBitmap> frameSource, CancellationToken ct);

        // Остановка диспетчера
        Task StopFrameDispatchAsync();

        // Статус
        bool IsDispatching { get; }
        int Fps { get; }
    }
}
