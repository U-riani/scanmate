using ZebraSCannerTest1.Core.Models;

public class ScanProductOddo
{
    public int product_id { get; set; }
    public string barcode { get; set; }
    public string name { get; set; }
    public double qty { get; set; }
    public string uom { get; set; }
    public string category { get; set; }
    public List<VariantModel> variants { get; set; }
    public double compare_price { get; set; }
    public double sale_price { get; set; }
    public string location { get; set; }
    public object employee_ids { get; set; }
}
