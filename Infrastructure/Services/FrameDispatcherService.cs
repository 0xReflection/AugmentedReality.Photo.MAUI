using Domain.Interfaces;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class FrameDispatcherService : IFrameDispatcherService
    {
        private Channel<SKBitmap>? _frameChannel;
        private CancellationTokenSource? _cts;
        private Task? _dispatchTask;

        public event EventHandler<SKBitmap>? OnFrameForUi;
        public event EventHandler<SKBitmap>? OnFrameForAi;

        public bool IsDispatching { get; private set; }
        public int Fps { get; private set; }

        public Task StartFrameDispatchAsync(IAsyncEnumerable<SKBitmap> frameSource, CancellationToken ct = default)
        {
            if (IsDispatching) return Task.CompletedTask;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _frameChannel = Channel.CreateUnbounded<SKBitmap>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });

            // Задача для чтения кадров из source и записи в канал
            _dispatchTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var frame in frameSource.WithCancellation(_cts.Token))
                    {
                        if (_frameChannel == null || _cts.IsCancellationRequested)
                        {
                            frame.Dispose();
                            break;
                        }

                        while (!_frameChannel.Writer.TryWrite(frame))
                        {
                            await Task.Delay(1, _cts.Token);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                finally
                {
                    _frameChannel?.Writer.TryComplete();
                }
            }, _cts.Token);

            // Задача для рассылки кадров подписчикам
            _ = Task.Run(async () =>
            {
                var reader = _frameChannel!.Reader;
                try
                {
                    while (await reader.WaitToReadAsync(_cts.Token))
                    {
                        while (reader.TryRead(out var frame))
                        {
                            try
                            {
                                OnFrameForUi?.Invoke(this, frame);
                                OnFrameForAi?.Invoke(this, frame);
                            }
                            finally
                            {
                                frame.Dispose();
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }, _cts.Token);

            IsDispatching = true;
            return Task.CompletedTask;
        }

        public async Task StopFrameDispatchAsync()
        {
            if (!IsDispatching) return;

            _cts?.Cancel();
            if (_dispatchTask != null)
            {
                try { await _dispatchTask; } catch { }
            }

            _frameChannel = null;

            _cts?.Dispose();
            _cts = null;
            _dispatchTask = null;
            IsDispatching = false;
        }

        public void EnqueueFrame(SKBitmap frame)
        {
            if (frame == null) return;

            try
            {
                if (_frameChannel != null && !_cts?.IsCancellationRequested == true)
                {
                    if (!_frameChannel.Writer.TryWrite(frame))
                    {
                        frame.Dispose();
                    }
                }
                else
                {
                    frame.Dispose();
                }
            }
            catch
            {
                frame.Dispose();
            }
        }

        public void Dispose()
        {
            StopFrameDispatchAsync().Wait();
        }
    }
}
