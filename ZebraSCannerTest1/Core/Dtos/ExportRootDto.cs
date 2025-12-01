using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos
{
    public class ExportRootDto
    {
        public Dictionary<string, ExportProductDto> barcode_data { get; set; }
            = new();
    }
}
