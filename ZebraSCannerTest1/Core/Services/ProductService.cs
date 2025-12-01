using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Data;


namespace ZebraSCannerTest1.Core.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _repository;
        private readonly ILoggerService<ProductService> _logger;
        private string GetTableName(bool isLoots) => isLoots ? "LootsProducts" : "Products";
        private readonly ILootsProductRepository _lootsRepo;


        public ProductService(IProductRepository repository, ILoggerService<ProductService> logger, ILootsProductRepository lootsRepo)
        {
            _repository = repository;
            _logger = logger;
            _lootsRepo = lootsRepo;
        }

        public async Task<IEnumerable<Product>> GetRecentAsync(int limit, InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                return await _repository.GetRecentAsync(limit, mode);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load recent products", ex);
                return Array.Empty<Product>();
            }
        }

        public (int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes) GetInventoryStats(InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                if (mode == InventoryMode.Loots)
                    return _lootsRepo.GetInventoryStats(mode);

                return _repository.GetInventoryStats(mode);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to get inventory stats", ex);
                return (0, 0, 0, 0);
            }
        }


        public async Task<Product?> GetByBarcodeAsync(string barcode, InventoryMode mode = InventoryMode.Standard, string box_id = null)
        {
            try
            {
                if (mode == InventoryMode.Loots && box_id is not null)
                    return await _lootsRepo.FindAsync(barcode, box_id);

                return await _repository.FindAsync(barcode, mode);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to get product by barcode", ex);
                return null;
            }
        }

        public async Task AddOrUpdateAsync(Product p, InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                var existing = await _repository.FindAsync(p.Barcode, mode);

                if (existing == null)
                {
                    await _repository.AddAsync(p, mode);
                }
                else
                {
                    await _repository.UpdateAsync(p, mode);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to add or update product", ex);
            }
        }
        public List<StatsProduct> GetAllProducts(InventoryMode mode)
        {
            var list = new List<StatsProduct>();
            string table = mode == InventoryMode.Loots ? "LootsProducts" : "Products";

            using var conn = DatabaseInitializer.GetConnection(mode);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = mode == InventoryMode.Loots
                ? @"SELECT 
            Barcode, Box_Id,
            InitialQuantity, ScannedQuantity,
            CreatedAt, UpdatedAt,
            Name, Category, Uom, Location,
            ComparePrice, SalePrice,
            VariantsJson, EmployeesJson, Product_id
        FROM LootsProducts"
                : @"SELECT 
            Barcode,
            InitialQuantity, ScannedQuantity,
            CreatedAt, UpdatedAt,
            Name, Category, Uom, Location,
            ComparePrice, SalePrice,
            VariantsJson, EmployeesJson, Product_id
        FROM Products";


            using var r = cmd.ExecuteReader();

            while (r.Read())
            {
                if (mode == InventoryMode.Loots)
                {
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
                        EmployeesJson = r.IsDBNull(13) ? "" : r.GetString(13),
                        ProductId = r.IsDBNull(14) ? 0 : r.GetInt32(14)
                    });
                }
                else // Standard mode (NO Box_Id)
                {
                    list.Add(new StatsProduct
                    {
                        Barcode = r.GetString(0),
                        BoxId = "", // always empty in Standard
                        InitialQuantity = r.GetDouble(1),
                        ScannedQuantity = r.GetDouble(2),
                        CreatedAt = DateTime.Parse(r.GetString(3)),
                        UpdatedAt = DateTime.Parse(r.GetString(4)),
                        Name = r.IsDBNull(5) ? "" : r.GetString(5),
                        Category = r.IsDBNull(6) ? "" : r.GetString(6),
                        Uom = r.IsDBNull(7) ? "" : r.GetString(7),
                        Location = r.IsDBNull(8) ? "" : r.GetString(8),
                        ComparePrice = r.IsDBNull(9) ? 0 : r.GetDouble(9),
                        SalePrice = r.IsDBNull(10) ? 0 : r.GetDouble(10),
                        VariantsJson = r.IsDBNull(11) ? "" : r.GetString(11),
                        EmployeesJson = r.IsDBNull(12) ? "" : r.GetString(12),
                        ProductId = r.IsDBNull(13) ? 0 : r.GetInt32(13)
                    });
                }
            }

            return list;

        }



        public async Task<IEnumerable<Product>> GetProductsByBoxAsync(string boxId, InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                return await _lootsRepo.GetByBoxAsync(boxId);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load products by box", ex);
                return Array.Empty<Product>();
            }
        }

    }
}
