using System.Buffers.Text;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Dtos;
using ZebraSCannerTest1.Core.Interfaces;

namespace ZebraSCannerTest1.Core.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://test.archevani.com.ge/";
        public ApiService()
        {
            // Use your local test server
            // For Android emulator use: http://10.0.2.2:3000
            // For physical device, use LAN IP like http://192.168.1.101:3000
            _httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        }

        public async Task<string> GetEmployeesAsync(string sessionId, string apiKey)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);


                string endpoint = $"api/scanmate/{sessionId}/employees";

                var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                Console.WriteLine("----- Get Employes RESPONSE: " + json);

                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ---- GetEmployees failed: " + ex.Message);
                throw;
            }
        }
        public async Task<UploadResponseDto> UploadInventoryJsonAsync(  int sessionId,
                                                                        string apiKey,
                                                                        int employeeId,
                                                                        string rawJson
                                                                     )
        {
            string endpoint = $"api/scanmate/{sessionId}/submit/{employeeId}";
            Console.WriteLine("___________________________________________");
            Console.WriteLine(rawJson);
            Console.WriteLine("___________________________________________");

            var gz = CompressGzip(rawJson);


            var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(rawJson, Encoding.UTF8, "application/json")
            };

            req.Headers.Add("X-API-KEY", apiKey);

            var response = await _httpClient.SendAsync(req);
            string resultJson = await response.Content.ReadAsStringAsync();

            Console.WriteLine("=== SERVER RAW RESPONSE ===");
            Console.WriteLine(resultJson);
            Console.WriteLine("=== SERVER RAW RESPONSE ===");


            // Deserialize wrapper
            var wrapper = JsonSerializer.Deserialize<JsonRpcWrapper>(resultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (wrapper == null)
            {
                return new UploadResponseDto
                {
                    success = false,
                    error = "Invalid wrapper"
                };
            }

            UploadResponseDto finalResult = null;

            // CASE 1: result is a JSON object
            if (wrapper.result.ValueKind == JsonValueKind.Object)
            {
                finalResult = wrapper.result.Deserialize<UploadResponseDto>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            // CASE 2: result is a STRING containing JSON
            else if (wrapper.result.ValueKind == JsonValueKind.String)
            {
                string inner = wrapper.result.GetString();
                finalResult = JsonSerializer.Deserialize<UploadResponseDto>(inner,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return finalResult ?? new UploadResponseDto
            {
                success = false,
                error = "Unknown server response"
            };
        }


        public async Task<string> GetDataFromServer(string sessionId, string apiKey, string employeeId)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);


                string endpoint = $"api/scanmate/{sessionId}/data/{employeeId}";

                var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✅ ---- GET JSON Data: {json} chars");
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ---- Download Data failed: {ex.Message}");
                throw;
            }
        }

        public async Task<string> DownloadInventoryJsonAsync(string endpoint = "/")
        {
            try
            {
                Console.WriteLine("++++++:" + _baseUrl + endpoint);
                var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✅ Downloaded inventory JSON: {json.Length} chars");
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Download failed: {ex.Message}");
                throw;
            }
        }

        public static byte[] CompressGzip(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            using var ms = new MemoryStream();
            using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(bytes, 0, bytes.Length);
            }
            return ms.ToArray();
        }
    }
}
