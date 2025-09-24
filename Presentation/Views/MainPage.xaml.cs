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
        private SKPaint _backgroundPaint;
        private SKTypeface _typeface;
        private bool _isDisposed = false;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
            InitializeSkiaResources();
        }

        private void InitializeSkiaResources()
        {
            // Фон для текста
            _backgroundPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 128), // Полупрозрачный черный
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            try
            {
                // Используем системные шрифты Android
                _typeface = SKTypeface.FromFamilyName("sans-serif",
                    SKFontStyleWeight.Bold,
                    SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright);
            }
            catch
            {
                try
                {
                    // Fallback на Roboto
                    _typeface = SKTypeface.FromFamilyName("Roboto",
                        SKFontStyleWeight.Normal,
                        SKFontStyleWidth.Normal,
                        SKFontStyleSlant.Upright);
                }
                catch
                {
                    // Ultimate fallback
                    _typeface = SKTypeface.Default;
                }
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Запускаем обновление обоих canvas
            Device.StartTimer(TimeSpan.FromMilliseconds(16), () =>
            {
                if (!_isDisposed)
                {
                    // Всегда обновляем основной кадр
                    CameraFrameView.InvalidateSurface();

                    // Overlay обновляем только при детекции
                    if (_viewModel.IsDetecting)
                    {
                        DetectionOverlay.InvalidateSurface();
                    }
                }
                return !_isDisposed;
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

            // Автоматически останавливаем детекцию при скрытии страницы
            if (_viewModel.IsDetecting)
            {
                _ = _viewModel.ToggleDetectionCommand.ExecuteAsync(null);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Обновляем основной кадр при изменении CurrentFrame или статуса детекции
            if (e.PropertyName == nameof(MainViewModel.CurrentFrame) ||
                e.PropertyName == nameof(MainViewModel.IsDetecting))
            {
                CameraFrameView.InvalidateSurface();
            }

            // Обновляем overlay при изменении результатов детекции
            if (e.PropertyName == nameof(MainViewModel.DetectionResult) ||
                e.PropertyName == nameof(MainViewModel.IsPersonDetected) ||
                e.PropertyName == nameof(MainViewModel.DetectionConfidence) ||
                e.PropertyName == nameof(MainViewModel.CurrentFps) ||
                e.PropertyName == nameof(MainViewModel.ProcessingTime))
            {
                DetectionOverlay.InvalidateSurface();
            }
        }

        private void OnCameraFramePaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            if (_isDisposed) return;

            var canvas = e.Surface.Canvas;
            var info = e.Info;

            // Очищаем canvas
            canvas.Clear(SKColors.Black);

            try
            {
                // Отображаем текущий кадр с камеры, если он есть
                if (_viewModel.CurrentFrame != null && !_viewModel.CurrentFrame.IsNull)
                {
                    DrawCameraFrame(canvas, info, _viewModel.CurrentFrame);
                }
                else
                {
                    // Показываем сообщение, если кадра нет
                    DrawStatusMessage(canvas, info);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка отрисовки кадра: {ex.Message}");
                DrawErrorMessage(canvas, info, "Ошибка отображения");
            }
        }

        private void DrawCameraFrame(SKCanvas canvas, SKImageInfo info, SKBitmap frame)
        {
            // Рассчитываем масштабирование для сохранения пропорций
            float scaleX = (float)info.Width / frame.Width;
            float scaleY = (float)info.Height / frame.Height;
            float scale = Math.Min(scaleX, scaleY);

            float scaledWidth = frame.Width * scale;
            float scaledHeight = frame.Height * scale;
            float x = (info.Width - scaledWidth) / 2;
            float y = (info.Height - scaledHeight) / 2;

            var destRect = new SKRect(x, y, x + scaledWidth, y + scaledHeight);

            // Рисуем кадр с высоким качеством
            using var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.High,
                IsAntialias = true
            };

            canvas.DrawBitmap(frame, destRect, paint);

            // Если детекция не активна, показываем сообщение
            if (!_viewModel.IsDetecting)
            {
                DrawCenteredText(canvas, info, "Нажмите 'Запустить детекцию'", 20, SKColors.White);
            }
        }

        private void DrawStatusMessage(SKCanvas canvas, SKImageInfo info)
        {
            canvas.Clear(SKColors.Black);

            if (_viewModel.IsProcessing)
            {
                DrawCenteredText(canvas, info, "Загрузка камеры...", 24, SKColors.White);
            }
            else
            {
                DrawCenteredText(canvas, info, "Камера не активирована", 24, SKColors.White);
            }
        }

        private void DrawErrorMessage(SKCanvas canvas, SKImageInfo info, string message)
        {
            canvas.Clear(SKColors.Black);
            DrawCenteredText(canvas, info, message, 20, SKColors.Red);
        }

        private void OnDetectionPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            if (_isDisposed || !_viewModel.IsDetecting) return;

            var canvas = e.Surface.Canvas;
            // Очищаем прозрачным цветом для overlay
            canvas.Clear(SKColors.Transparent);

            try
            {
                if (_viewModel.IsPersonDetected && _viewModel.DetectionResult?.Human != null)
                {
                    DrawDetectionInfo(canvas, e.Info, _viewModel.DetectionResult.Human.Confidence);
                }

                DrawPerformanceInfo(canvas, e.Info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка отрисовки детекции: {ex.Message}");
            }
        }

        private void DrawDetectionInfo(SKCanvas canvas, SKImageInfo info, float confidence)
        {
            try
            {
                var confidenceText = $"Уверенность: {confidence:P0}";
                var qualityText = _viewModel.DetectionQuality;
                var statusText = "ЧЕЛОВЕК ОБНАРУЖЕН";

                // Рисуем статус детекции
                DrawTextWithBackground(canvas, statusText, 10, 40, 18, SKColors.LightGreen);

                // Рисуем уверенность
                DrawTextWithBackground(canvas, confidenceText, 10, 70, 16, SKColors.White);

                // Рисуем качество
                if (!string.IsNullOrEmpty(qualityText))
                {
                    var qualityColor = confidence switch
                    {
                        > 0.8f => SKColors.LightGreen,
                        > 0.6f => SKColors.Yellow,
                        > 0.4f => SKColors.Orange,
                        _ => SKColors.LightCoral
                    };
                    DrawTextWithBackground(canvas, qualityText, 10, 100, 14, qualityColor);
                }

                // Рисуем индикатор уверенности
                DrawConfidenceBar(canvas, info, confidence);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка отрисовки детекции: {ex.Message}");
            }
        }

        private void DrawPerformanceInfo(SKCanvas canvas, SKImageInfo info)
        {
            try
            {
                var fpsText = $"FPS: {_viewModel.CurrentFps:F1}";
                var targetFpsText = $"Цель: {_viewModel.TargetFps} FPS";
                var processingText = _viewModel.ProcessingTime;

                var yPos = info.Height - 80;

                // FPS информация
                DrawTextWithBackground(canvas, fpsText, 10, yPos, 14, SKColors.White);
                DrawTextWithBackground(canvas, targetFpsText, 10, yPos + 25, 12, SKColors.LightGray);
                DrawTextWithBackground(canvas, processingText, 10, yPos + 45, 12, SKColors.LightGray);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка отрисовки производительности: {ex.Message}");
            }
        }

        private void DrawTextWithBackground(SKCanvas canvas, string text, float x, float y, float textSize, SKColor textColor)
        {
            try
            {
                using var textPaint = new SKPaint
                {
                    Color = textColor,
                    TextSize = textSize,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                    TextAlign = SKTextAlign.Left,
                    Typeface = _typeface
                };

                // Измеряем текст
                var textBounds = new SKRect();
                textPaint.MeasureText(text, ref textBounds);

                // Корректируем позицию Y для правильного отображения
                float actualY = y - textBounds.Top;

                var backgroundRect = new SKRect(
                    x - 8,
                    y - textBounds.Height - 4,
                    x + textBounds.Width + 8,
                    y + 4
                );

                // Рисуем фон с закругленными углами
                canvas.DrawRoundRect(backgroundRect, 6, 6, _backgroundPaint);

                // Рисуем текст
                canvas.DrawText(text, x, actualY, textPaint);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка отрисовки текста: {ex.Message}");
            }
        }

        private void DrawCenteredText(SKCanvas canvas, SKImageInfo info, string text, float textSize, SKColor color)
        {
            try
            {
                using var textPaint = new SKPaint
                {
                    Color = color,
                    TextSize = textSize,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                    TextAlign = SKTextAlign.Center,
                    Typeface = _typeface
                };

                var textBounds = new SKRect();
                textPaint.MeasureText(text, ref textBounds);

                float x = info.Width / 2;
                float y = info.Height / 2 - textBounds.MidY;

                canvas.DrawText(text, x, y, textPaint);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка отрисовки центрированного текста: {ex.Message}");
            }
        }

        private void DrawConfidenceBar(SKCanvas canvas, SKImageInfo info, float confidence)
        {
            try
            {
                var barWidth = info.Width - 40;
                var barHeight = 12;
                var barX = 20;
                var barY = 130;

                // Фон бара
                var backgroundRect = new SKRect(barX, barY, barX + barWidth, barY + barHeight);
                using var backgroundPaint = new SKPaint
                {
                    Color = new SKColor(100, 100, 100, 200),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                canvas.DrawRoundRect(backgroundRect, 6, 6, backgroundPaint);

                // Заполненная часть
                var fillWidth = barWidth * confidence;
                if (fillWidth > 0)
                {
                    var fillRect = new SKRect(barX, barY, barX + fillWidth, barY + barHeight);

                    var fillColor = confidence switch
                    {
                        > 0.8f => new SKColor(76, 175, 80),   // Green
                        > 0.6f => new SKColor(255, 193, 7),   // Yellow
                        > 0.4f => new SKColor(255, 152, 0),   // Orange
                        _ => new SKColor(244, 67, 54)         // Red
                    };

                    using var fillPaint = new SKPaint
                    {
                        Color = fillColor,
                        Style = SKPaintStyle.Fill,
                        IsAntialias = true
                    };

                    canvas.DrawRoundRect(fillRect, 6, 6, fillPaint);
                }

                // Текст процентов
                var percentText = $"{confidence:P0}";
                DrawTextWithBackground(canvas, percentText, barX + barWidth + 10, barY + barHeight / 2 + 4, 12,
                    confidence > 0.6f ? SKColors.White : SKColors.LightGray);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка отрисовки бара: {ex.Message}");
            }
        }

        private void OnTargetFpsSliderChanged(object sender, ValueChangedEventArgs e)
        {
            if (sender is Slider slider)
            {
                var fps = (int)slider.Value;
                _viewModel.ChangeTargetFpsCommand.Execute(fps);
            }
        }

        private void OnCapturePhotoClicked(object sender, EventArgs e)
        {
            _ = _viewModel.CapturePhotoCommand.ExecuteAsync(null);
        }

        private void OnRestartCameraClicked(object sender, EventArgs e)
        {
            _ = _viewModel.RestartCameraCommand.ExecuteAsync(null);
        }

        protected override void OnHandlerChanging(HandlerChangingEventArgs args)
        {
            base.OnHandlerChanging(args);

            if (args.OldHandler != null)
            {
                CleanupResources();
            }
        }

        private void CleanupResources()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _backgroundPaint?.Dispose();
            _typeface?.Dispose();
        }

        ~MainPage()
        {
            CleanupResources();
        }
    }
}