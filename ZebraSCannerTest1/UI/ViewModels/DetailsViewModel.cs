using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using System.Text.Json;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Messages;

namespace ZebraSCannerTest1.UI.ViewModels;

[QueryProperty(nameof(BoxId), "BoxId")]
public partial class DetailsViewModel : ObservableObject, IDisposable
{
    private readonly ClipboardService _clipboard;
    private int _originalQuantity;
    private readonly string _currentSection;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool hasUnsavedChanges;
    [ObservableProperty] private bool isReadOnly;

    public ObservableCollection<ScanLog> Logs { get; } = new();

    public DetailsViewModel(ClipboardService clipboard)
    {
        _clipboard = clipboard;
        _currentSection = Preferences.Get("CurrentSection", null);

        SaveCommand = new AsyncRelayCommand(() => SaveUpdatedDetailsAsync(false, null));
        LoadLogsCommand = new AsyncRelayCommand(LoadLogsAsync);
    }

    // === PRODUCT PROPERTIES ===
    public int Difference => ScannedQuantity - InitialQuantity;

    [ObservableProperty] private string productBarcode;
    [ObservableProperty] private int scannedQuantity;
    [ObservableProperty] private int initialQuantity;

    [ObservableProperty] private string productCategory;
    [ObservableProperty] private string productUom;
    [ObservableProperty] private string productLocation;
    [ObservableProperty] private string productName;

    [ObservableProperty] private double comparePrice;
    [ObservableProperty] private double salePrice;

    [ObservableProperty] private InventoryMode currentMode = InventoryMode.Standard;
    [ObservableProperty] private string boxId = string.Empty;

    // Loots summary
    [ObservableProperty] private int totalScannedQuantity;
    [ObservableProperty] private int totalInitialQuantity;
    [ObservableProperty] private string allBoxesInfo = string.Empty;

    [ObservableProperty]
    private ObservableCollection<VariantModel> variants = new();

    [ObservableProperty]
    private ObservableCollection<int> employees = new();


    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand LoadLogsCommand { get; }

    // ==============================
    // QUANTITY ADJUSTMENTS
    // ==============================

    [RelayCommand]
    private void Increment()
    {
        ScannedQuantity++;
        UpdateUnsavedState();
    }

    [RelayCommand]
    private void Decrement()
    {
        if (ScannedQuantity > 0)
        {
            ScannedQuantity--;
            UpdateUnsavedState();
        }
    }

    [RelayCommand]
    private async Task ManualEditAsync()
    {
        var input = await Shell.Current.DisplayPromptAsync(
            "Edit Quantity", "Enter new quantity:", "OK", "Cancel",
            "10", maxLength: 5, keyboard: Keyboard.Numeric,
            initialValue: ScannedQuantity.ToString());

        if (int.TryParse(input, out int newQty) && newQty >= 0)
        {
            ScannedQuantity = newQty;
            UpdateUnsavedState();
        }
    }

    private void UpdateUnsavedState() =>
        HasUnsavedChanges = ScannedQuantity != _originalQuantity;


    // ==============================
    // SAVE
    // ==============================

    public async Task SaveUpdatedDetailsAsync(bool isAutoSave = false, int? previousValue = null)
    {
        if (string.IsNullOrWhiteSpace(ProductBarcode))
            return;

        string now = DateTime.UtcNow.ToString("o");
        string table = CurrentMode == InventoryMode.Loots ? "LootsProducts" : "Products";

        int previousQty = previousValue ?? LoadPreviousQty();

        int incrementBy = ScannedQuantity - previousQty;
        if (incrementBy == 0 && !isAutoSave)
        {
            await Shell.Current.DisplayAlert("No Changes", "Quantity unchanged.", "OK");
            return;
        }

        using var conn = DatabaseInitializer.GetConnection(CurrentMode);
        using var cmd = conn.CreateCommand();

        // STANDARD MODE
        if (CurrentMode == InventoryMode.Standard)
        {
            cmd.CommandText = @"
INSERT INTO Products
(Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
 Name, Category, Uom, Location, ComparePrice, SalePrice,
 VariantsJson, EmployeesJson)
VALUES
($barcode, $initial, $scanned, $created, $updated,
 $name, $category, $uom, $location, $compare, $sale,
 $variants, $employees)
ON CONFLICT(Barcode) DO UPDATE SET
 InitialQuantity=$initial,
 ScannedQuantity=$scanned,
 UpdatedAt=$updated,
 Name=$name,
 Category=$category,
 Uom=$uom,
 Location=$location,
 ComparePrice=$compare,
 SalePrice=$sale,
 VariantsJson=$variants,
 EmployeesJson=$employees;
";

            cmd.Parameters.AddWithValue("$barcode", ProductBarcode);
            cmd.Parameters.AddWithValue("$initial", InitialQuantity);
            cmd.Parameters.AddWithValue("$scanned", ScannedQuantity);
            cmd.Parameters.AddWithValue("$created", now);
            cmd.Parameters.AddWithValue("$updated", now);

            cmd.Parameters.AddWithValue("$name", ProductName ?? "");
            cmd.Parameters.AddWithValue("$category", ProductCategory ?? "");
            cmd.Parameters.AddWithValue("$uom", ProductUom ?? "");
            cmd.Parameters.AddWithValue("$location", ProductLocation ?? "");

            cmd.Parameters.AddWithValue("$compare", ComparePrice);
            cmd.Parameters.AddWithValue("$sale", SalePrice);
            cmd.Parameters.AddWithValue("$variants",
            JsonSerializer.Serialize(Variants ?? new ObservableCollection<VariantModel>()));

            cmd.Parameters.AddWithValue("$employees",
                JsonSerializer.Serialize(Employees ?? new ObservableCollection<int>()));

            cmd.ExecuteNonQuery();
        }

        // LOOTS MODE
        else
        {
            cmd.CommandText = @"
INSERT OR REPLACE INTO LootsProducts
(Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
 Name, Category, Uom, Location, ComparePrice, SalePrice,
 VariantsJson, EmployeesJson)
VALUES
($barcode, $box, $initial, $scanned, $created, $updated,
 $name, $category, $uom, $location, $compare, $sale,
 $variants, $employees);";

            cmd.Parameters.AddWithValue("$barcode", ProductBarcode);
            cmd.Parameters.AddWithValue("$box", BoxId ?? "");
            cmd.Parameters.AddWithValue("$initial", InitialQuantity);
            cmd.Parameters.AddWithValue("$scanned", ScannedQuantity);
            cmd.Parameters.AddWithValue("$created", now);
            cmd.Parameters.AddWithValue("$updated", now);

            cmd.Parameters.AddWithValue("$name", ProductName ?? "");
            cmd.Parameters.AddWithValue("$category", ProductCategory ?? "");
            cmd.Parameters.AddWithValue("$uom", ProductUom ?? "");
            cmd.Parameters.AddWithValue("$location", ProductLocation ?? "");

            cmd.Parameters.AddWithValue("$compare", ComparePrice);
            cmd.Parameters.AddWithValue("$sale", SalePrice);
            cmd.Parameters.AddWithValue("$variants",
            JsonSerializer.Serialize(Variants ?? new ObservableCollection<VariantModel>()));

            cmd.Parameters.AddWithValue("$employees",
                JsonSerializer.Serialize(Employees ?? new ObservableCollection<int>()));


            cmd.ExecuteNonQuery();
        }

        WriteLog(previousQty, incrementBy, now);

        _originalQuantity = ScannedQuantity;
        HasUnsavedChanges = false;

        if (!isAutoSave)
            await Shell.Current.DisplayAlert("Saved", "Product updated.", "OK");
    }

    private int LoadPreviousQty()
    {
        using var conn = DatabaseInitializer.GetConnection(CurrentMode);
        using var cmd = conn.CreateCommand();

        if (CurrentMode == InventoryMode.Loots)
        {
            cmd.CommandText = "SELECT ScannedQuantity FROM LootsProducts WHERE Barcode=$b AND Box_Id=$box";
            cmd.Parameters.AddWithValue("$b", ProductBarcode);
            cmd.Parameters.AddWithValue("$box", BoxId ?? "");
        }
        else
        {
            cmd.CommandText = "SELECT ScannedQuantity FROM Products WHERE Barcode=$b";
            cmd.Parameters.AddWithValue("$b", ProductBarcode);
        }

        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    private void WriteLog(int previousQty, int incrementBy, string now)
    {
        using var conn = DatabaseInitializer.GetConnection(CurrentMode);
        using var log = conn.CreateCommand();

        if (CurrentMode == InventoryMode.Standard)
        {
            log.CommandText = @"
INSERT INTO ScanLogs
(Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section)
VALUES ($b,$was,$inc,$val,$now,1,$sec)";
            log.Parameters.AddWithValue("$b", ProductBarcode);
            log.Parameters.AddWithValue("$was", previousQty);
            log.Parameters.AddWithValue("$inc", incrementBy);
            log.Parameters.AddWithValue("$val", ScannedQuantity);
            log.Parameters.AddWithValue("$now", now);
            log.Parameters.AddWithValue("$sec", _currentSection ?? (object)DBNull.Value);
        }
        else
        {
            log.CommandText = @"
INSERT INTO LootsScanLogs
(Barcode, Box_Id, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section)
VALUES ($b,$box,$was,$inc,$val,$now,1,$sec)";
            log.Parameters.AddWithValue("$b", ProductBarcode);
            log.Parameters.AddWithValue("$box", BoxId ?? "");
            log.Parameters.AddWithValue("$was", previousQty);
            log.Parameters.AddWithValue("$inc", incrementBy);
            log.Parameters.AddWithValue("$val", ScannedQuantity);
            log.Parameters.AddWithValue("$now", now);
            log.Parameters.AddWithValue("$sec", _currentSection ?? (object)DBNull.Value);
        }

        log.ExecuteNonQuery();
    }

    // ==============================
    // LOAD PRODUCT
    // ==============================

    public async Task LoadProductAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductBarcode))
            return;

        using var conn = DatabaseInitializer.GetConnection(CurrentMode);
        using var cmd = conn.CreateCommand();

        if (CurrentMode == InventoryMode.Standard)
        {
            cmd.CommandText = @"
SELECT Name, Category, Uom, Location,
       ComparePrice, SalePrice,
       InitialQuantity, ScannedQuantity, VariantsJson, EmployeesJson
FROM Products
WHERE Barcode=$b";

            cmd.Parameters.AddWithValue("$b", ProductBarcode);
        }
        else
        {
            cmd.CommandText = @"
SELECT Name, Category, Uom, Location,
       ComparePrice, SalePrice,
       InitialQuantity, ScannedQuantity,  VariantsJson, EmployeesJson, Box_Id
FROM LootsProducts
WHERE Barcode=$b AND Box_Id=$box";

            cmd.Parameters.AddWithValue("$b", ProductBarcode);
            cmd.Parameters.AddWithValue("$box", BoxId ?? "");
        }

        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return;

        ProductName = r.IsDBNull(0) ? "" : r.GetString(0);
        ProductCategory = r.IsDBNull(1) ? "" : r.GetString(1);
        ProductUom = r.IsDBNull(2) ? "" : r.GetString(2);
        ProductLocation = r.IsDBNull(3) ? "" : r.GetString(3);
        ComparePrice = r.IsDBNull(4) ? 0 : r.GetDouble(4);
        SalePrice = r.IsDBNull(5) ? 0 : r.GetDouble(5);

        InitialQuantity = r.GetInt32(6);
        ScannedQuantity = r.GetInt32(7);
        // Variants
        string rawVariants = r.IsDBNull(8) ? "[]" : r.GetString(8);
        string rawEmployees = r.IsDBNull(9) ? "[]" : r.GetString(9);
        try
        {
            Console.WriteLine($"--------=== {rawVariants}  {rawEmployees}");
            var vList = JsonSerializer.Deserialize<List<VariantModel>>(rawVariants)
                        ?? new List<VariantModel>();

            Variants = new ObservableCollection<VariantModel>(vList);
        }
        catch
        {
            Variants = new ObservableCollection<VariantModel>();
        }

        // Employees

        try
        {
            var eList = JsonSerializer.Deserialize<List<int>>(rawEmployees)
                        ?? new List<int>();

            Employees = new ObservableCollection<int>(eList);
        }
        catch
        {
            Employees = new ObservableCollection<int>();
        }

        if (CurrentMode == InventoryMode.Loots && !r.IsDBNull(10))
            BoxId = r.GetString(10);


        _originalQuantity = ScannedQuantity;
        HasUnsavedChanges = false;

        if (CurrentMode == InventoryMode.Loots)
            LoadLootTotals(conn);
    }

    private void LoadLootTotals(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT Box_Id, SUM(ScannedQuantity), SUM(InitialQuantity)
FROM LootsProducts
WHERE Barcode=$b
GROUP BY Box_Id";
        cmd.Parameters.AddWithValue("$b", ProductBarcode);

        using var r = cmd.ExecuteReader();

        int totalScanned = 0;
        int totalInitial = 0;
        List<string> list = new();

        while (r.Read())
        {
            string box = r.IsDBNull(0) ? "(none)" : r.GetString(0);
            int scanned = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            int initial = r.IsDBNull(2) ? 0 : r.GetInt32(2);

            totalScanned += scanned;
            totalInitial += initial;

            list.Add($"{box} ({scanned}/{initial})");
        }

        TotalScannedQuantity = totalScanned;
        TotalInitialQuantity = totalInitial;
        AllBoxesInfo = string.Join(" • ", list);
    }

    // ==============================
    // LOAD LOGS
    // ==============================

    private async Task LoadLogsAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductBarcode))
            return;

        Logs.Clear();

        string table = CurrentMode == InventoryMode.Loots ? "LootsScanLogs" : "ScanLogs";

        using var conn = DatabaseInitializer.GetConnection(CurrentMode);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = CurrentMode == InventoryMode.Loots
            ? @"SELECT Barcode, Box_Id, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section
                FROM LootsScanLogs WHERE Barcode=$b ORDER BY UpdatedAt DESC LIMIT 50"
            : @"SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section
                FROM ScanLogs WHERE Barcode=$b ORDER BY UpdatedAt DESC LIMIT 50";

        cmd.Parameters.AddWithValue("$b", ProductBarcode);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var log = new ScanLog
            {
                Barcode = r.GetString(0),
                Was = r.GetInt32(CurrentMode == InventoryMode.Loots ? 2 : 1),
                IncrementBy = r.GetInt32(CurrentMode == InventoryMode.Loots ? 3 : 2),
                IsValue = r.GetInt32(CurrentMode == InventoryMode.Loots ? 4 : 3),
                UpdatedAt = DateTime.Parse(r.GetString(CurrentMode == InventoryMode.Loots ? 5 : 4)),
                IsManual = r.IsDBNull(CurrentMode == InventoryMode.Loots ? 6 : 5) ? null : r.GetInt32(CurrentMode == InventoryMode.Loots ? 6 : 5),
                Section = r.IsDBNull(CurrentMode == InventoryMode.Loots ? 7 : 6) ? null : r.GetString(CurrentMode == InventoryMode.Loots ? 7 : 6),
            };

            if (CurrentMode == InventoryMode.Loots)
                log.Box_Id = r.IsDBNull(1) ? null : r.GetString(1);

            Logs.Add(log);
        }
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }
}
