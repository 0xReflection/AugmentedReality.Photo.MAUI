using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    using System;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SkiaSharp;
    using Microsoft.Maui.ApplicationModel; // MainThread

    public sealed class FrameDispatcherService : IFrameDispatcherService, IAsyncDisposable
    {
        private readonly ILogger<FrameDispatcherService> _logger;
        private CancellationTokenSource? _dispatchCts;
        private Channel<SKBitmap>? _aiChannel;
        private Task? _dispatchTask;
        private Task? _aiWorkerTask;
        private bool _disposed;
        private int _frameCount;
        private DateTime _lastFpsUpdate;
        private int _fps;

        public event EventHandler<SKBitmap>? OnFrameForUi;
        public event EventHandler<SKBitmap>? OnFrameForAi;

        public bool IsDispatching { get; private set; }
        public int Fps => _fps;

        public FrameDispatcherService(ILogger<FrameDispatcherService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartFrameDispatchAsync(IAsyncEnumerable<SKBitmap> frameSource, CancellationToken ct)
        {
            if (IsDispatching)
            {
                _logger.LogWarning("Frame dispatch is already running");
                return;
            }

            _dispatchCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _dispatchCts.Token;
            var options = new BoundedChannelOptions(1)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest
            };
            _aiChannel = Channel.CreateBounded<SKBitmap>(options);

            IsDispatching = true;
            _frameCount = 0;
            _lastFpsUpdate = DateTime.UtcNow;
            _fps = 0;

            _logger.LogInformation("Starting frame dispatch");

            _aiWorkerTask = Task.Run(() => AiWorkerAsync(_aiChannel.Reader, token), token);
            _dispatchTask = Task.Run(() => DispatchLoopAsync(frameSource, token), token);
            await Task.Delay(50, CancellationToken.None);
            _logger.LogInformation("Frame dispatch started successfully");
        }

        private async Task DispatchLoopAsync(IAsyncEnumerable<SKBitmap> frameSource, CancellationToken ct)
        {
            try
            {
                await foreach (var frame in frameSource.WithCancellation(ct))
                {
                    if (ct.IsCancellationRequested)
                    {
                        frame.Dispose();
                        break;
                    }

                    SKBitmap? uiFrame = null;
                    SKBitmap? aiFrame = null;

                    try
                    {

                        if (OnFrameForUi != null)
                        {
                            try
                            {
                                uiFrame = frame.Copy(); // создаём копию для UI
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    try
                                    {
                                        OnFrameForUi?.Invoke(this, uiFrame);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error in UI frame handler");
                                        uiFrame.Dispose();
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to copy frame for UI");
                                uiFrame?.Dispose();
                            }
                        }


                        if (_aiChannel != null && OnFrameForAi != null)
                        {
                            try
                            {
                                aiFrame = frame.Copy(); // создаём копию для AI
                                var written = _aiChannel.Writer.TryWrite(aiFrame);
                                if (!written)
                                {
                                    aiFrame.Dispose();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to copy frame for AI");
                                aiFrame?.Dispose();
                            }
                        }
                    }
                    finally
                    {
                       
                        frame.Dispose();
                    }

                    UpdateFps();

                    // Поддержка примерно 30–60 FPS
                    await Task.Delay(16, ct); // ~60fps
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Frame dispatch cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in frame dispatch loop");
            }
            finally
            {
                IsDispatching = false;
                try { _aiChannel?.Writer.TryComplete(); } catch { }
                _logger.LogInformation("Frame dispatch stopped");
            }
        }


        private async Task AiWorkerAsync(ChannelReader<SKBitmap> reader, CancellationToken ct)
        {
            try
            {
                while (await reader.WaitToReadAsync(ct))
                {
                    while (reader.TryRead(out var aiFrame))
                    {
                        try
                        {
                            
                            try
                            {
                                OnFrameForAi?.Invoke(this, aiFrame);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error in AI frame handler");
                               
                            }
                        }
                        finally
                        {
                           
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI worker failed");
            }
        }

        private void UpdateFps()
        {
            _frameCount++;
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastFpsUpdate).TotalSeconds;

            if (elapsed >= 1.0)
            {
                _fps = (int)(_frameCount / elapsed);
                _frameCount = 0;
                _lastFpsUpdate = now;
                _logger.LogDebug($"Frame dispatch FPS: {_fps}");
            }
        }

        public async Task StopFrameDispatchAsync()
        {
            if (!IsDispatching) return;

            try
            {
                _logger.LogInformation("Stopping frame dispatch...");
                _dispatchCts?.Cancel();

                if (_dispatchTask != null)
                {
                    await Task.WhenAny(_dispatchTask, Task.Delay(TimeSpan.FromSeconds(5)));
                }
                try { _aiChannel?.Writer.TryComplete(); } catch { }
                if (_aiWorkerTask != null)
                {
                    await Task.WhenAny(_aiWorkerTask, Task.Delay(TimeSpan.FromSeconds(5)));
                }

                IsDispatching = false;
                _logger.LogInformation("Frame dispatch stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping frame dispatch");
            }
            finally
            {
                _dispatchCts?.Dispose();
                _dispatchCts = null;
                _aiChannel = null;
                _dispatchTask = null;
                _aiWorkerTask = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            try
            {
                if (IsDispatching)
                    await StopFrameDispatchAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during DisposeAsync");
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().Wait();
        }
    }
}
