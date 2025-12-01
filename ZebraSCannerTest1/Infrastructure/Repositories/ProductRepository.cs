using Microsoft.Data.Sqlite;
using System.Text.Json;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Data;

namespace ZebraSCannerTest1.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private const int BASE_OFFSET = 0;
        private const int LOOTS_OFFSET = 1;

        private string GetTableName(InventoryMode mode) =>
            mode == InventoryMode.Loots ? "LootsProducts" : "Products";

        // ------------------------------------------------------------
        // 1) GET RECENT PRODUCTS (fast)
        // ------------------------------------------------------------
        public async Task<IEnumerable<Product>> GetRecentAsync(int limit = 8, InventoryMode mode = InventoryMode.Standard)
        {
            var list = new List<Product>();
            bool isLoots = mode == InventoryMode.Loots;
            string table = GetTableName(mode);

            using var conn = DatabaseInitializer.GetConnection(mode);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
                SELECT 
                    Barcode,
                    {(isLoots ? "Box_Id," : "")}
                    InitialQuantity,
                    ScannedQuantity,
                    CreatedAt,
                    UpdatedAt
                FROM {table}
                ORDER BY UpdatedAt DESC
                LIMIT $limit";

            cmd.Parameters.AddWithValue("$limit", limit);

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var p = new Product
                {
                    Barcode = r.GetString(0),
                    InitialQuantity = r.GetInt32(isLoots ? 2 : 1),
                    ScannedQuantity = r.GetInt32(isLoots ? 3 : 2),
                    CreatedAt = DateTime.Parse(r.GetString(isLoots ? 4 : 3)),
                    UpdatedAt = DateTime.Parse(r.GetString(isLoots ? 5 : 4))
                };



                list.Add(p);
            }

            return list;
        }


        // ------------------------------------------------------------
        // 2) GET PRODUCTS BY BOX (Loots only)
        // ------------------------------------------------------------
        public async Task<IEnumerable<Product>> GetByBoxAsync(string boxId, InventoryMode mode = InventoryMode.Standard)
        {
            var list = new List<Product>();
            bool isLoots = mode == InventoryMode.Loots;
            int offset = isLoots ? LOOTS_OFFSET : BASE_OFFSET;
            string table = GetTableName(mode);

            using var conn = DatabaseInitializer.GetConnection(mode);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = isLoots
                ? $@"SELECT 
                        Barcode, Box_Id,
                        InitialQuantity, ScannedQuantity,
                        CreatedAt, UpdatedAt,
                        Name, Category, Uom, Location,
                        ComparePrice, SalePrice,
                        VariantsJson, EmployeesJson, Product_id
                    FROM {table}
                    WHERE Box_Id = $box
                    ORDER BY UpdatedAt DESC"
                : $@"SELECT 
                        Barcode,
                        InitialQuantity, ScannedQuantity,
                        CreatedAt, UpdatedAt,
                        Name, Category, Uom, Location,
                        ComparePrice, SalePrice,
                        VariantsJson, EmployeesJson, Product_id
                    FROM {table}
                    ORDER BY UpdatedAt DESC";

            cmd.Parameters.AddWithValue("$box", boxId);

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var p = MapProductFromReader(r, offset);


                list.Add(p);
            }

            return list;
        }


        // ------------------------------------------------------------
        // 3) FIND PRODUCT
        // ------------------------------------------------------------
        public async Task<Product?> FindAsync(string barcode, InventoryMode mode = InventoryMode.Standard, string? boxId = null)
        {
            bool isLoots = mode == InventoryMode.Loots;
            int offset = isLoots ? LOOTS_OFFSET : BASE_OFFSET;
            string table = GetTableName(mode);

            using var conn = DatabaseInitializer.GetConnection(mode);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = isLoots
                ? $@"SELECT 
                        Barcode, Box_Id,
                        InitialQuantity, ScannedQuantity,
                        CreatedAt, UpdatedAt,
                        Name, Category, Uom, Location,
                        ComparePrice, SalePrice,
                        VariantsJson, EmployeesJson, Product_id
                    FROM {table}
                    WHERE Barcode=$b AND (Box_Id=$box OR $box IS NULL)"
                : $@"SELECT 
                        Barcode,
                        InitialQuantity, ScannedQuantity,
                        CreatedAt, UpdatedAt,
                        Name, Category, Uom, Location,
                        ComparePrice, SalePrice,
                        VariantsJson, EmployeesJson, Product_id
                    FROM {table}
                    WHERE Barcode=$b";

            cmd.Parameters.AddWithValue("$b", barcode);
            if (isLoots)
                cmd.Parameters.AddWithValue("$box", boxId ?? (object)DBNull.Value);

            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return null;

            var p = MapProductFromReader(r, offset);



            return p;
        }


        // ------------------------------------------------------------
        // 4) INSERT PRODUCT
        // ------------------------------------------------------------
        public async Task AddAsync(Product product, InventoryMode mode = InventoryMode.Standard)
        {
            string table = GetTableName(mode);
            bool isLoots = mode == InventoryMode.Loots;

            using var conn = DatabaseInitializer.GetConnection(mode);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = isLoots
                ? $@"INSERT INTO {table}
                        (Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
                    VALUES ($b, $box, $i, $s, $c, $u)"
                : $@"INSERT INTO {table}
                        (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
                    VALUES ($b, $i, $s, $c, $u)";

            cmd.Parameters.AddWithValue("$b", product.Barcode);
            

            cmd.Parameters.AddWithValue("$i", product.InitialQuantity);
            cmd.Parameters.AddWithValue("$s", product.ScannedQuantity);
            cmd.Parameters.AddWithValue("$c", product.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));

            await cmd.ExecuteNonQueryAsync();
        }


        // ------------------------------------------------------------
        // 5) UPDATE PRODUCT
        // ------------------------------------------------------------
        public async Task UpdateAsync(Product product, InventoryMode mode = InventoryMode.Standard)
        {
            string table = GetTableName(mode);
            bool isLoots = mode == InventoryMode.Loots;

            using var conn = DatabaseInitializer.GetConnection(mode);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = isLoots
                ? $@"UPDATE {table}
                    SET ScannedQuantity=$s, UpdatedAt=$u
                    WHERE Barcode=$b AND (Box_Id=$box OR $box IS NULL)"
                : $@"UPDATE {table}
                    SET ScannedQuantity=$s, UpdatedAt=$u
                    WHERE Barcode=$b";

            cmd.Parameters.AddWithValue("$s", product.ScannedQuantity);
            cmd.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$b", product.Barcode);

            await cmd.ExecuteNonQueryAsync();
        }


        // ------------------------------------------------------------
        // 6) GET INVENTORY STATS
        // ------------------------------------------------------------
        public (int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes)
            GetInventoryStats(InventoryMode mode = InventoryMode.Standard)
        {
            string table = GetTableName(mode);

            using var conn = DatabaseInitializer.GetConnection(mode);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
                SELECT 
                    SUM(InitialQuantity),
                    SUM(ScannedQuantity),
                    COUNT(*),
                    SUM(CASE WHEN ScannedQuantity > 0 THEN 1 ELSE 0 END)
                FROM {table}";

            using var r = cmd.ExecuteReader();

            if (!r.Read())
                return (0, 0, 0, 0);

            return (
                r.IsDBNull(0) ? 0 : r.GetInt32(0),
                r.IsDBNull(1) ? 0 : r.GetInt32(1),
                r.IsDBNull(2) ? 0 : r.GetInt32(2),
                r.IsDBNull(3) ? 0 : r.GetInt32(3)
            );
        }


        // ------------------------------------------------------------
        // 7) READER → PRODUCT MAPPER
        // ------------------------------------------------------------
        private Product MapProductFromReader(SqliteDataReader r, int offset)
        {
            return new Product
            {
                Barcode = r.GetString(0),

                InitialQuantity = Convert.ToInt32(r.GetValue(1 + offset)),
                ScannedQuantity = Convert.ToInt32(r.GetValue(2 + offset)),
                CreatedAt = DateTime.Parse(r.GetString(3 + offset)),
                UpdatedAt = DateTime.Parse(r.GetString(4 + offset)),

                Name = r.IsDBNull(5 + offset) ? "" : r.GetString(5 + offset),
                Category = r.IsDBNull(6 + offset) ? "" : r.GetString(6 + offset),
                Uom = r.IsDBNull(7 + offset) ? "" : r.GetString(7 + offset),
                Location = r.IsDBNull(8 + offset) ? "" : r.GetString(8 + offset),


                ComparePrice = r.IsDBNull(9 + offset) ? 0 : r.GetDouble(9 + offset),
                SalePrice = r.IsDBNull(10 + offset) ? 0 : r.GetDouble(10 + offset),

                Variants = ParseJson<List<VariantModel>>(r, 11 + offset),
                Employees = ParseJson<List<int>>(r, 12 + offset),
                ProductId = Convert.ToInt32(r.GetValue(13 + offset)),
            };
        }


        private T ParseJson<T>(SqliteDataReader r, int index)
        {
            if (r.IsDBNull(index)) return default!;
            string json = r.GetString(index);
            if (string.IsNullOrWhiteSpace(json)) return default!;
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
