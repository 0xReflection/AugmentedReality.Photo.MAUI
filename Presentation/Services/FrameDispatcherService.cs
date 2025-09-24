using Domain.Interfaces;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Presentation.Services
{
    public class FrameDispatcherService : IFrameDispatcherService, IDisposable
    {
        private readonly Channel<SKBitmap> _uiChannel = Channel.CreateBounded<SKBitmap>(3);
        private readonly Channel<SKBitmap> _aiChannel = Channel.CreateBounded<SKBitmap>(2);

        private CancellationTokenSource _cts;
        private Task _dispatchTask;
        private int _fps;
        private DateTime _lastFpsUpdate = DateTime.Now;
        private int _frameCount;
        private bool _disposed = false;
        private readonly object _lockObject = new object();

        public event EventHandler<SKBitmap> OnFrameForUi;
        public event EventHandler<SKBitmap> OnFrameForAi;

        public bool IsDispatching { get; private set; }
        public int Fps => _fps;

        public async Task StartFrameDispatchAsync(IAsyncEnumerable<SKBitmap> frameSource, CancellationToken ct)
        {
            lock (_lockObject)
            {
                if (IsDispatching || _disposed) return;

                _cts?.Dispose();
                _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                IsDispatching = true;
                _frameCount = 0;
                _lastFpsUpdate = DateTime.Now;
            }

            _dispatchTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var frame in frameSource.WithCancellation(_cts.Token))
                    {
                        if (_cts.Token.IsCancellationRequested || _disposed)
                        {
                            frame?.Dispose();
                            break;
                        }

                        await ProcessFrameAsync(frame);
                        UpdateFps();
                    }
                }
                catch (OperationCanceledException)
                {
                    // 
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Frame dispatch error: {ex.Message}");
                }
                finally
                {
                    lock (_lockObject)
                    {
                        IsDispatching = false;
                    }
                }
            });
        }

        private async Task ProcessFrameAsync(SKBitmap originalFrame)
        {
            SKBitmap uiFrame = null;
            SKBitmap aiFrame = null;

            try
            {
                if (originalFrame == null || originalFrame.IsNull) return;
     
                uiFrame = originalFrame.Copy();
                aiFrame = originalFrame.Copy();

                // ui
                if (uiFrame != null && !uiFrame.IsNull)
                {
                    if (_uiChannel.Writer.TryWrite(uiFrame))
                    {
                        OnFrameForUi?.Invoke(this, uiFrame);
                        uiFrame = null;
                    }
                }
                //ai
              
                if (aiFrame != null && !aiFrame.IsNull)
                {
                    if (_aiChannel.Writer.TryWrite(aiFrame))
                    {
                        OnFrameForAi?.Invoke(this, aiFrame);
                        aiFrame = null; 
                    }
                }

               
                await Task.Delay(33); // ~30 FPS
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Frame processing error: {ex.Message}");
            }
            finally
            {
   
                originalFrame?.Dispose();
                uiFrame?.Dispose();
                aiFrame?.Dispose();
            }
        }

        public void EnqueueFrame(SKBitmap frame)
        {
            if (_disposed || !IsDispatching || frame == null || frame.IsNull)
            {
                frame?.Dispose();
                return;
            }

            try
            {
            
                var uiFrame = frame.Copy();

                if (uiFrame != null && !uiFrame.IsNull)
                {
                    if (_uiChannel.Writer.TryWrite(uiFrame))
                    {
                        OnFrameForUi?.Invoke(this, uiFrame);
                    }
                    else
                    {
                        uiFrame.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enqueueing frame: {ex.Message}");
            }
        }

        private void UpdateFps()
        {
            _frameCount++;
            var now = DateTime.Now;
            var elapsed = (now - _lastFpsUpdate).TotalSeconds;

            if (elapsed >= 1.0)
            {
                _fps = (int)(_frameCount / elapsed);
                _frameCount = 0;
                _lastFpsUpdate = now;
            }
        }

        public async Task StopFrameDispatchAsync()
        {
            lock (_lockObject)
            {
                if (!IsDispatching) return;
                _cts?.Cancel();
            }

            if (_dispatchTask != null)
            {
                try
                {
                    await _dispatchTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                _dispatchTask = null;
            }

      
            _uiChannel.Writer.TryComplete();
            _aiChannel.Writer.TryComplete();

        
            CleanupChannel(_uiChannel);
            CleanupChannel(_aiChannel);

            lock (_lockObject)
            {
                IsDispatching = false;
            }
        }

        private void CleanupChannel(Channel<SKBitmap> channel)
        {
            while (channel.Reader.TryRead(out var frame))
            {
                frame?.Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _ = StopFrameDispatchAsync();
            _cts?.Dispose();
        }
    }
}