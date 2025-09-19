using Microsoft.Extensions.Logging;
using Presentation.ViewModel;
using CommunityToolkit.Maui;
using Presentation.Views;

namespace Presentation
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
//#pragma warning disable MCT001 // `.UseMauiCommunityToolkit()` Not Found on MauiAppBuilder
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkitCamera()
                .RegisterViewModels()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("NotoSerif-Bold.ttf", "NotoSerifBold");
                    fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
                    fonts.AddFont("Poppins-SemiBold.ttf", "PoppinsSemibold");
                    fonts.AddFont("Poppins-Regular.ttf", "Poppins");
                    fonts.AddFont("MaterialIconsOutlined-Regular.otf", "Material");

                });
//#pragma warning restore MCT001 // `.UseMauiCommunityToolkit()` Not Found on MauiAppBuilder

#if DEBUG
            builder.Logging.AddDebug();
            //builder.Services.AddSingleton<ICameraService, CameraService>();
#endif

            return builder.Build();
        }
        public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder mauiAppBuilder)
        {
            mauiAppBuilder.Services.AddTransient<MainViewModel>();
            //mauiAppBuilder.Services.AddTransient<SectionsViewModel>();
            //mauiAppBuilder.Services.AddTransient<ArticleViewModel>();
            //mauiAppBuilder.Services.AddTransient<BookmarksViewModel>();

            mauiAppBuilder.Services.AddTransient<MainPage>();
            //mauiAppBuilder.Services.AddTransient<SectionsPage>();
            //mauiAppBuilder.Services.AddTransient<ArticlePage>();
            //mauiAppBuilder.Services.AddTransient<BookmarksPage>();

            return mauiAppBuilder;
        }
    }
}
