using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Models
{
    public class ScanMateGzResponse
    {
        public bool success { get; set; }
        public int session_id { get; set; }
        public int employee_id { get; set; }
        public string gz_data { get; set; }
    }
}
