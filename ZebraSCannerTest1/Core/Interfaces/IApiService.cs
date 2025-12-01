using ZebraSCannerTest1.Core.Dtos;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IApiService
    {
        Task<string> DownloadInventoryJsonAsync(string endpoint = "/");
        Task<string> GetEmployeesAsync(string sessionId, string apiKey);
        Task<string> GetDataFromServer(string sessionId, string apiKey, string employeeId);
        Task<UploadResponseDto> UploadInventoryJsonAsync(
        int sessionId,
        string apiKey,
        int employeeId,
        string rawJson);
    }
}
