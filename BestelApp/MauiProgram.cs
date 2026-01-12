using Microsoft.Extensions.Logging;
using BestelApp.Services;
using BestelApp.ViewModels;
using BestelApp.Views;

namespace BestelApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // Register configuration service
            builder.Services.AddSingleton<ConfigurationService>();

            // Register services
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<IOrderService, OrderService>();

            // Register ViewModel
            builder.Services.AddSingleton<BestellingViewModel>();

            // Register Views
            builder.Services.AddSingleton<BestellingPage>();
            builder.Services.AddSingleton<AppShell>();
            builder.Services.AddSingleton<App>();


            var app = builder.Build();

            // Initialize database
            var dbService = app.Services.GetRequiredService<DatabaseService>();
            dbService.Init().Wait(); // Using .Wait() for simplicity in startup, consider a better async pattern for production

            return app;
        }
    }
}
