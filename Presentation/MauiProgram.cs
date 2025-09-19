using Microsoft.Extensions.Logging;
using Presentation.ViewModel;
using CommunityToolkit.Maui;
using Presentation.Views;
using Domain.Interfaces;
using Infrastructure.Services;
using AppUseCase.UseCases;


namespace Presentation
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>().UseMauiCommunityToolkitMediaElement().UseMauiCommunityToolkitCamera().RegisterViewModels().ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("NotoSerif-Bold.ttf", "NotoSerifBold");
                fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
                fonts.AddFont("Poppins-SemiBold.ttf", "PoppinsSemibold");
                fonts.AddFont("Poppins-Regular.ttf", "Poppins");
                fonts.AddFont("MaterialIconsOutlined-Regular.otf", "Material");
            }).UseMauiCommunityToolkit();
#if DEBUG
            builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<ICameraService, CameraService>();
            builder.Services.AddSingleton<IStorageService, StorageService>();
            builder.Services.AddSingleton<IQrService, QrService>();
            builder.Services.AddSingleton<IArService, ArService>();
            builder.Services.AddTransient<CapturePhotoUseCase>();
            builder.Services.AddTransient<LaunchArUseCase>();
            builder.Services.AddTransient<ScanQrUseCase>();
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<MainPage>();
           
#if WINDOWS
            // builder.Services.AddSingleton<IPlatformService, WindowsPlatformService>();
#endif
#if ANDROID
            // builder.Services.AddSingleton<IPlatformService, AndroidPlatformService>();
#endif
#if IOS
            // builder.Services.AddSingleton<IPlatformService, IosPlatformService>();
#endif
            return builder.Build();
        }

        public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder mauiAppBuilder)
        {
            mauiAppBuilder.Services.AddTransient<MainViewModel>();
            mauiAppBuilder.Services.AddTransient<MainPage>();
            return mauiAppBuilder;
        }
    }
}