using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos
{
    public class JsonRpcWrapper
    {
        public string jsonrpc { get; set; }
        public object id { get; set; }
        public JsonElement result { get; set; }
    }


}
