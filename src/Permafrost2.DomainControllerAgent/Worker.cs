using Permafrost2.DomainControllerAgent.Services;
using Permafrost2.Shared.DTOs;
using System.Text.Json;

namespace Permafrost2.DomainControllerAgent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IApiClient _apiClient;
    private readonly IDomainDataCollector _domainDataCollector;
    private readonly Services.IConfigurationManager _configurationManager;
    private readonly IServiceProvider _serviceProvider;

    private Guid? _agentId;
    private DateTime _lastHeartbeat = DateTime.MinValue;
    private DateTime _lastDataCollection = DateTime.MinValue;
    private AgentConfigurationDto _agentConfiguration = new();

    public Worker(
        ILogger<Worker> logger,
        IConfiguration configuration,
        IApiClient apiClient,
        IDomainDataCollector domainDataCollector,
        Services.IConfigurationManager configurationManager,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _apiClient = apiClient;
        _domainDataCollector = domainDataCollector;
        _configurationManager = configurationManager;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Permafrost2 Domain Controller Agent starting...");

        // Initialize agent
        await InitializeAgentAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Send heartbeat if needed
                await SendHeartbeatIfNeededAsync();

                // Collect and submit data if needed
                await CollectDataIfNeededAsync();

                // Wait for next cycle
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in main worker loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Permafrost2 Domain Controller Agent stopping...");
    }

    private async Task InitializeAgentAsync()
    {
        try
        {
            // Load existing agent ID if available
            _agentId = await _configurationManager.GetAgentIdAsync();
            _agentConfiguration = await _configurationManager.GetConfigurationAsync();

            if (_agentId == null)
            {
                // Register new agent
                await RegisterAgentAsync();
            }
            else
            {
                _logger.LogInformation("Agent already registered with ID: {AgentId}", _agentId);
            }

            // Test domain connection
            var domainConnected = await _domainDataCollector.TestDomainConnectionAsync();
            if (!domainConnected)
            {
                _logger.LogWarning("Domain connection test failed - some functionality may be limited");
            }

            // Test API connection
            var apiConnected = await _apiClient.CheckConnectionAsync();
            if (!apiConnected)
            {
                _logger.LogWarning("API connection test failed - will retry during operation");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize agent");
        }
    }

    private async Task RegisterAgentAsync()
    {
        try
        {
            var request = new AgentRegistrationRequest
            {
                Name = _configuration["Permafrost2:AgentName"] ?? "Domain Controller Agent",
                Type = _configuration["Permafrost2:AgentType"] ?? "DomainController",
                Version = _configuration["Permafrost2:Version"] ?? "1.0.0",
                MachineName = Environment.MachineName,
                IpAddress = GetLocalIpAddress(),
                Domain = Environment.UserDomainName,
                OperatingSystem = Environment.OSVersion.ToString()
            };

            var response = await _apiClient.RegisterAsync(request);
            if (response.Success && response.AgentId != Guid.Empty)
            {
                _agentId = response.AgentId;
                await _configurationManager.SetAgentIdAsync(_agentId.Value);

                if (!string.IsNullOrEmpty(response.ApiKey))
                {
                    await _configurationManager.SetApiKeyAsync(response.ApiKey);
                }

                if (response.Configuration != null)
                {
                    _agentConfiguration = response.Configuration;
                    await _configurationManager.UpdateConfigurationAsync(_agentConfiguration);
                }

                _logger.LogInformation("Agent registered successfully with ID: {AgentId}", _agentId);
            }
            else
            {
                _logger.LogError("Agent registration failed: {Message}", response.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent");
        }
    }

    private async Task SendHeartbeatIfNeededAsync()
    {
        if (_agentId == null) return;

        var now = DateTime.UtcNow;
        var heartbeatInterval = TimeSpan.FromSeconds(_agentConfiguration.HeartbeatIntervalSeconds);

        if (now - _lastHeartbeat >= heartbeatInterval)
        {
            try
            {
                var request = new AgentHeartbeatRequest
                {
                    AgentId = _agentId.Value,
                    Status = "Online",
                    StatusMessage = "Agent running normally",
                    Timestamp = now
                };

                var response = await _apiClient.SendHeartbeatAsync(request);
                if (response.Success)
                {
                    _lastHeartbeat = now;
                    await _configurationManager.SetLastHeartbeatAsync(now);

                    if (response.UpdatedConfiguration != null)
                    {
                        _agentConfiguration = response.UpdatedConfiguration;
                        await _configurationManager.UpdateConfigurationAsync(_agentConfiguration);
                        _logger.LogInformation("Agent configuration updated from server");
                    }

                    if (response.UpdateAvailable)
                    {
                        _logger.LogInformation("Agent update available: {Version} at {Url}",
                            response.UpdateVersion, response.UpdateUrl);
                    }
                }
                else
                {
                    _logger.LogWarning("Heartbeat failed: {Message}", response.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send heartbeat");
            }
        }
    }

    private async Task CollectDataIfNeededAsync()
    {
        if (_agentId == null) return;

        var now = DateTime.UtcNow;
        var collectionInterval = TimeSpan.FromMinutes(_agentConfiguration.DataCollectionIntervalMinutes);

        if (now - _lastDataCollection >= collectionInterval)
        {
            try
            {
                foreach (var dataType in _agentConfiguration.EnabledDataTypes)
                {
                    await CollectAndSubmitDataAsync(dataType);
                }

                _lastDataCollection = now;
                await _configurationManager.SetLastDataCollectionAsync(now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect data");
            }
        }
    }

    private async Task CollectAndSubmitDataAsync(string dataType)
    {
        try
        {
            _logger.LogInformation("Collecting {DataType} data...", dataType);

            byte[] data;
            int recordCount;

            switch (dataType.ToLower())
            {
                case "users":
                    var users = await _domainDataCollector.CollectUsersAsync();
                    data = JsonSerializer.SerializeToUtf8Bytes(users);
                    recordCount = users.Count;
                    break;

                case "groups":
                    var groups = await _domainDataCollector.CollectGroupsAsync();
                    data = JsonSerializer.SerializeToUtf8Bytes(groups);
                    recordCount = groups.Count;
                    break;

                case "policies":
                    var policies = await _domainDataCollector.CollectPoliciesAsync();
                    data = JsonSerializer.SerializeToUtf8Bytes(policies);
                    recordCount = policies.Count;
                    break;

                default:
                    _logger.LogWarning("Unknown data type: {DataType}", dataType);
                    return;
            }

            if (recordCount > 0)
            {
                var request = new DataSubmissionRequest
                {
                    AgentId = _agentId!.Value,
                    DataType = dataType,
                    CollectedAt = DateTime.UtcNow,
                    RecordCount = recordCount,
                    Data = data,
                    DataHash = ComputeHash(data)
                };

                var response = await _apiClient.SubmitDataAsync(request);
                if (response.Success)
                {
                    _logger.LogInformation("Successfully submitted {RecordCount} {DataType} records",
                        recordCount, dataType);
                }
                else
                {
                    _logger.LogError("Failed to submit {DataType} data: {Message}",
                        dataType, response.Message);
                }
            }
            else
            {
                _logger.LogInformation("No {DataType} data to submit", dataType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect and submit {DataType} data", dataType);
        }
    }

    private string GetLocalIpAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return ip?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string ComputeHash(byte[] data)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }
}
