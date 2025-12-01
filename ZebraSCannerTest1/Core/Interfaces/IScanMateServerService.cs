using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IScanMateServerService
    {
        Task<List<Employee>> GetEmployeesAsync(int sessionId, string apiKey);
        Task<List<ScanProductOddo>> DownloadSessionDataAsync(int sessionId, string apiKey, int employeeId);

    }

}
