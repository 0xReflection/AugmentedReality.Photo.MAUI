using Microsoft.Extensions.Logging;
using Presentation.ViewModel;

namespace Presentation
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                
                
                .UseMauiApp<App>()
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

#if DEBUG
    		builder.Logging.AddDebug();
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
