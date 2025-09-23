using Microsoft.Maui.Controls;
using Presentation.ViewModel;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using System;

namespace Presentation.Views
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _viewModel;
        private SKPaint _textPaint;
        private SKTypeface _typeface;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
            InitializeSkiaResources();
        }

        private void InitializeSkiaResources()
        {
            _textPaint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = 24,
                IsAntialias = true
            };

            try
            {
                _typeface = SKTypeface.FromFamilyName("Poppins", SKFontStyle.Bold);
                _textPaint.Typeface = _typeface;
            }
            catch
            {
                _textPaint.Typeface = SKTypeface.Default;
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            Device.StartTimer(TimeSpan.FromMilliseconds(16), () =>
            {
                if (_viewModel.IsDetecting)
                {
                    DetectionOverlay.InvalidateSurface();
                }
                return _viewModel.IsDetecting;
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

            if (_viewModel.IsDetecting)
                _ = _viewModel.ToggleDetectionCommand.ExecuteAsync(null);
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.DetectionResult) ||
                e.PropertyName == nameof(MainViewModel.IsPersonDetected))
            {
                DetectionOverlay.InvalidateSurface();
            }
        }

        private void OnDetectionPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear();

            if (_viewModel.IsPersonDetected && _viewModel.DetectionResult?.Human != null)
            {
                DrawDetectionConfidence(canvas, e.Info, _viewModel.DetectionResult.Human.Confidence);
            }
        }

        private void DrawDetectionConfidence(SKCanvas canvas, SKImageInfo info, float confidence)
        {
            try
            {
                var text = $"Человек: {confidence:P0}";
                var textBounds = new SKRect();
                _textPaint.MeasureText(text, ref textBounds);

                var x = 10;
                var y = 30;

                using var backgroundPaint = new SKPaint
                {
                    Color = SKColors.Black.WithAlpha(0x7F),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };

                var textBackground = new SKRect(
                    x - 5,
                    y - textBounds.Height - 5,
                    x + textBounds.Width + 5,
                    y + 5
                );

                canvas.DrawRoundRect(textBackground, 5, 5, backgroundPaint);
                canvas.DrawText(text, x, y, _textPaint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отрисовки уверенности: {ex.Message}");
            }
        }

        private void OnTargetFpsChanged(object sender, ValueChangedEventArgs e)
        {
            _viewModel.ChangeTargetFpsCommand.Execute((int)e.NewValue);
        }

        protected override void OnHandlerChanging(HandlerChangingEventArgs args)
        {
            base.OnHandlerChanging(args);

            if (args.OldHandler != null)
            {
                _textPaint?.Dispose();
                _typeface?.Dispose();
            }
        }
    }
}
