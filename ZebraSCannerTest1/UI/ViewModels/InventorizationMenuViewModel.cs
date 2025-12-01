using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Dtos;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Extensions;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Helpers;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.UI.Views;


namespace ZebraSCannerTest1.UI.ViewModels;

[QueryProperty(nameof(Mode), "Mode")]
public partial class InventorizationMenuViewModel : ObservableObject
{
    [ObservableProperty]
    private InventoryMode mode;

    public IRelayCommand NavigateToContinueCommand { get; }
    public IRelayCommand NavigateToResultCommand { get; }
    public IRelayCommand ExportCommand { get; }
    public IRelayCommand ImportCommand { get; }
    public IRelayCommand ClearCommand { get; }

    public IRelayCommand OpenConnectCommand { get;  }

    private readonly IDataImportService _importer;
    private readonly IExcelExportService _exporter;
    private readonly IExcelExportLogsService _logExporter;
    private readonly IDialogService _dialogs;
    private readonly ILoggerService<InventorizationMenuViewModel> _logger;
    private readonly PopupService _popup;
    private readonly IProductService _productService;

    private readonly IJsonExportService _jsonExporter;
    private readonly IJsonExportLogsService _jsonLogExporter;
    private readonly IApiService _apiService;
    private readonly IServerImportService _serverImporter;
    private IScanLogRepository _scanLogRepository;
    private readonly IScanMateServerService _scanMateService;


    private bool _importLocked = false;

    //int sessionId = 31;
    //string apiKey = "dab7e6f986654dd2c0b4194306b326afb4fd00068d1210fbab590c38620b3a46";
    //string selectedName;

    int sessionId
    {
        get => Preferences.Get("SessionId", 0);
        set => Preferences.Set("SessionId", value);
    }

    string apiKey
    {
        get => Preferences.Get("ApiKey", "");
        set => Preferences.Set("ApiKey", value);
    }

    string selectedName
    {
        get => Preferences.Get("SelectedEmployeeName", "");
        set => Preferences.Set("SelectedEmployeeName", value);
    }

    int selectedEmployeeId
    {
        get => Preferences.Get("SelectedEmployeeId", 0);
        set => Preferences.Set("SelectedEmployeeId", value);
    }


    public InventorizationMenuViewModel(
        IDataImportService importer,
        IExcelExportService exporter,
        IDialogService dialogs,
        ILoggerService<InventorizationMenuViewModel> logger,
        PopupService popup,
        IProductService productService,
        IExcelExportLogsService logExporter,
        IJsonExportService jsonExporter,
        IJsonExportLogsService jsonLogExporter,
        IApiService apiService,
        IServerImportService serverImporter,
        IScanLogRepository scanLogRepository,
        IScanMateServerService scanMateService)
    {
        _importer = importer;
        _exporter = exporter;
        _dialogs = dialogs;
        _logger = logger;
        _popup = popup;
        _productService = productService;
        _logExporter = logExporter;
        _jsonExporter = jsonExporter;
        _jsonLogExporter = jsonLogExporter;

        NavigateToContinueCommand = new AsyncRelayCommand(OnContinueAsync);
        NavigateToResultCommand = new AsyncRelayCommand(OnResultAsync);
        ExportCommand = new AsyncRelayCommand(OnExportAsync);
        ImportCommand = new AsyncRelayCommand(OnImportAsync);
        ClearCommand = new AsyncRelayCommand(OnClearAsync);
        OpenConnectCommand = new AsyncRelayCommand(GetSesionInformation);
        _apiService = apiService;
        _serverImporter = serverImporter;
        _scanLogRepository = scanLogRepository;
        _scanMateService = scanMateService;

    }

    private async Task OnContinueAsync()
    {
        var targetPage = Mode == InventoryMode.Loots
            ? nameof(InventorizationByLootsPage)
            : nameof(InventorizationPage);

        await Shell.Current.GoToAsync(targetPage);
    }

    private async Task GetSesionInformation()
    {
        var sesionIfo = await Shell.Current.DisplayPromptAsync(
            "GET ID/API", "Scan QR:",
            "OK", "Cancel");

        if (string.IsNullOrWhiteSpace(sesionIfo))
            return;

        // Expect: "40:apikey"
        var sesionInfoArr = sesionIfo.Split(':');

        if (sesionInfoArr.Length != 2)
        {
            await Shell.Current.DisplayAlert("Error", "QR format invalid", "OK");
            return;
        }

        if (!int.TryParse(sesionInfoArr[0], out int sessionId))
        {
            await Shell.Current.DisplayAlert("Error", "Session ID invalid", "OK");
            return;
        }

        string apiKey = sesionInfoArr[1];

        // Save
        Preferences.Set("SessionId", sessionId);
        Preferences.Set("ApiKey", apiKey);

        Console.WriteLine($"Session: {sessionId} / API: {apiKey}");

    }

    // === RESULT ===
    private async Task OnResultAsync()
    {
        try
        {
            await _popup.ShowProgressAsync($"Calculating totals for {Mode}...");
            var (totalInitial, totalScanned, totalBarcodes, scannedBarcodes) =
                _productService.GetInventoryStats(Mode);

            await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());

            string modeName = Mode.ToString().ToUpper();
            int quantityDiff = totalScanned - totalInitial;
            int barcodeDiff = scannedBarcodes - totalBarcodes;

            string msg =
                $"📊 {modeName} INVENTORY SUMMARY\n\n" +
                $"🔹 Quantities:\n" +
                $"• Scanned Total Qty: {totalScanned:N0}\n" +
                $"• Expected Total Qty: {totalInitial:N0}\n" +
                $"• Difference: {quantityDiff:N0}\n\n" +
                $"🔹 Barcodes:\n" +
                $"• Scanned Barcodes: {scannedBarcodes:N0}\n" +
                $"• Total Barcodes: {totalBarcodes:N0}\n" +
                $"• Difference: {barcodeDiff:N0}";

            await Shell.Current.DisplayAlert("📦 Inventory Results", msg, "OK");
        }
        catch (Exception ex)
        {
            _popup.Close();
            _logger.Error($"{Mode} result calculation failed", ex);
            await _dialogs.ShowMessageAsync("❌ Error", ex.Message);
        }
    }

    // === EXPORT ===
    private async Task OnExportAsync()
    {
        bool popupOpened = false;
        try
        {
            var choice = await Shell.Current.DisplayActionSheet(
                $"Export {Mode} Data",
                "Cancel", null,
                "Export to Server",
                "Products (Excel)", "Logs (Excel)");

            if (choice == "Cancel" || string.IsNullOrWhiteSpace(choice))
                return;

            await _popup.ShowProgressAsync("Preparing export...");
            popupOpened = true;

#if ANDROID
            var path = Android.OS.Environment.GetExternalStoragePublicDirectory(
                Android.OS.Environment.DirectoryDownloads).AbsolutePath;
#else
            var path = FileSystem.AppDataDirectory;
#endif

            string extension = choice.Contains("JSON") ? "json" : "xlsx";
            var fileName = $"{Mode}_{choice.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{extension}";
            var fullPath = Path.Combine(path, fileName);

            var progress = new Progress<double>(p =>
                _popup.UpdateMessage($"Exporting... {(int)(p * 100)}%"));

            // Decide which export service to use
            await Task.Run(async () =>
            {
                if (choice == "Products (Excel)")
                    await _exporter.ExportProductsAsync(fullPath, progress, Mode);
                else if (choice == "Logs (Excel)")
                    await _logExporter.ExportLogsAsync(fullPath, progress, Mode);

                else if (choice == "Export to Server")
                {
                    await ExportToServerAsync();
                    return;
                }
            });


            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _popup.UpdateMessage("Finalizing...");
                _popup.Close();
            });
            popupOpened = false;

            //await _dialogs.ShowMessageAsync("✅ Export Complete", $"File saved: {fileName}");
        }
        catch (Exception ex)
        {
            if (popupOpened) _popup.Close();
            _logger.Error($"{Mode} export failed", ex);
            await _dialogs.ShowMessageAsync("❌ Export Error", ex.Message);
        }
    }



    private async Task OnImportAsync()
    {
        var choice = await Shell.Current.DisplayActionSheet(
            $"Import {Mode} Data",
            "Cancel", null,
            "From Device", "From Server");

        if (choice == "Cancel" || string.IsNullOrWhiteSpace(choice))
            return;

        bool popupOpened = false;

        try
        {
            popupOpened = true;

            int imported = 0;

            if (choice == "From Server")
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    _popup.ShowProgressAsync("Downloading data..."));

                var employees = await _scanMateService.GetEmployeesAsync(sessionId, apiKey);

                if (employees.Count == 0)
                {
                    await _dialogs.ShowMessageAsync("No Employees", "Cannot proceed.");
                    return;
                }


                selectedName = await Shell.Current.DisplayActionSheet(
                    "Choose Employee", "Cancel", null, employees.Select(e => e.name).ToArray());

                if (selectedName == "Cancel") return;

                var selected = employees.First(e => e.name == selectedName);

                Preferences.Set("SelectedEmployeeName", selectedName);
                Preferences.Set("SelectedEmployeeId", selected.id);


                await Task.Run(async () =>
                {
                    var products = await _scanMateService.DownloadSessionDataAsync(
                        sessionId, apiKey, selected.id);

                    await SaveProductsToLocalDb(products);
                });


                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    _popup.Close();
                    await _dialogs.ShowMessageAsync("Done", $"Imported successfully.");
                });

                return;
            }


            else // From Device
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = $"Select File to Import for {Mode}",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "*/*" } },
                    { DevicePlatform.WinUI, new[] { ".xlsx", ".json", ".db" } }
                })
                });

                if (result == null)
                    return;

                using var stream = await result.OpenReadAsync();
                string ext = Path.GetExtension(result.FileName).ToLowerInvariant();

                switch (ext)
                {
                    case ".xlsx":
                        _popup.UpdateMessage("Importing Excel data...");
                        await _importer.ImportExcelAsync(stream, Mode, result.FileName);
                        break;
                    case ".json":
                        _popup.UpdateMessage("Importing JSON data...");
                        await _importer.ImportJsonAsync(stream, Mode);
                        break;
                    case ".db":
                        _popup.UpdateMessage("Importing database...");
                        await _importer.ImportDbAsync(stream, Mode);
                        break;
                    default:
                        throw new InvalidOperationException("Please select a valid .xlsx, .json, or .db file.");
                }

                _popup.UpdateMessage("✅ Import Complete (Device)");
            }

            await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());
            popupOpened = false;

            await _dialogs.ShowMessageAsync("✅ Import Complete", $"Data imported successfully from {choice}");
        }
        catch (Exception ex)
        {
            _logger.Error($"{Mode} import failed", ex);

            if (popupOpened)
                await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());

            await _dialogs.ShowMessageAsync("❌ Import Error", ex.Message);
        }
        finally
        {
            if (popupOpened)
                await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());
        }
    }

    private async Task SaveProductsToLocalDb(List<ScanProductOddo> products)
    {
        var dtoList = products
       .Select(p => p.ToJsonDto())
       .ToList();

        string json = JsonSerializer.Serialize(dtoList);

        using var jsonStream = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(json));

        int imported = await _importer.ImportJsonAsync(jsonStream, Mode);

        Console.WriteLine($"[SERVER IMPORT] Imported {imported} products from Odoo.");
    }

    public static string DecompressGzipBase64(string base64)
    {
        var gzBytes = Convert.FromBase64String(base64);

        using var ms = new MemoryStream(gzBytes);
        using var gz = new GZipStream(ms, CompressionMode.Decompress);
        using var outMs = new MemoryStream();
        gz.CopyTo(outMs);

        return Encoding.UTF8.GetString(outMs.ToArray());
    }

    private async Task<ExportRootDto> BuildExportDtoAsync(int sessionId, int employeeId)
    {
        var dto = new ExportRootDto();

        // 1) Load scanned products
        var products = _productService.GetAllProducts(Mode);

        // 2) Load logs
        var logs = await _scanLogRepository.GetLogsAsync(Mode);

        foreach (var p in products)
        {
            var logItems = logs
                .Where(l => l.Barcode == p.Barcode)
                .Select(l => new ExportLogDto
                {
                    session_id = sessionId,
                    product_id = l.ProductId,
                    barcode = l.Barcode,
                    employee_id = employeeId,
                    previous_qty = l.Was,
                    final_qty = l.IsValue,
                    scan_qty = l.IncrementBy,
                    timestamp = l.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToList();

            dto.barcode_data[p.Barcode] = new ExportProductDto
            {
                counted_qty = p.ScannedQuantity,
                logs = logItems
            };
        }

        return dto;
    }

    private async Task ExportToServerAsync()
    {
        int employeeId = selectedEmployeeId;
        int sessionId = this.sessionId;

        var dto = await BuildExportDtoAsync(sessionId, employeeId);

        string json = JsonSerializer.Serialize(dto);

        Console.WriteLine("===== OUTGOING JSON =====");
        Console.WriteLine(json);

        var result = await _apiService.UploadInventoryJsonAsync(
            sessionId,
            apiKey,
            employeeId,
            json
        );

        Console.WriteLine("111111111111111111");
        Console.WriteLine(result.error);
        Console.WriteLine("111111111111111111");


        if (result.success)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await _dialogs.ShowMessageAsync(
                    "Success",
                    $"Exported {dto.barcode_data.Count} items.\nUpdated on server: {result.updated}"
                )
            );
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await _dialogs.ShowMessageAsync(
                    "❌ Failed",
                    $"Server error: {result.error}"
                )
            );
        }
    }


    public static byte[] CompressGzip(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }



    private async Task OnClearAsync()
    {
        await _dialogs.ShowMessageAsync("Not Implemented", "Clear data functionality not yet available.");
    }

}
