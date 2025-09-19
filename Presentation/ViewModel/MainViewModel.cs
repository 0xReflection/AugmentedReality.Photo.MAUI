
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Presentation.ViewModel;

public partial class MainViewModel : ObservableObject
{
    private double _zoom = 1;
    public double Zoom
    {
        get => _zoom;
        set => SetProperty(ref _zoom, value);
    }

    [RelayCommand]
    public async Task CaptureImageAsync(object cameraObject)
    {
        if (cameraObject is not CommunityToolkit.Maui.Views.CameraView cameraView)
            return;

        try
        {
            using var stream = await cameraView.CaptureImage(CancellationToken.None);
            if (stream == null) return;

            string filePath;

#if ANDROID
            var picturesPath = Android.OS.Environment.GetExternalStoragePublicDirectory(
                Android.OS.Environment.DirectoryPictures).AbsolutePath;
            filePath = Path.Combine(picturesPath, $"captured_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
#elif IOS
            filePath = Path.Combine(FileSystem.CacheDirectory, $"captured_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
#elif WINDOWS
            var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            filePath = Path.Combine(picturesPath, $"captured_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
#else
            filePath = Path.Combine(FileSystem.CacheDirectory, $"captured_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
#endif

            using (var fileStream = File.OpenWrite(filePath))
                await stream.CopyToAsync(fileStream);

#if ANDROID
            // Обновляем галерею
            var mediaScanIntent = new Android.Content.Intent(Android.Content.Intent.ActionMediaScannerScanFile);
            var contentUri = Android.Net.Uri.FromFile(new Java.IO.File(filePath));
            mediaScanIntent.SetData(contentUri);
            Android.App.Application.Context.SendBroadcast(mediaScanIntent);
#elif IOS
            UIKit.UIImage.FromFile(filePath).SaveToPhotosAlbum((img, error) =>
            {
                if (error != null)
                    Console.WriteLine("Ошибка сохранения: " + error.LocalizedDescription);
            });
#endif

            var page = Application.Current?.Windows[0]?.Page;
            if (page != null)
                await page.DisplayAlert("Фото", $"Сохранено: {filePath}", "OK");
        }
        catch (Exception ex)
        {
            var page = Application.Current?.Windows[0]?.Page;
            if (page != null)
                await page.DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }
}
