using Microsoft.Data.Sqlite;
using System.Text.Json;
using ZebraSCannerTest1.Core.Dtos;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Data;

namespace ZebraSCannerTest1.Core.Services
{
    public class DataImportService : IDataImportService
    {
        private readonly ExcelImportService _excelImport;

        public DataImportService(SqliteConnection conn)
        {
            _excelImport = new ExcelImportService(conn);
        }

        private SqliteConnection GetConn(InventoryMode mode)
        {
            var dbName = mode == InventoryMode.Loots
                ? "zebraScanner_loots.db"
                : "zebraScanner_standard.db";

            var path = Path.Combine(FileSystem.AppDataDirectory, dbName);
            var conn = new SqliteConnection($"Data Source={path}");
            conn.Open();
            return conn;
        }

        public async Task ImportExcelAsync(Stream stream, InventoryMode mode = InventoryMode.Standard, string? fileName = null)
        {
            await _excelImport.ImportExcelAsync(stream, mode, fileName);
        }

        public async Task ImportDbAsync(Stream dbStream, InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                var fileName = mode == InventoryMode.Loots
                    ? "zebraScanner_loots.db"
                    : "zebraScanner_standard.db";

                var targetPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

                if (File.Exists(targetPath))
                {
                    var backup = targetPath + ".bak";
                    File.Copy(targetPath, backup, true);
                }

                using (var dst = File.Create(targetPath))
                    await dbStream.CopyToAsync(dst);

                using var conn = GetConn(mode);
                DatabaseInitializer.Initialize(conn, mode);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to import DB ({mode}): {ex.Message}", ex);
            }
        }

        public async Task<int> ImportJsonAsync(Stream jsonStream, InventoryMode mode = InventoryMode.Standard)
        {
            string table = mode == InventoryMode.Loots ? "LootsProducts" : "Products";
            bool isLoots = mode == InventoryMode.Loots;

            using var conn = GetConn(mode);
            using var reader = new StreamReader(jsonStream);

            var json = await reader.ReadToEndAsync();
            //Console.WriteLine("-+++++++++++----respone" + json);
            var items = JsonSerializer.Deserialize<List<JsonDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });


            if (items == null || items.Count == 0)
                throw new Exception("No valid items found in JSON file.");

            using (var clear = conn.CreateCommand())
            {
                clear.CommandText = $"DELETE FROM {table};";
                clear.ExecuteNonQuery();
            }
            // Clear logs as well
            using (var clearLogs = conn.CreateCommand())
            {
                clearLogs.CommandText = mode == InventoryMode.Loots
                    ? "DELETE FROM LootsScanLogs;"
                    : "DELETE FROM ScanLogs;";
                clearLogs.ExecuteNonQuery();
            }


            using var tx = conn.BeginTransaction();
            using var insert = conn.CreateCommand();
            insert.Transaction = tx;

            insert.CommandText = isLoots
                ? $@"
INSERT OR REPLACE INTO {table}
(Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
 Name, Category, Uom, Location,
 ComparePrice, SalePrice, VariantsJson, EmployeesJson, Product_id)
VALUES
($barcode, $box, $initial, $scanned, $created, $updated,
 $name, $category, $uom, $location,
 $compare, $sale, $variants, $employees, $product_id);"
                : $@"
INSERT OR REPLACE INTO {table}
(Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
 Name, Category, Uom, Location,
 ComparePrice, SalePrice, VariantsJson, EmployeesJson, Product_id)
VALUES
($barcode, $initial, $scanned, $created, $updated,
 $name, $category, $uom, $location,
 $compare, $sale, $variants, $employees, $product_id);";

            insert.Parameters.Add("$barcode", SqliteType.Text);
            insert.Parameters.Add("$initial", SqliteType.Real);
            insert.Parameters.Add("$scanned", SqliteType.Real);
            insert.Parameters.Add("$created", SqliteType.Text);
            insert.Parameters.Add("$updated", SqliteType.Text);
            insert.Parameters.Add("$name", SqliteType.Text);
            insert.Parameters.Add("$category", SqliteType.Text);
            insert.Parameters.Add("$uom", SqliteType.Text);
            insert.Parameters.Add("$location", SqliteType.Text);
            insert.Parameters.Add("$compare", SqliteType.Real);
            insert.Parameters.Add("$sale", SqliteType.Real);
            insert.Parameters.Add("$variants", SqliteType.Text);
            insert.Parameters.Add("$employees", SqliteType.Text);
            insert.Parameters.Add("$product_id", SqliteType.Text);

            int processed = 0;
            string now = DateTime.UtcNow.ToString("o");

            foreach (var p in items)
            {
                //foreach(var i in p.Variants)
                //{
                //    Console.WriteLine("+++++++++" + i.Name);
                //}
                //Console.WriteLine(JsonSerializer.Serialize(p));

                insert.Parameters["$barcode"].Value = p.Barcode;



                insert.Parameters["$initial"].Value = p.InitialQuantity;
                insert.Parameters["$scanned"].Value = p.ScannedQuantity;
                insert.Parameters["$created"].Value = p.CreatedAt ?? now;
                insert.Parameters["$updated"].Value = p.UpdatedAt ?? now;
                insert.Parameters["$name"].Value = p.Name ?? "";
                insert.Parameters["$category"].Value = p.Category ?? "";
                insert.Parameters["$uom"].Value = p.Uom ?? "";
                insert.Parameters["$location"].Value = p.Location ?? "";
                insert.Parameters["$compare"].Value = p.ComparePrice;
                insert.Parameters["$sale"].Value = p.SalePrice;

                insert.Parameters["$variants"].Value =
                    JsonSerializer.Serialize(p.Variants ?? new List<VariantModel>());

                insert.Parameters["$employees"].Value =
                    JsonSerializer.Serialize(p.employee_ids ?? []);
                insert.Parameters["$product_id"].Value = p.ProductId;

                insert.ExecuteNonQuery();
                processed++;
            }

            tx.Commit();
            return processed;
        }
    }
}
