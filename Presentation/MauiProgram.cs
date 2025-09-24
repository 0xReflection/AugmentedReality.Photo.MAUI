
#if ANDROID
using Android.Content;
using AndroidX.Lifecycle;
using AppUseCase.UseCases;
using CommunityToolkit.Maui;
using Domain.Interfaces;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Presentation.Platforms.Android;
using Presentation.Services;
using Presentation.ViewModel;
using Presentation.Views;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls.Hosting;
using System.Threading.Channels;

namespace Presentation
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>().UseSkiaSharp().ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("NotoSerif-Bold.ttf", "NotoSerifBold");
                fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
                fonts.AddFont("Poppins-SemiBold.ttf", "PoppinsSemibold");
                fonts.AddFont("Poppins-Regular.ttf", "Poppins");
                fonts.AddFont("MaterialIconsOutlined-Regular.otf", "Material");
            }).UseMauiCommunityToolkit().UseMauiCommunityToolkitCamera();
#if DEBUG
            builder.Logging.AddDebug();
#endif
            RegisterServices(builder.Services);
            RegisterViewsAndViewModels(builder.Services);
            return builder.Build();
        }

        private static void RegisterServices(IServiceCollection services)
        {

            services.AddSingleton<ICameraService, AndroidCameraService>();
            services.AddSingleton<Context>(provider => Android.App.Application.Context);
            services.AddSingleton<ILifecycleOwner>(provider =>
            {
                var activity = MainActivity.Instance;
                if (activity == null)
                {
                    throw new InvalidOperationException("MainActivity not initialized. Wait for activity to be created.");
                }

                return activity;
            });
            var aiChannel = Channel.CreateUnbounded<SKBitmap>();

            services.AddSingleton(aiChannel);             // сам канал
            services.AddSingleton(aiChannel.Reader);      // для RealTimeDetectionService
            services.AddSingleton(aiChannel.Writer);      // для CameraService/FrameDispatch
            services.AddSingleton<IFrameDispatcherService, FrameDispatcherService>();
            services.AddSingleton<IObjectDetectionService, ObjectDetectionService>();
            services.AddSingleton<IRealTimeDetectionService, RealTimeDetectionService>();
            services.AddSingleton<IStorageService, StorageService>();
            services.AddSingleton<IQrService, QrService>();
            services.AddSingleton<IArService, ArService>();
            services.AddTransient<IDetectObjectsUseCase>(sp => new DetectObjectsUseCase(sp.GetRequiredService<IObjectDetectionService>(), sp.GetRequiredService<ILogger<DetectObjectsUseCase>>()));
            services.AddTransient<CapturePhotoUseCase>(sp => new CapturePhotoUseCase(sp.GetRequiredService<ICameraService>(), sp.GetRequiredService<IStorageService>()));
            //services.AddTransient<LaunchArUseCase>(sp =>
            //    new LaunchArUseCase(
            //        sp.GetRequiredService<IArService>(),
            //        sp.GetRequiredService<IObjectDetectionService>()
            //    ));
            //services.AddTransient<ScanQrUseCase>(sp =>
            //     new ScanQrUseCase(sp.GetRequiredService<IQrService>()));
        

        }

        private static void RegisterViewsAndViewModels(IServiceCollection services)
        {
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainPage>();
            services.AddTransient<CharacterPage>();
        }
    }
}
#endif