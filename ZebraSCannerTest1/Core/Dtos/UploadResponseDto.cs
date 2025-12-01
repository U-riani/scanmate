using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos
{
    public class UploadResponseDto
    {
        public bool success { get; set; }
        public int updated { get; set; }
        public string error { get; set; }
    }

}
