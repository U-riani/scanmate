using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos
{
    public class ExportProductDto
    {
        public double counted_qty { get; set; }
        public List<ExportLogDto> logs { get; set; } = new();
    }
}
