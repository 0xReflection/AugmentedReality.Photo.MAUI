using Domain.Interfaces;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Presentation.Services
{
    public sealed class FrameDispatcherService : IFrameDispatcherService
    {
        public event EventHandler<SKBitmap> OnFrameForUi;
        public event EventHandler<SKBitmap> OnFrameForAi;

        private CancellationTokenSource _cts;
        private bool _isDispatching;
        private int _fps;
        private long _framesProcessed;
        private readonly ConcurrentQueue<SKBitmap> _queue = new();

        public bool IsDispatching => _isDispatching;
        public int Fps => _fps;
        public int QueueSize => _queue.Count;
        public long TotalFramesProcessed => _framesProcessed;

        public async Task StartFrameDispatchAsync(IAsyncEnumerable<SKBitmap> frameSource, CancellationToken ct)
        {
            if (_isDispatching) return;
            _isDispatching = true;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            _ = Task.Run(async () =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int frames = 0;

                await foreach (var frame in frameSource.WithCancellation(_cts.Token))
                {
                    frames++;
                    _framesProcessed++;

                    var aiFrame = frame.Copy();

                    OnFrameForUi?.Invoke(this, frame);
                    OnFrameForAi?.Invoke(this, aiFrame);

                    if (sw.ElapsedMilliseconds >= 1000)
                    {
                        _fps = frames;
                        frames = 0;
                        sw.Restart();
                    }
                }
            }, _cts.Token);
        }

        public async Task StopFrameDispatchAsync()
        {
            _cts?.Cancel();
            _isDispatching = false;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            while (_queue.TryDequeue(out var bmp))
                bmp.Dispose();
        }
    }
}
