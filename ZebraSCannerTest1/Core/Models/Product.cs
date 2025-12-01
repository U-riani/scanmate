using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZebraSCannerTest1.Core.Models
{
    public class Product : INotifyPropertyChanged
    {
        private int _initialQuantity;
        private int _scannedQuantity;
        private DateTime _updatedAt;

        private string _name;
        private string _category;
        private string _uom;
        private string _location;

        private double _comparePrice;
        private double _salePrice;

        private List<VariantModel> _variants = new();
        private List<int> _employees = new();
        public int _productId { get; set; }

        public string Barcode { get; set; }
        public DateTime CreatedAt { get; set; }

        public int InitialQuantity
        {
            get => _initialQuantity;
            set { _initialQuantity = value; OnPropertyChanged(); }
        }

        public int ScannedQuantity
        {
            get => _scannedQuantity;
            set { _scannedQuantity = value; OnPropertyChanged(); }
        }

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set { _updatedAt = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public string Uom
        {
            get => _uom;
            set { _uom = value; OnPropertyChanged(); }
        }

        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged(); }
        }

        public double ComparePrice
        {
            get => _comparePrice;
            set { _comparePrice = value; OnPropertyChanged(); }
        }

        public double SalePrice
        {
            get => _salePrice;
            set { _salePrice = value; OnPropertyChanged(); }
        }

        public int ProductId
        {
            get => _productId;
            set { _productId = value; OnPropertyChanged(); }
        }

        public List<VariantModel> Variants
        {
            get => _variants;
            set { _variants = value; OnPropertyChanged(); }
        }

        public List<int> Employees
        {
            get => _employees;
            set { _employees = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string field = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(field));
    }
}
