using Permafrost2.Shared.DTOs;
using System.Text.Json;

namespace Permafrost2.DomainControllerAgent.Services;

public class ConfigurationManager : IConfigurationManager
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly string _configFilePath;
    private AgentConfigurationDto? _cachedConfiguration;

    public ConfigurationManager(IConfiguration configuration, ILogger<ConfigurationManager> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent-config.json");
    }

    public async Task<Guid?> GetAgentIdAsync()
    {
        var config = await LoadAgentConfigAsync();
        return config.AgentId;
    }

    public async Task SetAgentIdAsync(Guid agentId)
    {
        var config = await LoadAgentConfigAsync();
        config.AgentId = agentId;
        await SaveAgentConfigAsync(config);
    }

    public async Task<string?> GetApiKeyAsync()
    {
        var config = await LoadAgentConfigAsync();
        return config.ApiKey;
    }

    public async Task SetApiKeyAsync(string apiKey)
    {
        var config = await LoadAgentConfigAsync();
        config.ApiKey = apiKey;
        await SaveAgentConfigAsync(config);
    }

    public async Task<AgentConfigurationDto> GetConfigurationAsync()
    {
        if (_cachedConfiguration != null)
            return _cachedConfiguration;

        var config = await LoadAgentConfigAsync();
        _cachedConfiguration = config.Configuration ?? GetDefaultConfiguration();
        return _cachedConfiguration;
    }

    public async Task UpdateConfigurationAsync(AgentConfigurationDto configuration)
    {
        var config = await LoadAgentConfigAsync();
        config.Configuration = configuration;
        _cachedConfiguration = configuration;
        await SaveAgentConfigAsync(config);
    }

    public async Task<DateTime?> GetLastHeartbeatAsync()
    {
        var config = await LoadAgentConfigAsync();
        return config.LastHeartbeat;
    }

    public async Task SetLastHeartbeatAsync(DateTime timestamp)
    {
        var config = await LoadAgentConfigAsync();
        config.LastHeartbeat = timestamp;
        await SaveAgentConfigAsync(config);
    }

    public async Task<DateTime?> GetLastDataCollectionAsync()
    {
        var config = await LoadAgentConfigAsync();
        return config.LastDataCollection;
    }

    public async Task SetLastDataCollectionAsync(DateTime timestamp)
    {
        var config = await LoadAgentConfigAsync();
        config.LastDataCollection = timestamp;
        await SaveAgentConfigAsync(config);
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        var config = await LoadAgentConfigAsync();
        return config.Settings?.GetValueOrDefault(key);
    }

    public async Task SetSettingAsync(string key, string value)
    {
        var config = await LoadAgentConfigAsync();
        config.Settings ??= new Dictionary<string, string>();
        config.Settings[key] = value;
        await SaveAgentConfigAsync(config);
    }

    private async Task<AgentConfig> LoadAgentConfigAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                var config = JsonSerializer.Deserialize<AgentConfig>(json);
                return config ?? new AgentConfig();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load agent configuration from {ConfigPath}", _configFilePath);
        }

        return new AgentConfig();
    }

    private async Task SaveAgentConfigAsync(AgentConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save agent configuration to {ConfigPath}", _configFilePath);
        }
    }

    private AgentConfigurationDto GetDefaultConfiguration()
    {
        return new AgentConfigurationDto
        {
            HeartbeatIntervalSeconds = _configuration.GetValue<int>("Permafrost2:HeartbeatIntervalSeconds", 300),
            DataCollectionIntervalMinutes = _configuration.GetValue<int>("Permafrost2:DataCollectionIntervalMinutes", 60),
            EnableDetailedLogging = _configuration.GetValue<bool>("Permafrost2:EnableDetailedLogging", false),
            EnabledDataTypes = _configuration.GetSection("Permafrost2:EnabledDataTypes").Get<List<string>>() ?? new List<string> { "Users" }
        };
    }

    private class AgentConfig
    {
        public Guid? AgentId { get; set; }
        public string? ApiKey { get; set; }
        public AgentConfigurationDto? Configuration { get; set; }
        public DateTime? LastHeartbeat { get; set; }
        public DateTime? LastDataCollection { get; set; }
        public Dictionary<string, string>? Settings { get; set; }
    }
}
