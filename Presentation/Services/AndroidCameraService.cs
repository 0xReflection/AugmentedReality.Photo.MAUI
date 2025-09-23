#if ANDROID
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Views;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Infrastructure.Services;
using Domain.Models;

namespace Presentation.Services
{
    public class AndroidCameraService : CameraService
    {
        private readonly Context _context;
        private CameraDevice? _cameraDevice;
        private CameraCaptureSession? _captureSession;
        private ImageReader? _imageReader;
        private HandlerThread? _cameraThread;
        private Handler? _cameraHandler;
        private CancellationTokenSource? _cts;
        private Channel<SKBitmap>? _frameChannel;

        private readonly int _previewWidth = 640;
        private readonly int _previewHeight = 480;

        public AndroidCameraService(Context context) => _context = context ?? throw new ArgumentNullException(nameof(context));

        public override async Task InitializeAsync()
        {
            if (_isInitialized) return;

            _cts = new CancellationTokenSource();
            _frameChannel = Channel.CreateBounded<SKBitmap>(new BoundedChannelOptions(2)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _cameraThread = new HandlerThread("CameraBackground");
            _cameraThread.Start();
            _cameraHandler = new Handler(_cameraThread.Looper);

            var manager = (CameraManager)_context.GetSystemService(Context.CameraService);
            string? cameraId = null;

            foreach (var id in manager.GetCameraIdList())
            {
                var characteristics = manager.GetCameraCharacteristics(id);
                var facing = (int)characteristics.Get(CameraCharacteristics.LensFacing);
                var capabilities = (int[])characteristics.Get(CameraCharacteristics.RequestAvailableCapabilities);

                // Отбрасываем aux камеры и неподдерживаемые
                if (capabilities != null && Array.Exists(capabilities, c => c == (int)RequestAvailableCapabilities.BackwardCompatible))
                {
                    if (facing == (int)LensFacing.Back || facing == (int)LensFacing.Front)
                    {
                        cameraId = id;
                        break;
                    }
                }
            }

            if (cameraId == null) throw new Exception("No valid camera found");

            int attempts = 0;
            while (attempts < 3)
            {
                try
                {
                    await OpenCameraAsync(manager, cameraId);
                    _isInitialized = true;
                    return;
                }
                catch (CameraAccessException)
                {
                    attempts++;
                    await Task.Delay(1000);
                }
            }

            throw new Exception("Cannot open camera after retries");
        }

        private Task OpenCameraAsync(CameraManager manager, string cameraId)
        {
            var tcs = new TaskCompletionSource<bool>();
            manager.OpenCamera(cameraId, new CameraStateCallback(this, tcs), _cameraHandler);
            return tcs.Task.ContinueWith(async _ =>
            {
                _imageReader = ImageReader.NewInstance(_previewWidth, _previewHeight, ImageFormatType.Yuv420888, 3);
                _imageReader.SetOnImageAvailableListener(new ImageAvailableListener(this, _cts.Token!), _cameraHandler);

                var surface = _imageReader.Surface;
                var captureRequestBuilder = _cameraDevice!.CreateCaptureRequest(CameraTemplate.Preview);
                captureRequestBuilder.AddTarget(surface);

                var sessionTcs = new TaskCompletionSource<bool>();
                _cameraDevice.CreateCaptureSession(
                    new List<Surface> { surface },
                    new CaptureStateCallback(this, captureRequestBuilder, sessionTcs),
                    _cameraHandler
                );
                await sessionTcs.Task;
            }).Unwrap();
        }

        public override IAsyncEnumerable<SKBitmap> GetFrameStream(CancellationToken ct)
        {
            if (!_isInitialized || _frameChannel == null)
                throw new InvalidOperationException("Camera not initialized");
            return ReadFramesAsync(ct);
        }

        private async IAsyncEnumerable<SKBitmap> ReadFramesAsync([EnumeratorCancellation] CancellationToken ct)
        {
            var reader = _frameChannel!.Reader;
            while (await reader.WaitToReadAsync(ct))
            {
                while (reader.TryRead(out var frame))
                {
                    yield return frame;
                }
            }
        }

        public override async Task<Photo?> CaptureAsync(CancellationToken ct = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts!.Token);
            await foreach (var frame in GetFrameStream(cts.Token))
            {
                return new Photo(frame);
            }
            return null;
        }

        public override async Task StopAsync()
        {
            if (!_isInitialized) return;

            _cts?.Cancel();
            await Task.Delay(50);

            try { _captureSession?.StopRepeating(); } catch { }
            try { _captureSession?.Close(); } catch { }
            try { _cameraDevice?.Close(); } catch { }
            try { _imageReader?.Close(); } catch { }

            _captureSession = null;
            _cameraDevice = null;
            _imageReader = null;

            _frameChannel?.Writer.TryComplete();

            if (_cameraThread != null)
            {
                _cameraThread.QuitSafely();
                _cameraThread.Join();
                _cameraThread = null;
                _cameraHandler = null;
            }

            _isInitialized = false;
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
                _tcs.TrySetResult(true);
            }

            public override void OnDisconnected(CameraDevice camera)
            {
                camera.Close();
                _tcs.TrySetResult(false);
            }

            public override void OnError(CameraDevice camera, CameraError error)
            {
                camera.Close();
                _tcs.TrySetException(new Exception($"Camera error: {error}"));
            }
        }

        private class CaptureStateCallback : CameraCaptureSession.StateCallback
        {
            private readonly AndroidCameraService _service;
            private readonly CaptureRequest.Builder _builder;
            private readonly TaskCompletionSource<bool> _tcs;

            public CaptureStateCallback(AndroidCameraService service, CaptureRequest.Builder builder, TaskCompletionSource<bool> tcs)
            {
                _service = service;
                _builder = builder;
                _tcs = tcs;
            }

            public override void OnConfigured(CameraCaptureSession session)
            {
                _service._captureSession = session;
                try { session.SetRepeatingRequest(_builder.Build(), null, _service._cameraHandler); } catch { }
                _tcs.TrySetResult(true);
            }

            public override void OnConfigureFailed(CameraCaptureSession session)
            {
                _tcs.TrySetException(new Exception("Failed to configure camera capture session"));
            }
        }

        private class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
        {
            private readonly AndroidCameraService _service;
            private readonly CancellationToken _ct;

            public ImageAvailableListener(AndroidCameraService service, CancellationToken ct)
            {
                _service = service;
                _ct = ct;
            }

            public void OnImageAvailable(ImageReader reader)
            {
                if (_ct.IsCancellationRequested) return;

                var image = reader.AcquireLatestImage();
                if (image == null) return;

                try
                {
                    var skBitmap = ConvertYuvToSkBitmap(image);
                    if (skBitmap != null && _service._frameChannel != null)
                    {
                        if (!_service._frameChannel.Writer.TryWrite(skBitmap))
                            skBitmap.Dispose();
                    }
                }
                catch { }
                finally { image.Close(); }
            }

            private SKBitmap? ConvertYuvToSkBitmap(Android.Media.Image image)
            {
                try
                {
                    int width = image.Width;
                    int height = image.Height;
                    var yPlane = image.GetPlanes()[0];
                    var uPlane = image.GetPlanes()[1];
                    var vPlane = image.GetPlanes()[2];

                    var nv21 = Yuv420ToNv21(yPlane, uPlane, vPlane, width, height);

                    using var ms = new MemoryStream();
                    new YuvImage(nv21, ImageFormatType.Nv21, width, height, null)
                        .CompressToJpeg(new Android.Graphics.Rect(0, 0, width, height), 90, ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    var bitmap = BitmapFactory.DecodeStream(ms);
                    return bitmap?.Copy(Bitmap.Config.Argb8888, false).ToSKBitmap();
                }
                catch { return null; }
            }

            private static byte[] Yuv420ToNv21(Android.Media.Image.Plane yPlane, Android.Media.Image.Plane uPlane, Android.Media.Image.Plane vPlane, int width, int height)
            {
                int frameSize = width * height;
                int chromaHeight = height / 2;
                int chromaWidth = width / 2;

                byte[] nv21 = new byte[frameSize + 2 * chromaWidth * chromaHeight];
                yPlane.Buffer.Get(nv21, 0, yPlane.Buffer.Remaining());

                var uBuffer = new byte[uPlane.Buffer.Remaining()];
                var vBuffer = new byte[vPlane.Buffer.Remaining()];
                uPlane.Buffer.Get(uBuffer);
                vPlane.Buffer.Get(vBuffer);

                int uvIndex = frameSize;
                for (int row = 0; row < chromaHeight; row++)
                {
                    for (int col = 0; col < chromaWidth; col++)
                    {
                        int uPos = row * uPlane.RowStride + col * uPlane.PixelStride;
                        int vPos = row * vPlane.RowStride + col * vPlane.PixelStride;

                        nv21[uvIndex++] = vBuffer[vPos];
                        nv21[uvIndex++] = uBuffer[uPos];
                    }
                }

                return nv21;
            }
        }
    }

    internal static class BitmapExtensions
    {
        public static SKBitmap ToSKBitmap(this Bitmap bitmap)
        {
            var skBitmap = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            byte[] pixelData = new byte[bitmap.ByteCount];
            var buffer = Java.Nio.ByteBuffer.Wrap(pixelData);
            bitmap.CopyPixelsToBuffer(buffer);
            buffer.Rewind();
            System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, skBitmap.GetPixels(), pixelData.Length);
            return skBitmap;
        }
    }
}
#endif
