//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using ZebraSCannerTest1.Core.Dtos;
//using ZebraSCannerTest1.Core.Models;

//namespace ZebraSCannerTest1.Helpers
//{
//    public static JsonDto ToJsonDto(this ScanProductOddo p)
//    {
//        var now = DateTime.UtcNow.ToString("o");

//        return new JsonDto
//        {
//            Barcode = p.barcode,
//            InitialQuantity = p.qty,
//            ScannedQuantity = 0,
//            CreatedAt = now,
//            UpdatedAt = now,
//            Name = p.name,
//            Category = p.category,
//            Uom = p.uom,
//            Location = p.location,

//            Color = "",
//            Size = "",
//            Price = "",
//            ArticCode = ""
//        };
//    }

//}
