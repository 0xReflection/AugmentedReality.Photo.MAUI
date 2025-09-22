#if WINDOWS
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using SkiaSharp;
using Microsoft.UI.Xaml.Controls;

namespace Infrastructure.Services
{
    public class WindowsCameraService : CameraService
    {
        private MediaCapture? _mediaCapture;
        private readonly SemaphoreSlim _captureLock = new(1, 1);

        public override async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _captureLock.WaitAsync();
            try
            {
                _mediaCapture = new MediaCapture();
                await _mediaCapture.InitializeAsync();
                _isInitialized = true;
            }
            finally
            {
                _captureLock.Release();
            }
        }

        public override async Task StopAsync()
        {
            await _captureLock.WaitAsync();
            try
            {
                _mediaCapture?.Dispose();
                _mediaCapture = null;
                _isInitialized = false;
            }
            finally
            {
                _captureLock.Release();
            }
        }

        public override async IAsyncEnumerable<SKBitmap> GetFrameStream(
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (!_isInitialized)
                await InitializeAsync();

            var props = _mediaCapture?.VideoDeviceController
                .GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;

            int width = (int)(props?.Width ?? 640);
            int height = (int)(props?.Height ?? 480);

            while (!ct.IsCancellationRequested)
            {
                await _captureLock.WaitAsync(ct);
                try
                {
                    if (_mediaCapture == null) yield break;

                    using var frame = new VideoFrame(BitmapPixelFormat.Bgra8, width, height);
                    var previewFrame = await _mediaCapture.GetPreviewFrameAsync(frame);

                    if (previewFrame?.SoftwareBitmap is { } softwareBitmap)
                    {
                        var skBitmap = await ConvertToSkBitmapAsync(softwareBitmap);
                        if (skBitmap != null)
                            yield return skBitmap;
                    }
                }
                finally
                {
                    _captureLock.Release();
                }

                await Task.Delay(33, ct); // ~30 FPS
            }
        }

        private async Task<SKBitmap?> ConvertToSkBitmapAsync(SoftwareBitmap softwareBitmap)
        {
            try
            {
                var bitmap = new SKBitmap(softwareBitmap.PixelWidth, softwareBitmap.PixelHeight, 
                    SKColorType.Bgra8888, SKAlphaType.Premul);

                using var pixmap = bitmap.PeekPixels();
                var buffer = pixmap.GetPixels();

                await Task.Run(() =>
                {
                    softwareBitmap.CopyToBuffer(buffer);
                });

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await StopAsync();
            _captureLock.Dispose();
            await base.DisposeAsync();
        }
    }
}
#endif