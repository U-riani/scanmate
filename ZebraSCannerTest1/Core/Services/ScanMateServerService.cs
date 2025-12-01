using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.UI.ViewModels;

public class ScanMateServerService : IScanMateServerService
{
    private readonly IApiService _api;

    public ScanMateServerService(IApiService api)
    {
        _api = api;
    }

    public async Task<List<Employee>> GetEmployeesAsync(int sessionId, string apiKey)
    {
        string response = await _api.GetEmployeesAsync(sessionId.ToString(), apiKey);
        var result = JsonSerializer.Deserialize<EmployeeResponse>(response);

        return result?.success == true ? result.employees : new List<Employee>();
    }

    public async Task<List<ScanProductOddo>> DownloadSessionDataAsync(int sessionId, string apiKey, int employeeId)
    {
        // 1) Call API
        string response = await _api.GetDataFromServer(
            sessionId.ToString(),
            apiKey,
            employeeId.ToString());

        // 2) Deserialize gzip wrapper
        var result = JsonSerializer.Deserialize<ScanMateGzResponse>(
            response,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result == null || !result.success || string.IsNullOrWhiteSpace(result.gz_data))
            return new List<ScanProductOddo>();

        // 3) Decompress
        string json = DecompressBase64Gzip(result.gz_data);

        // 4) Deserialize REAL product list
        return JsonSerializer.Deserialize<List<ScanProductOddo>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<ScanProductOddo>();
    }


    private string DecompressBase64Gzip(string base64Gz)
    {
        var gz = Convert.FromBase64String(base64Gz);
        using var inStream = new MemoryStream(gz);
        using var gzStream = new GZipStream(inStream, CompressionMode.Decompress);
        using var outStream = new MemoryStream();
        gzStream.CopyTo(outStream);
        return Encoding.UTF8.GetString(outStream.ToArray());
    }

    public static string CompressToBase64Gzip(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);

        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            gz.Write(bytes, 0, bytes.Length);
        }

        return Convert.ToBase64String(ms.ToArray());
    }


}
