#if ANDROID
using Android.Content;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;

using AndroidX.Lifecycle;
using Domain.Interfaces;
using Domain.Models;
using Java.Lang;
using Java.Util.Concurrent;
using Microsoft.Maui.ApplicationModel;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Presentation.Services
{
    public sealed class AndroidCameraService : ICameraService, IAsyncDisposable
    {
        private Preview _preview;
        private ImageAnalysis _imageAnalysis;
        private ProcessCameraProvider _cameraProvider;
        private readonly ConcurrentQueue<SKBitmap> _frames = new();
        private bool _isInitialized;
        private bool _disposed;

        public bool IsInitialized => _isInitialized;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_isInitialized) return;

            var context = Android.App.Application.Context;
            _cameraProvider = await GetCameraProviderAsync(context);

            _preview = new Preview.Builder().Build();

            _imageAnalysis = new ImageAnalysis.Builder()
                .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest)
                .SetOutputImageFormat(ImageAnalysis.OutputImageFormatYuv420888)
                .Build();

            _imageAnalysis.SetAnalyzer(
                Executors.NewSingleThreadExecutor(),
                new Analyzer(this)
            );

            var selector = new CameraSelector.Builder()
                .RequireLensFacing(CameraSelector.LensFacingBack)
                .Build();

            // Правильный способ получить Activity в MAUI
            var activity = Platform.CurrentActivity;
            if (activity == null)
                throw new InvalidOperationException("Current Activity is null. Make sure InitializeAsync is called after OnCreate.");

            _cameraProvider.BindToLifecycle((ILifecycleOwner)activity, selector, _preview, _imageAnalysis);

            _isInitialized = true;
        }

        private sealed class Analyzer : Java.Lang.Object, ImageAnalysis.IAnalyzer
        {
            private readonly AndroidCameraService _svc;
            public Analyzer(AndroidCameraService svc) => _svc = svc;

            public void Analyze(IImageProxy image)
            {
                try
                {
                    var bmp = Convert(image);
                    if (bmp != null)
                    {
                        if (_svc._frames.Count >= 3 && _svc._frames.TryDequeue(out var old))
                            old.Dispose();
                        _svc._frames.Enqueue(bmp);
                    }
                }
                finally { image.Close(); }
            }

            private static SKBitmap? Convert(IImageProxy image)
            {
                var planes = image.GetPlanes();
                if (planes.Length < 3) return null;

                int width = image.Width;
                int height = image.Height;
                var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var ptr = bmp.GetPixels();

                unsafe
                {
                    byte* dst = (byte*)ptr.ToPointer();
                    int rowBytes = bmp.RowBytes;

                    var yBuf = planes[0].Buffer;
                    var uBuf = planes[1].Buffer;
                    var vBuf = planes[2].Buffer;

                    int yRowStride = planes[0].RowStride;
                    int uRowStride = planes[1].RowStride;
                    int vRowStride = planes[2].RowStride;

                    int uPixelStride = planes[1].PixelStride;
                    int vPixelStride = planes[2].PixelStride;

                    for (int j = 0; j < height; j++)
                    {
                        for (int i = 0; i < width; i++)
                        {
                            int yIndex = j * yRowStride + i;
                            int uvIndex = (j / 2) * uRowStride + (i / 2) * uPixelStride;

                            byte y = (byte)yBuf.Get(yIndex);
                            byte u = (byte)uBuf.Get(uvIndex);
                            byte v = (byte)vBuf.Get(uvIndex);

                            int c = y - 16;
                            int d = u - 128;
                            int e = v - 128;

                            int r = (298 * c + 409 * e + 128) >> 8;
                            int g = (298 * c - 100 * d - 208 * e + 128) >> 8;
                            int b = (298 * c + 516 * d + 128) >> 8;

                            r = Java.Lang.Math.Clamp(r, 0, 255);
                            g = Java.Lang.Math.Clamp(g, 0, 255);
                            b = Java.Lang.Math.Clamp(b, 0, 255);

                            byte* pixel = dst + j * rowBytes + i * 4;
                            pixel[0] = (byte)b;
                            pixel[1] = (byte)g;
                            pixel[2] = (byte)r;
                            pixel[3] = 255;
                        }
                    }
                }

                return bmp;
            }
        }

        public async IAsyncEnumerable<SKBitmap> GetFrameStream([EnumeratorCancellation] CancellationToken ct = default)
        {
            while (!ct.IsCancellationRequested && !_disposed)
            {
                if (_frames.TryDequeue(out var frame)) yield return frame;
                else await Task.Delay(10, ct);
            }
        }

        public Task<Photo> CaptureAsync(CancellationToken ct = default)
        {
            if (_frames.TryPeek(out var frame))
                return Task.FromResult(new Photo(frame.Copy()));

            throw new InvalidOperationException("No frame available");
        }

        public Task StopAsync()
        {
            _cameraProvider?.UnbindAll();
            while (_frames.TryDequeue(out var f)) f.Dispose();
            _isInitialized = false;
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            await StopAsync();
        }

        private static Task<ProcessCameraProvider> GetCameraProviderAsync(Context context)
        {
            var tcs = new TaskCompletionSource<ProcessCameraProvider>();
            var future = ProcessCameraProvider.GetInstance(context);
            future.AddListener(new Runnable(() =>
            {
                try { tcs.SetResult((ProcessCameraProvider)future.Get()); }
                catch (Java.Lang.Exception ex) { tcs.SetException(ex); }
            }), Executors.NewSingleThreadExecutor());

            return tcs.Task;
        }

        private sealed class Runnable : Java.Lang.Object, IRunnable
        {
            private readonly Action _action;
            public Runnable(Action action) => _action = action;
            public void Run() => _action();
        }
    }
}
#endif
