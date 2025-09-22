#if ANDROID
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Domain.Interfaces;
using Domain.Models;
using Infrastructure.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Presentation.Services
{
    public class AndroidCameraService : CameraService
    {
        private readonly Context _context;

        private CameraDevice _cameraDevice;
        private CameraCaptureSession _captureSession;
        private ImageReader _imageReader;
        private CancellationTokenSource _cts;
        private int _previewWidth = 640;
        private int _previewHeight = 480;

        private object _lock = new object();

        public AndroidCameraService(Context context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public override async Task InitializeAsync()
        {
            if (_isInitialized) return;

            _cts = new CancellationTokenSource();

            var manager = (CameraManager)_context.GetSystemService(Context.CameraService);
            var cameraId = manager.GetCameraIdList()[0]; // берём первую камеру

            var tcs = new TaskCompletionSource<bool>();
            manager.OpenCamera(cameraId, new CameraStateCallback(this, tcs), null);
            await tcs.Task;

            // ImageReader для получения кадров
            _imageReader = ImageReader.NewInstance(_previewWidth, _previewHeight, ImageFormatType.Jpeg, 2);

            _isInitialized = true;
        }

        public override IAsyncEnumerable<SKBitmap> GetFrameStream(CancellationToken ct)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Camera not initialized");

            return CaptureFramesAsync(ct);
        }

        private async IAsyncEnumerable<SKBitmap> CaptureFramesAsync([EnumeratorCancellation] CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                SKBitmap bitmap = null;

                lock (_lock)
                {
                    try
                    {
                        // В реальной реализации — берём кадр с ImageReader/SurfaceTexture
                        // Здесь создаём пустой черный кадр для примера
                        bitmap = new SKBitmap(_previewWidth, _previewHeight);
                        using var canvas = new SKCanvas(bitmap);
                        canvas.Clear(SKColors.Black);
                    }
                    catch
                    {
                        bitmap?.Dispose();
                        continue;
                    }
                }

                yield return bitmap;

                await Task.Delay(33, ct); // ~30 FPS
            }
        }

        public override async Task<Photo?> CaptureAsync(CancellationToken ct = default)
        {
            if (!_isInitialized) return null;

            // Для упрощения: просто берём первый кадр из GetFrameStream
            await foreach (var frame in GetFrameStream(ct))
            {
                return new Photo(frame.Copy());
            }

            return null;
        }

        public override async Task StopAsync()
        {
            if (!_isInitialized) return;

            _cts.Cancel();

            _captureSession?.Close();
            _cameraDevice?.Close();
            _imageReader?.Close();

            _isInitialized = false;
            await Task.CompletedTask;
        }

        private class CameraStateCallback : CameraDevice.StateCallback
        {
            private readonly AndroidCameraService _service;
            private readonly TaskCompletionSource<bool> _tcs;

            public CameraStateCallback(AndroidCameraService service, TaskCompletionSource<bool> tcs)
            {
                _service = service;
                _tcs = tcs;
            }

            public override void OnOpened(CameraDevice camera)
            {
                _service._cameraDevice = camera;
                _tcs.TrySetResult(true); // <-- безопасно
            }

            public override void OnDisconnected(CameraDevice camera)
            {
                camera.Close();
                _tcs.TrySetResult(false); // <-- безопасно
            }

            public override void OnError(CameraDevice camera, CameraError error)
            {
                camera.Close();
                _tcs.TrySetException(new Exception($"Camera error: {error}")); // <-- безопасно
            }
        }
    }
}
#endif
