using Permafrost2.Shared.DTOs;
using System.Text;
using System.Text.Json;

namespace Permafrost2.DomainControllerAgent.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<AgentRegistrationResponse> RegisterAsync(AgentRegistrationRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/agents/register", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<AgentRegistrationResponse>(responseContent, _jsonOptions);
                return result ?? new AgentRegistrationResponse { Success = false, Message = "Invalid response format" };
            }
            else
            {
                _logger.LogError("Registration failed with status {StatusCode}: {Content}", response.StatusCode, responseContent);
                return new AgentRegistrationResponse { Success = false, Message = $"HTTP {response.StatusCode}: {responseContent}" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent");
            return new AgentRegistrationResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<AgentHeartbeatResponse> SendHeartbeatAsync(AgentHeartbeatRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/agents/heartbeat", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<AgentHeartbeatResponse>(responseContent, _jsonOptions);
                return result ?? new AgentHeartbeatResponse { Success = false, Message = "Invalid response format" };
            }
            else
            {
                _logger.LogError("Heartbeat failed with status {StatusCode}: {Content}", response.StatusCode, responseContent);
                return new AgentHeartbeatResponse { Success = false, Message = $"HTTP {response.StatusCode}: {responseContent}" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send heartbeat");
            return new AgentHeartbeatResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<DataSubmissionResponse> SubmitDataAsync(DataSubmissionRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/agents/submit-data", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<DataSubmissionResponse>(responseContent, _jsonOptions);
                return result ?? new DataSubmissionResponse { Success = false, Message = "Invalid response format" };
            }
            else
            {
                _logger.LogError("Data submission failed with status {StatusCode}: {Content}", response.StatusCode, responseContent);
                return new DataSubmissionResponse { Success = false, Message = $"HTTP {response.StatusCode}: {responseContent}" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit data");
            return new DataSubmissionResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/agents/version");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check API connection");
            return false;
        }
    }
}
