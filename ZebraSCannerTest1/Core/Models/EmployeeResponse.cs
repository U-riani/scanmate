using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Models
{
    public class EmployeeResponse
    {
        public bool success { get; set; }
        public List<Employee> employees { get; set; }
    }
}
