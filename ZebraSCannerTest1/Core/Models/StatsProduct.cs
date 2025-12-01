namespace ZebraSCannerTest1.Core.Models
{
    public class StatsProduct
    {
        public string Barcode { get; set; }
        public int ProductId { get; set; }
        public string BoxId { get; set; } = "";

        public double InitialQuantity { get; set; }
        public double ScannedQuantity { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Uom { get; set; } = "";
        public string Location { get; set; } = "";

        public double ComparePrice { get; set; }
        public double SalePrice { get; set; }

        public string VariantsJson { get; set; } = "";
        public string EmployeesJson { get; set; } = "";

        // Convenience
        public int Difference => (int)ScannedQuantity - (int)InitialQuantity;
    }
}
