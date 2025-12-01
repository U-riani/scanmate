using CommunityToolkit.Maui;
using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Infrastructure.Repositories;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.UI.ViewModels;
using ZebraSCannerTest1.UI.Views;
using Microsoft.Maui.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1;
//check
public static class MauiProgram
{
    public static IServiceProvider ServiceProvider { get; private set; }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        //var basePath = FileSystem.AppDataDirectory;
        //var std = Path.Combine(basePath, "zebraScanner_standard.db");
        //var loots = Path.Combine(basePath, "zebraScanner_loots.db");

        //try { if (File.Exists(std)) File.Delete(std); } catch { }
        //try { if (File.Exists(loots)) File.Delete(loots); } catch { }

        // // === Database connection ===
        builder.Services.AddSingleton<SqliteConnection>(provider =>
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "zebraScanner_standard.db");
            var conn = new SqliteConnection($"Data Source={path}");
            conn.Open();
            DatabaseInitializer.Initialize(conn, InventoryMode.Standard);
            return conn;
        });


        //builder.Services.AddSingleton<SqliteConnection>(sp =>
        //{
        //    var salesConn = SalesDatabaseInitializer.GetConnection();
        //    SalesDatabaseInitializer.InitializeConnection(salesConn);
        //    return salesConn;
        //});

        // === Core services & repositories ===
        builder.Services.AddSingleton<IProductRepository, ProductRepository>();
        builder.Services.AddSingleton<IScanLogRepository, ScanLogRepository>();
        builder.Services.AddSingleton<ILootsProductRepository, LootsProductRepository>();
        builder.Services.AddSingleton<SalesRepository>();
        builder.Services.AddSingleton<IDataImportService, DataImportService>();
        builder.Services.AddSingleton<IExcelExportService, ExcelExportService>();
        builder.Services.AddSingleton<IExcelExportLogsService, ExcelExportLogsService>(); // ✅ Add this line
        builder.Services.AddSingleton<LogBufferService>();
        builder.Services.AddSingleton<ClipboardService>();
        builder.Services.AddSingleton<IScanningService, ScanningService>();
        builder.Services.AddSingleton<IProductService, ProductService>();
        builder.Services.AddSingleton<IMenuService, MenuService>();
        builder.Services.AddSingleton<IJsonExportService, JsonExportService>();
        builder.Services.AddSingleton<IJsonExportLogsService, JsonExportLogsService>();
        builder.Services.AddSingleton<IApiService, ApiService>();
        builder.Services.AddSingleton<IServerImportService, ServerImportService>();
        builder.Services.AddSingleton<SalesExcelImportService>();
        builder.Services.AddTransient<IScanMateServerService, ScanMateServerService>();


        // === UI helpers ===
        builder.Services.AddSingleton<IDialogService, MauiDialogService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton(typeof(ILoggerService<>), typeof(LoggerService<>));
        builder.Services.AddSingleton<ZebraSCannerTest1.UI.Services.PopupService>(); // ✅ Add this line


        // === ViewModels ===
        builder.Services.AddTransient<InventorizationViewModel>();
        builder.Services.AddTransient<DetailsViewModel>();
        builder.Services.AddTransient<LogsViewModel>();
        builder.Services.AddTransient<ScannedProductsViewModel>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddTransient<InventorizationByLootsMenuViewModel>();
        builder.Services.AddTransient<InventorizationByLootsViewModel>();
        builder.Services.AddTransient<LootsScanningViewModel>();
        builder.Services.AddTransient<InventorizationMenuViewModel>();
        builder.Services.AddTransient<SalesMenuViewModel>();
        builder.Services.AddTransient<SalesViewModel>();


        // === Views ===
        builder.Services.AddSingleton<InventorizationPage>();
        builder.Services.AddTransient<DetailsPage>();
        builder.Services.AddTransient<LogsPage>();
        builder.Services.AddTransient<ScannedProductsPage>();
        builder.Services.AddTransient<InventorizationByLootsMenuPage>();
        builder.Services.AddTransient<InventorizationByLootsPage>();
        builder.Services.AddTransient<LootsScanningPage>();
        builder.Services.AddTransient<InventorizationMenuPage>();
        builder.Services.AddTransient<SalesMenuPage>();
        builder.Services.AddTransient<SalesPage>();

        var app = builder.Build();

        // 🔹 This line exposes the container globally
        ServiceProvider = app.Services;

        return app;
    }
}
