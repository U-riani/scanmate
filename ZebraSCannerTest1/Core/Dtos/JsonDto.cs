using ZebraSCannerTest1.Core.Models;

public class JsonDto
{
    public int ProductId { get; set; }

    public string Barcode { get; set; }
    public double InitialQuantity { get; set; }
    public double ScannedQuantity { get; set; }
    public string CreatedAt { get; set; }
    public string UpdatedAt { get; set; }

    public string Name { get; set; }
    public string Category { get; set; }
    public string Uom { get; set; }
    public string Location { get; set; }

    public List<VariantModel> Variants { get; set; }
    public List<int> employee_ids { get; set; }

    public double ComparePrice { get; set; }
    public double SalePrice { get; set; }
}
