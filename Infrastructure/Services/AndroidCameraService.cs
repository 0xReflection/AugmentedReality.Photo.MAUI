
using Android.Content;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Views;
using SkiaSharp;

namespace Infrastructure.Services
{
    public class AndroidCameraService : CameraService
    {
        private readonly Context _context;
        private CameraDevice? _cameraDevice;
        private CameraCaptureSession? _captureSession;
        private ImageReader? _imageReader;
        private SKBitmap? _latestFrame;
        private readonly object _frameLock = new();

        public AndroidCameraService()
        {
            _context = Android.App.Application.Context;
        }

        public override async Task InitializeAsync()
        {
            if (_isInitialized || _disposed) return;

            var cameraManager = (CameraManager)_context.GetSystemService(Context.CameraService)!;
            var cameraId = GetCameraId(cameraManager);

            var tcs = new TaskCompletionSource();
            var callback = new CameraStateCallback(tcs);

            cameraManager.OpenCamera(cameraId, callback, null);
            await tcs.Task;
            _cameraDevice = callback.Device;

            _imageReader = ImageReader.NewInstance(640, 480, ImageFormatType.Yuv420888, 2);
            _imageReader.SetOnImageAvailableListener(new ImageAvailableListener(this), null);

            var sessionTcs = new TaskCompletionSource();
            var surfaces = new List<Surface> { _imageReader.Surface! };
            _cameraDevice.CreateCaptureSession(surfaces, new CaptureSessionCallback(sessionTcs), null);
            await sessionTcs.Task;

            _isInitialized = true;
        }

        public override async Task StopAsync()
        {
            if (!_isInitialized || _disposed) return;

            _captureSession?.Close();
            _captureSession = null;

            _imageReader?.Close();
            _imageReader = null;

            _cameraDevice?.Close();
            _cameraDevice = null;

            _isInitialized = false;
            await Task.CompletedTask;
        }

        public override async IAsyncEnumerable<SKBitmap> GetFrameStream(
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (!_isInitialized || _disposed)
                await InitializeAsync();

            while (!ct.IsCancellationRequested && !_disposed)
            {
                SKBitmap? frame;
                lock (_frameLock)
                {
                    frame = _latestFrame;
                    _latestFrame = null;
                }

                if (frame != null)
                    yield return frame;

                await Task.Delay(16, ct);
            }
        }

        public override Task<Photo?> CaptureAsync(CancellationToken ct = default)
        {
            // Реализация захвата фото
            return Task.FromResult<Photo?>(null);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Освобождаем управляемые ресурсы
                    StopAsync().Wait(2000);
                    
                    _imageReader?.Dispose();
                    _cameraDevice?.Dispose();
                    _captureSession?.Dispose();
                    
                    lock (_frameLock)
                    {
                        _latestFrame?.Dispose();
                        _latestFrame = null;
                    }
                }
                
                base.Dispose(disposing);
            }
        }

        private void OnFrameReceived(SKBitmap frame)
        {
            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = frame;
            }
        }

        private string GetCameraId(CameraManager cameraManager)
        {
            var cameraIds = cameraManager.GetCameraIdList();
            foreach (var id in cameraIds)
            {
                var characteristics = cameraManager.GetCameraCharacteristics(id);
                var facing = (LensFacing?)characteristics.Get(CameraCharacteristics.LensFacing);
                if (facing == LensFacing.Back)
                    return id;
            }
            return cameraIds[0];
        }

        private class CameraStateCallback : CameraDevice.StateCallback
        {
            private readonly TaskCompletionSource _tcs;
            public CameraDevice? Device { get; private set; }

            public CameraStateCallback(TaskCompletionSource tcs) => _tcs = tcs;

            public override void OnOpened(CameraDevice camera)
            {
                Device = camera;
                _tcs.TrySetResult();
            }

            public override void OnDisconnected(CameraDevice camera) => _tcs.TrySetException(new Exception("Camera disconnected"));
            public override void OnError(CameraDevice camera, CameraError error) => _tcs.TrySetException(new Exception($"Camera error: {error}"));
        }

        private class CaptureSessionCallback : CameraCaptureSession.StateCallback
        {
            private readonly TaskCompletionSource _tcs;
            public CaptureSessionCallback(TaskCompletionSource tcs) => _tcs = tcs;

            public override void OnConfigured(CameraCaptureSession session) => _tcs.TrySetResult();
            public override void OnConfigureFailed(CameraCaptureSession session) => _tcs.TrySetException(new Exception("Camera session configuration failed"));
        }

        private class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
        {
            private readonly AndroidCameraService _service;

            public ImageAvailableListener(AndroidCameraService service) => _service = service;

            public void OnImageAvailable(ImageReader reader)
            {
                using var image = reader.AcquireLatestImage();
                if (image == null) return;

                try
                {
                    var bitmap = ConvertYuvToBitmap(image);
                    if (bitmap != null)
                        _service.OnFrameReceived(bitmap);
                }
                finally
                {
                    image.Close();
                }
            }

            private SKBitmap ConvertYuvToBitmap(Image image)
            {
                // Упрощенная конвертация YUV -> RGB
                var yPlane = image.GetPlanes()[0];
                var uPlane = image.GetPlanes()[1];
                var vPlane = image.GetPlanes()[2];

                var yBuffer = yPlane.Buffer;
                var uBuffer = uPlane.Buffer;
                var vBuffer = vPlane.Buffer;

                var width = image.Width;
                var height = image.Height;

                var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
                using var pixmap = bitmap.PeekPixels();

                // Здесь должна быть реальная конвертация YUV -> RGB
                // Для демо просто создаем черный кадр
                pixmap.Erase(SKColors.Black);

                return bitmap;
            }
        }
    }
}
