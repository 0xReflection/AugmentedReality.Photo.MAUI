#if ANDROID
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Util;
using Android.Views;
using Domain.Interfaces;
using Domain.Models;
using Java.Lang;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Presentation.Services
{
    public class AndroidCameraService : ICameraService
    {
        private readonly Context _context;
        private readonly ILogger<AndroidCameraService> _logger;
        private CameraManager _cameraManager;
        private CameraDevice _cameraDevice;
        private CameraCaptureSession _captureSession;
        private ImageReader _imageReader;
        private HandlerThread _backgroundThread;
        private Handler _backgroundHandler;
        private string _cameraId;
        private bool _disposed = false;
        private bool _isInitialized = false;

        private readonly Channel<SKBitmap> _frameChannel = Channel.CreateBounded<SKBitmap>(3);
        private readonly object _syncLock = new object();

        public bool IsInitialized => _isInitialized;

        public AndroidCameraService(Context context, ILogger<AndroidCameraService> logger = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AndroidCameraService));
            if (_isInitialized) return;

            lock (_syncLock)
            {
                if (_isInitialized) return;
            }

            try
            {
  
                _backgroundThread = new HandlerThread("CameraBackground");
                _backgroundThread.Start();
                _backgroundHandler = new Handler(_backgroundThread.Looper);

                _cameraManager = (CameraManager)_context.GetSystemService(Context.CameraService);
                if (_cameraManager == null)
                    throw new InvalidOperationException("Camera manager not available");

                _cameraId = GetBackCameraId();
                if (string.IsNullOrEmpty(_cameraId))
                    throw new InvalidOperationException("Back camera not found");
                _imageReader = ImageReader.NewInstance(640, 480, ImageFormatType.Yuv420888, 3);
                _imageReader.SetOnImageAvailableListener(new ImageAvailableListener(OnImageAvailable), _backgroundHandler);

                var tcs = new TaskCompletionSource<bool>();

                _cameraManager.OpenCamera(_cameraId, new CameraStateCallback(tcs, this), _backgroundHandler);

                await WaitForTaskWithTimeout(tcs.Task, TimeSpan.FromSeconds(10), "Camera initialization");

                if (!tcs.Task.Result)
                    throw new InvalidOperationException("Failed to open camera");

                lock (_syncLock)
                {
                    _isInitialized = true;
                }

                _logger?.LogInformation("Camera initialized successfully");
            }
            catch (Java.Lang.Exception ex)
            {
                _logger?.LogError(ex, "Camera initialization failed");
                await CleanupResources();
                throw;
            }
        }

        private async Task WaitForTaskWithTimeout(Task task, TimeSpan timeout, string operationName)
        {
            var delayTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(task, delayTask);

            if (completedTask == delayTask)
                throw new TimeoutException($"{operationName} timeout after {timeout.TotalSeconds} seconds");

            await task; 

        private string GetBackCameraId()
        {
            try
            {
                var cameraIds = _cameraManager.GetCameraIdList();
                foreach (var id in cameraIds)
                {
                    var characteristics = _cameraManager.GetCameraCharacteristics(id);
                    var lensFacing = (Integer)characteristics.Get(CameraCharacteristics.LensFacing);
                    if (lensFacing?.IntValue() == (int)LensFacing.Back)
                        return id;
                }
            }
            catch (Java.Lang.Exception ex)
            {
                _logger?.LogError(ex, "Error getting camera ID");
            }
            return null;
        }

        private void OnImageAvailable(ImageReader reader)
        {
            Android.Media.Image image = null;
            try
            {
                image = reader.AcquireLatestImage();
                if (image == null) return;

                var bitmap = ConvertYuvToSkBitmap(image);
                if (bitmap != null)
                {
                 
                    if (!_frameChannel.Writer.TryWrite(bitmap))
                    {
                        bitmap.Dispose();
                    }
                }
            }
            catch (Java.Lang.Exception ex)
            {
                _logger?.LogError(ex, "Error processing image");
            }
            finally
            {
                image?.Close();
                image?.Dispose();
            }
        }

        private SKBitmap ConvertYuvToSkBitmap(Android.Media.Image image)
        {
            if (image == null)
                return null;

            try
            {
                var planes = image.GetPlanes();
                if (planes == null || planes.Length < 3)
                    return null;

                var yPlane = planes[0];
                var uPlane = planes[1];
                var vPlane = planes[2];

                int width = image.Width;
                int height = image.Height;

                var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                if (bitmap.GetPixels() == IntPtr.Zero)
                    return null;

                // получение буферов
                var yBuffer = yPlane.Buffer;
                var uBuffer = uPlane.Buffer;
                var vBuffer = vPlane.Buffer;

                //  копии данных
                byte[] yArray = new byte[yBuffer.Remaining()];
                byte[] uArray = new byte[uBuffer.Remaining()];
                byte[] vArray = new byte[vBuffer.Remaining()];

                yBuffer.Get(yArray);
                uBuffer.Get(uArray);
                vBuffer.Get(vArray);

                int yRowStride = yPlane.RowStride;
                int uvRowStride = uPlane.RowStride;
                int uvPixelStride = uPlane.PixelStride;
                int yPixelStride = yPlane.PixelStride;

                unsafe
                {
                    byte* ptr = (byte*)bitmap.GetPixels().ToPointer();

                    for (int y = 0; y < height; y++)
                    {
                        int yRow = y * yRowStride;

                        for (int x = 0; x < width; x++)
                        {
                            // Y компонент
                            int yIndex = yRow + (x * yPixelStride);
                            if (yIndex >= yArray.Length) continue;
                            byte yValue = yArray[yIndex];

                            // UV компоненты
                            int uvX = x / 2;
                            int uvY = y / 2;
                            int uvIndex = (uvY * uvRowStride) + (uvX * uvPixelStride);

                            byte uValue = 128;
                            byte vValue = 128;

                            if (uvIndex < uArray.Length)
                                uValue = uArray[uvIndex];
                            if (uvIndex < vArray.Length)
                                vValue = vArray[uvIndex];

                            // YUV to RGB conversion 
                            int c = yValue - 16;
                            int d = uValue - 128;
                            int e = vValue - 128;

                            int r = Java.Lang.Math.Clamp((298 * c + 409 * e + 128) >> 8, 0, 255);
                            int g = Java.Lang.Math.Clamp((298 * c - 100 * d - 208 * e + 128) >> 8, 0, 255);
                            int b = Java.Lang.Math.Clamp((298 * c + 516 * d + 128) >> 8, 0, 255);

                            int pixelIndex = (y * width + x) * 4;
                            ptr[pixelIndex] = (byte)b;     // Blue
                            ptr[pixelIndex + 1] = (byte)g; // Green
                            ptr[pixelIndex + 2] = (byte)r; // Red
                            ptr[pixelIndex + 3] = 255;     // Alpha
                        }
                    }
                }

                return bitmap;
            }
            catch (Java.Lang.Exception ex)
            {
                _logger?.LogError(ex, "Error converting YUV to bitmap");
                return null;
            }
        }

        public async IAsyncEnumerable<SKBitmap> GetFrameStream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Camera not initialized");

            await foreach (var frame in _frameChannel.Reader.ReadAllAsync(ct))
            {
                if (ct.IsCancellationRequested || _disposed)
                {
                    frame?.Dispose();
                    yield break;
                }

                if (frame != null && !frame.IsNull)
                {
                    yield return frame;
                }
            }
        }

        public async Task<Photo?> CaptureAsync(CancellationToken ct = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Camera not initialized");

            try
            {
                var frame = await _frameChannel.Reader.ReadAsync(ct);
                return new Photo(frame);
            }
            catch (Java.Lang.Exception ex)
            {
                _logger?.LogError(ex, "Error capturing photo");
                return null;
            }
        }

        public async Task StopAsync()
        {
            bool wasInitialized;
            lock (_syncLock)
            {
                wasInitialized = _isInitialized;
                _isInitialized = false;
            }

            if (!wasInitialized) return;

            try
            {
                _captureSession?.StopRepeating();
                _captureSession?.Close();
                _captureSession = null;

                _cameraDevice?.Close();
                _cameraDevice = null;

                _imageReader?.Close();
                _imageReader = null;

                _backgroundThread?.QuitSafely();
                _backgroundThread = null;
                _backgroundHandler = null;

              
                while (_frameChannel.Reader.TryRead(out var frame))
                {
                    frame?.Dispose();
                }

                _logger?.LogInformation("Camera stopped successfully");
            }
            catch (Java.Lang.Exception ex)
            {
                _logger?.LogError(ex, "Error stopping camera");
            }
        }

        private async Task CleanupResources()
        {
            try
            {
                await StopAsync();
            }
            catch (Java.Lang.Exception ex)
            {
                _logger?.LogError(ex, "Error during cleanup");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            Task.Run(async () =>
            {
                try
                {
                    await CleanupResources();
                }
                catch (Java.Lang.Exception ex)
                {
                    _logger?.LogError(ex, "Error during disposal");
                }
            }).ConfigureAwait(false);

            _frameChannel.Writer.TryComplete();
            GC.SuppressFinalize(this);
        }

        // Camera2 callback classes
        private class CameraStateCallback : CameraDevice.StateCallback
        {
            private readonly TaskCompletionSource<bool> _tcs;
            private readonly AndroidCameraService _service;

            public CameraStateCallback(TaskCompletionSource<bool> tcs, AndroidCameraService service)
            {
                _tcs = tcs;
                _service = service;
            }

            public override void OnOpened(CameraDevice camera)
            {
                _service._cameraDevice = camera;
                try
                {
                    CreateCaptureSession();
                }
                catch (Java.Lang.Exception ex)
                {
                    _service._logger?.LogError(ex, "Error creating capture session");
                    _tcs.TrySetException(ex);
                }
            }

            public override void OnDisconnected(CameraDevice camera)
            {
                try
                {
                    camera.Close();
                }
                catch (Java.Lang.Exception) { }

                _service._cameraDevice = null;
                _tcs.TrySetResult(false);
            }

            public override void OnError(CameraDevice camera, CameraError error)
            {
                try
                {
                    camera.Close();
                }
                catch (Java.Lang.Exception) { }

                _service._cameraDevice = null;
                _tcs.TrySetException(new InvalidOperationException($"Camera error: {error}"));
            }

            private void CreateCaptureSession()
            {
                try
                {
                    var surfaces = new List<Surface> { _service._imageReader.Surface };
                    _service._cameraDevice.CreateCaptureSession(surfaces,
                        new CaptureSessionCallback(_service, _tcs),
                        _service._backgroundHandler);
                }
                catch (Java.Lang.Exception ex)
                {
                    _tcs.TrySetException(ex);
                }
            }
        }

        private class CaptureSessionCallback : CameraCaptureSession.StateCallback
        {
            private readonly AndroidCameraService _service;
            private readonly TaskCompletionSource<bool> _tcs;

            public CaptureSessionCallback(AndroidCameraService service, TaskCompletionSource<bool> tcs)
            {
                _service = service;
                _tcs = tcs;
            }

            public override void OnConfigured(CameraCaptureSession session)
            {
                _service._captureSession = session;

                try
                {
                    var requestBuilder = _service._cameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
                    requestBuilder.AddTarget(_service._imageReader.Surface);
                    var fpsRange = new Android.Util.Range(30, 30);
                    requestBuilder.Set(CaptureRequest.ControlAeTargetFpsRange, fpsRange);

                    session.SetRepeatingRequest(requestBuilder.Build(), null, _service._backgroundHandler);

                    _service._logger?.LogInformation("Camera capture session configured");
                    _tcs.TrySetResult(true);
                }
                catch (Java.Lang.Exception ex)
                {
                    _service._logger?.LogError(ex, "Error configuring capture session");
                    _tcs.TrySetException(ex);
                }
            }

            public override void OnConfigureFailed(CameraCaptureSession session)
            {
                _service._logger?.LogError("Camera capture session configuration failed");
                _tcs.TrySetException(new InvalidOperationException("Capture session configuration failed"));
            }
        }

        private class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
        {
            private readonly Action<ImageReader> _onImageAvailable;

            public ImageAvailableListener(Action<ImageReader> onImageAvailable)
            {
                _onImageAvailable = onImageAvailable;
            }

            public void OnImageAvailable(ImageReader reader)
            {
                _onImageAvailable(reader);
            }
        }
    }
}
#endif