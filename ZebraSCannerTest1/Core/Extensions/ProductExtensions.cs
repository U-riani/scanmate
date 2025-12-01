using System.Text.Json;
using ZebraSCannerTest1.Core.Dtos;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Core.Extensions
{
    public static class ProductExtensions
    {
        public static JsonDto ToJsonDto(this ScanProductOddo p)
        {
            var now = DateTime.UtcNow.ToString("o");

            return new JsonDto
            {
                ProductId = p.product_id,
                Barcode = p.barcode,
                InitialQuantity = p.qty,
                ScannedQuantity = 0,
                CreatedAt = now,
                UpdatedAt = now,

                Name = p.name,
                Category = p.category,
                Uom = p.uom,
                Location = p.location,

                Variants = p.variants ?? new List<VariantModel>(),
                employee_ids = NormalizeEmployees(p.employee_ids),

                ComparePrice = p.compare_price,
                SalePrice = p.sale_price
            };
        }
        private static List<int> NormalizeEmployees(object raw)
        {
            if (raw == null)
                return new List<int>();

            try
            {
                // Case: already a list of ints
                if (raw is JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<int>();

                        foreach (var x in je.EnumerateArray())
                        {
                            if (x.ValueKind == JsonValueKind.Number)
                                list.Add(x.GetInt32());
                            else if (x.ValueKind == JsonValueKind.String && int.TryParse(x.GetString(), out var num))
                                list.Add(num);
                        }

                        return list;
                    }

                    // Case: single number
                    if (je.ValueKind == JsonValueKind.Number)
                        return new List<int> { je.GetInt32() };

                    // Case: null / false
                    return new List<int>();
                }

                // Case: int
                if (raw is int i)
                    return new List<int> { i };

                // Case: string
                if (raw is string s && int.TryParse(s, out var numFromString))
                    return new List<int> { numFromString };
            }
            catch
            {
                return new List<int>();
            }

            return new List<int>();
        }

    }
}
