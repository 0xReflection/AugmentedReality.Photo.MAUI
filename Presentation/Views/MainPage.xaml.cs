using Presentation.ViewModel;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Presentation.Views
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _vm;

        public MainPage(MainViewModel vm)
        {
            InitializeComponent();
            BindingContext = _vm = vm;

            Device.StartTimer(TimeSpan.FromMilliseconds(16), () =>
            {
                CameraCanvas.InvalidateSurface();
                return true;
            });
        }

        private void CameraCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);

            var frame = _vm.LatestFrame;
            if (frame != null)
            {
                var info = e.Info;
                var scaleX = (float)info.Width / frame.Width;
                var scaleY = (float)info.Height / frame.Height;
                var scale = Math.Min(scaleX, scaleY);

                var destWidth = frame.Width * scale;
                var destHeight = frame.Height * scale;
                var left = (info.Width - destWidth) / 2f;
                var top = (info.Height - destHeight) / 2f;

                var destRect = new SKRect(left, top, left + destWidth, top + destHeight);
                canvas.DrawBitmap(frame, destRect);
            }
        }
    }
}