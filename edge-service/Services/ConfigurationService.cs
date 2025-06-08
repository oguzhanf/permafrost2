using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Permafrost.EdgeService.Models;
using System.Text.Json;

namespace Permafrost.EdgeService.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationService> _logger;
    private ServiceConfiguration? _serviceConfig;
    private ActiveDirectoryConfiguration? _adConfig;
    private EventHubConfiguration? _eventHubConfig;
    private ApiConfiguration? _apiConfig;

    public ConfigurationService(IConfiguration configuration, ILogger<ConfigurationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        LoadConfigurations();
    }

    public ServiceConfiguration GetServiceConfiguration()
    {
        return _serviceConfig ??= LoadServiceConfiguration();
    }

    public ActiveDirectoryConfiguration GetActiveDirectoryConfiguration()
    {
        return _adConfig ??= LoadActiveDirectoryConfiguration();
    }

    public EventHubConfiguration GetEventHubConfiguration()
    {
        return _eventHubConfig ??= LoadEventHubConfiguration();
    }

    public ApiConfiguration GetApiConfiguration()
    {
        return _apiConfig ??= LoadApiConfiguration();
    }

    public void UpdateServiceConfiguration(ServiceConfiguration configuration)
    {
        _serviceConfig = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger.LogInformation("Service configuration updated");
    }

    public async Task SaveConfigurationAsync()
    {
        try
        {
            // In a real implementation, this would save to a persistent store
            // For now, we'll just log the action
            _logger.LogInformation("Configuration save requested - not implemented for file-based configuration");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
            throw;
        }
    }

    public async Task ReloadConfigurationAsync()
    {
        try
        {
            // Clear cached configurations
            _serviceConfig = null;
            _adConfig = null;
            _eventHubConfig = null;
            _apiConfig = null;

            // Reload from configuration
            LoadConfigurations();
            
            _logger.LogInformation("Configuration reloaded successfully");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload configuration");
            throw;
        }
    }

    private void LoadConfigurations()
    {
        try
        {
            _serviceConfig = LoadServiceConfiguration();
            _adConfig = LoadActiveDirectoryConfiguration();
            _eventHubConfig = LoadEventHubConfiguration();
            _apiConfig = LoadApiConfiguration();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configurations");
            throw;
        }
    }

    private ServiceConfiguration LoadServiceConfiguration()
    {
        var section = _configuration.GetSection("Service");
        
        return new ServiceConfiguration
        {
            InstanceId = section.GetValue<string>("InstanceId") ?? Environment.MachineName,
            DomainControllerName = section.GetValue<string>("DomainControllerName") ?? Environment.MachineName,
            CollectionIntervalMinutes = section.GetValue<int>("CollectionIntervalMinutes", 5),
            EnableEventCollection = section.GetValue<bool>("EnableEventCollection", true),
            EnableUserCollection = section.GetValue<bool>("EnableUserCollection", true),
            EnableGroupCollection = section.GetValue<bool>("EnableGroupCollection", true)
        };
    }

    private ActiveDirectoryConfiguration LoadActiveDirectoryConfiguration()
    {
        var section = _configuration.GetSection("ActiveDirectory");
        
        var domainController = section.GetValue<string>("DomainController");
        var searchBase = section.GetValue<string>("SearchBase");

        if (string.IsNullOrEmpty(domainController))
        {
            throw new InvalidOperationException("ActiveDirectory:DomainController configuration is required");
        }

        if (string.IsNullOrEmpty(searchBase))
        {
            throw new InvalidOperationException("ActiveDirectory:SearchBase configuration is required");
        }

        return new ActiveDirectoryConfiguration
        {
            DomainController = domainController,
            Port = section.GetValue<int>("Port", 389),
            UseSsl = section.GetValue<bool>("UseSsl", false),
            Username = section.GetValue<string>("Username"),
            Password = section.GetValue<string>("Password"),
            SearchBase = searchBase,
            QueryIntervalMinutes = section.GetValue<int>("QueryIntervalMinutes", 5),
            MaxResults = section.GetValue<int>("MaxResults", 1000)
        };
    }

    private EventHubConfiguration LoadEventHubConfiguration()
    {
        var section = _configuration.GetSection("EventHub");
        
        var connectionString = section.GetValue<string>("ConnectionString");
        var hubName = section.GetValue<string>("HubName");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("EventHub:ConnectionString configuration is required");
        }

        if (string.IsNullOrEmpty(hubName))
        {
            throw new InvalidOperationException("EventHub:HubName configuration is required");
        }

        return new EventHubConfiguration
        {
            ConnectionString = connectionString,
            HubName = hubName,
            BatchSize = section.GetValue<int>("BatchSize", 100),
            MaxWaitTimeSeconds = section.GetValue<int>("MaxWaitTimeSeconds", 30)
        };
    }

    private ApiConfiguration LoadApiConfiguration()
    {
        var section = _configuration.GetSection("Api");
        
        return new ApiConfiguration
        {
            MaxPageSize = section.GetValue<int>("MaxPageSize", 1000),
            DefaultPageSize = section.GetValue<int>("DefaultPageSize", 100),
            EnableSwagger = section.GetValue<bool>("EnableSwagger", true)
        };
    }
}
