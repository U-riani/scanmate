using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos
{
    public class ExportLogDto
    {
        public int session_id { get; set; }
        public int product_id { get; set; }
        public string barcode { get; set; }
        public int employee_id { get; set; }
        public double previous_qty { get; set; }
        public double final_qty { get; set; }
        public double scan_qty { get; set; }
        public string timestamp { get; set; }
    }
}
