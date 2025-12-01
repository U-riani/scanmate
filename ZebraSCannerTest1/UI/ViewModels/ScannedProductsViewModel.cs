using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels;

[QueryProperty(nameof(CurrentBoxId), "BoxId")]
[QueryProperty(nameof(CurrentMode), "Mode")]
public partial class ScannedProductsViewModel : ObservableObject
{
    private readonly SqliteConnection _conn;
    private readonly ClipboardService _clipboard;
    private CancellationTokenSource? _loadCts;

    private string _currentSortField = "UpdatedAt";
    private bool _currentSortDescending = true;
    private string _currentFilter = "ScannedQuantity > 0";

    private int _offset = 0;
    private const int PageSize = 50;

    [ObservableProperty] private string currentSortDescription = "Sort: Updated ↓";
    [ObservableProperty] private string currentFilterDescription = "Filter: Scanned";
    [ObservableProperty] private int rowCount;
    [ObservableProperty] private bool isInitialLoading;
    [ObservableProperty] private bool isLoadingMore;
    [ObservableProperty] private bool hasMoreRows;
    [ObservableProperty] private int totalRowCount;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool needsReload = true;
    [ObservableProperty] private string currentBoxId = string.Empty;
    [ObservableProperty] private InventoryMode currentMode = InventoryMode.Standard;

    public ObservableCollection<StatsProduct> ScannedProductsStats { get; private set; } = new();

    public ScannedProductsViewModel(SqliteConnection conn, ClipboardService clipboard)
    {
        _conn = conn;
        _clipboard = clipboard;
    }

    // =======================================
    // LOAD PRODUCTS
    // =======================================
    private async Task LoadProductsAsync(bool reset)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        if (reset)
        {
            _offset = 0;
            ScannedProductsStats.Clear();
            TotalRowCount = 0;
            IsInitialLoading = true;
            IsLoadingMore = false;
        }
        else
        {
            IsLoadingMore = true;
        }

        try
        {
            var where = string.IsNullOrWhiteSpace(_currentFilter) ? "" : $"WHERE {_currentFilter}";
            var dir = _currentSortDescending ? "DESC" : "ASC";

            var table = CurrentMode == InventoryMode.Loots ? "LootsProducts" : "Products";

            if (CurrentMode == InventoryMode.Loots && !string.IsNullOrWhiteSpace(CurrentBoxId))
            {
                var box = CurrentBoxId.Replace("'", "''");
                var extra = $"Box_Id = '{box}'";

                where = string.IsNullOrWhiteSpace(where)
                    ? $"WHERE {extra}"
                    : $"{where} AND {extra}";
            }

            (List<StatsProduct> temp, int total) = await Task.Run(() =>
            {
                var list = new List<StatsProduct>();

                using var conn = DatabaseInitializer.GetConnection(CurrentMode);
                using var cmd = conn.CreateCommand();

                cmd.CommandText = CurrentMode == InventoryMode.Loots
                    ? $@"
                        SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity,
                               CreatedAt, UpdatedAt,
                               Name, Category, Uom, Location,
                               ComparePrice, SalePrice,
                               VariantsJson, EmployeesJson
                        FROM {table}
                        {where}
                        ORDER BY {_currentSortField} {dir}
                        LIMIT {PageSize} OFFSET {_offset};"
                    : $@"
                        SELECT Barcode, NULL as Box_Id, InitialQuantity, ScannedQuantity,
                               CreatedAt, UpdatedAt,
                               Name, Category, Uom, Location,
                               ComparePrice, SalePrice,
                               VariantsJson, EmployeesJson
                        FROM {table}
                        {where}
                        ORDER BY {_currentSortField} {dir}
                        LIMIT {PageSize} OFFSET {_offset};";

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    if (token.IsCancellationRequested)
                        throw new OperationCanceledException();

                    list.Add(new StatsProduct
                    {
                        Barcode = r.GetString(0),
                        BoxId = r.IsDBNull(1) ? "" : r.GetString(1),
                        InitialQuantity = r.GetDouble(2),
                        ScannedQuantity = r.GetDouble(3),
                        CreatedAt = DateTime.Parse(r.GetString(4)),
                        UpdatedAt = DateTime.Parse(r.GetString(5)),
                        Name = r.IsDBNull(6) ? "" : r.GetString(6),
                        Category = r.IsDBNull(7) ? "" : r.GetString(7),
                        Uom = r.IsDBNull(8) ? "" : r.GetString(8),
                        Location = r.IsDBNull(9) ? "" : r.GetString(9),
                        ComparePrice = r.IsDBNull(10) ? 0 : r.GetDouble(10),
                        SalePrice = r.IsDBNull(11) ? 0 : r.GetDouble(11),
                        VariantsJson = r.IsDBNull(12) ? "" : r.GetString(12),
                        EmployeesJson = r.IsDBNull(13) ? "" : r.GetString(13)
                    });
                }

                int totalCount;
                using var countCmd = conn.CreateCommand();
                countCmd.CommandText = $"SELECT COUNT(*) FROM {table} {where}";
                totalCount = Convert.ToInt32(countCmd.ExecuteScalar());

                return (list, totalCount);
            }, token);

            if (token.IsCancellationRequested)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var p in temp)
                    ScannedProductsStats.Add(p);

                _offset += temp.Count;
                RowCount = ScannedProductsStats.Count;
                TotalRowCount = total;
                HasMoreRows = _offset < total;
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.DisplayAlert("Error", ex.Message, "OK"));
        }
        finally
        {
            IsInitialLoading = false;
            IsLoadingMore = false;
        }
    }

    // =======================================
    [RelayCommand]
    private async Task LoadMoreAsync() => await LoadProductsAsync(false);

    public async Task LoadAsync(bool reset = true, CancellationToken token = default)
    {
        if (IsLoading) return;

        try
        {
            await Task.Yield();
            await LoadProductsAsync(reset);
        }
        catch { }
        finally
        {
            IsInitialLoading = false;
            IsLoadingMore = false;
        }
    }

    // =======================================
    // SORT
    // =======================================
    [RelayCommand]
    private async Task Sort()
    {
        var fields = CurrentMode == InventoryMode.Loots
            ? new[] { "Box_Id", "Barcode", "ScannedQuantity", "InitialQuantity", "ComparePrice", "SalePrice", "UpdatedAt", "CreatedAt" }
            : new[] { "Barcode", "ScannedQuantity", "InitialQuantity", "ComparePrice", "SalePrice", "UpdatedAt", "CreatedAt" };

        string fieldChoice = await Shell.Current.DisplayActionSheet("Sort by:", "Cancel", null, fields);
        if (string.IsNullOrEmpty(fieldChoice) || fieldChoice == "Cancel") return;

        string orderChoice = await Shell.Current.DisplayActionSheet("Order:", "Cancel", null, "Ascending", "Descending");
        if (string.IsNullOrEmpty(orderChoice) || orderChoice == "Cancel") return;

        _currentSortField = fieldChoice;
        _currentSortDescending = orderChoice == "Descending";

        CurrentSortDescription = $"Sort: {fieldChoice} {(_currentSortDescending ? "↓" : "↑")}";
        await LoadProductsAsync(reset: true);
    }

    // =======================================
    // FILTER
    // =======================================
    [RelayCommand]
    private async Task Filter()
    {
        var choice = await Shell.Current.DisplayActionSheet(
            "Choose filter",
            "Cancel",
            null,
            "All Products",
            "Scanned > 0",
            "Unscanned (Scanned = 0)",
            "Shortage (Scanned < Initial)",
            "Overstock (Scanned > Initial)",
            "Equal (Scanned = Initial)",
            "",
            "Search by Barcode",
            "Search by Name",
            CurrentMode == InventoryMode.Loots ? "Filter by Box" : null
        );

        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        switch (choice)
        {
            case "All Products": _currentFilter = ""; CurrentFilterDescription = "Filter: All"; break;
            case "Scanned > 0": _currentFilter = "ScannedQuantity > 0"; CurrentFilterDescription = "Filter: Scanned"; break;
            case "Unscanned (Scanned = 0)": _currentFilter = "ScannedQuantity = 0"; break;
            case "Shortage (Scanned < Initial)": _currentFilter = "ScannedQuantity < InitialQuantity"; break;
            case "Overstock (Scanned > Initial)": _currentFilter = "ScannedQuantity > InitialQuantity"; break;
            case "Equal (Scanned = Initial)": _currentFilter = "ScannedQuantity = InitialQuantity"; break;

            case "Search by Barcode":
                var code = await Shell.Current.DisplayPromptAsync("Search", "Enter barcode:", "OK", "Cancel");
                if (!string.IsNullOrWhiteSpace(code))
                {
                    _currentFilter = $"Barcode LIKE '%{code}%'";
                    CurrentFilterDescription = $"Filter: Code~{code}";
                }
                break;

            case "Search by Name":
                var name = await Shell.Current.DisplayPromptAsync("Search", "Enter product name:", "OK", "Cancel");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _currentFilter = $"Name LIKE '%{name}%'";
                    CurrentFilterDescription = $"Filter: Name~{name}";
                }
                break;

            case "Filter by Box":
                if (CurrentMode == InventoryMode.Loots)
                {
                    var box = await Shell.Current.DisplayPromptAsync("Filter Box", "Enter Box ID:", "OK", "Cancel");
                    if (!string.IsNullOrWhiteSpace(box))
                    {
                        box = box.Replace("'", "''");
                        _currentFilter = $"Box_Id LIKE '%{box}%'";
                        CurrentFilterDescription = $"Filter: Box~{box}";
                    }
                }
                break;
        }

        await LoadProductsAsync(reset: true);
    }

    // =======================================
    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        _currentSortField = "UpdatedAt";
        _currentSortDescending = true;

        if (CurrentMode == InventoryMode.Loots)
        {
            CurrentBoxId = string.Empty;
            _currentFilter = "";
            CurrentFilterDescription = "Filter: All Loots Boxes";
        }
        else
        {
            _currentFilter = "ScannedQuantity > 0";
            CurrentFilterDescription = "Filter: Scanned";
        }

        await LoadProductsAsync(reset: true);
    }

    // =======================================
    [RelayCommand]
    private async Task CopyBarcode(string barcode) =>
        await _clipboard.CopyAsync(barcode);

    public async void ApplyManualFilter(string filter)
    {
        _currentFilter = filter;
        CurrentFilterDescription = $"Filter: Manual ({filter})";
        await LoadProductsAsync(reset: true);
    }

    // =======================================
    [RelayCommand]
    public async Task OpenDetailsAsync(StatsProduct product)
    {
        if (product == null) return;

        var query = new Dictionary<string, object>
        {
            ["Barcode"] = product.Barcode,
            ["Quantity"] = product.ScannedQuantity,
            ["InitialQuantity"] = product.InitialQuantity,
            ["Name"] = product.Name ?? "",
            ["Location"] = product.Location ?? "",
            ["Category"] = product.Category ?? "",
            ["ComparePrice"] = product.ComparePrice,
            ["SalePrice"] = product.SalePrice,
            ["IsReadOnly"] = true
        };

        await Shell.Current.GoToAsync(nameof(DetailsPage), query);
    }
}
