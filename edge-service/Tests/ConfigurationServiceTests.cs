using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Permafrost.EdgeService.Models;
using Permafrost.EdgeService.Services;
using Xunit;

namespace Permafrost.EdgeService.Tests;

public class ConfigurationServiceTests
{
    private readonly Mock<ILogger<ConfigurationService>> _mockLogger;
    private readonly IConfiguration _configuration;

    public ConfigurationServiceTests()
    {
        _mockLogger = new Mock<ILogger<ConfigurationService>>();
        
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Service:InstanceId"] = "test-instance",
            ["Service:DomainControllerName"] = "TEST-DC01",
            ["Service:CollectionIntervalMinutes"] = "10",
            ["Service:EnableEventCollection"] = "true",
            ["Service:EnableUserCollection"] = "true",
            ["Service:EnableGroupCollection"] = "false",
            
            ["ActiveDirectory:DomainController"] = "test-dc.example.com",
            ["ActiveDirectory:Port"] = "636",
            ["ActiveDirectory:UseSsl"] = "true",
            ["ActiveDirectory:Username"] = "testuser",
            ["ActiveDirectory:Password"] = "testpass",
            ["ActiveDirectory:SearchBase"] = "DC=test,DC=example,DC=com",
            ["ActiveDirectory:QueryIntervalMinutes"] = "15",
            ["ActiveDirectory:MaxResults"] = "500",
            
            ["EventHub:ConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            ["EventHub:HubName"] = "test-hub",
            ["EventHub:BatchSize"] = "50",
            ["EventHub:MaxWaitTimeSeconds"] = "60",
            
            ["Api:MaxPageSize"] = "500",
            ["Api:DefaultPageSize"] = "50",
            ["Api:EnableSwagger"] = "false"
        });
        
        _configuration = configurationBuilder.Build();
    }

    [Fact]
    public void GetServiceConfiguration_ReturnsCorrectConfiguration()
    {
        // Arrange
        var service = new ConfigurationService(_configuration, _mockLogger.Object);

        // Act
        var config = service.GetServiceConfiguration();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("test-instance", config.InstanceId);
        Assert.Equal("TEST-DC01", config.DomainControllerName);
        Assert.Equal(10, config.CollectionIntervalMinutes);
        Assert.True(config.EnableEventCollection);
        Assert.True(config.EnableUserCollection);
        Assert.False(config.EnableGroupCollection);
    }

    [Fact]
    public void GetActiveDirectoryConfiguration_ReturnsCorrectConfiguration()
    {
        // Arrange
        var service = new ConfigurationService(_configuration, _mockLogger.Object);

        // Act
        var config = service.GetActiveDirectoryConfiguration();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("test-dc.example.com", config.DomainController);
        Assert.Equal(636, config.Port);
        Assert.True(config.UseSsl);
        Assert.Equal("testuser", config.Username);
        Assert.Equal("testpass", config.Password);
        Assert.Equal("DC=test,DC=example,DC=com", config.SearchBase);
        Assert.Equal(15, config.QueryIntervalMinutes);
        Assert.Equal(500, config.MaxResults);
    }

    [Fact]
    public void GetEventHubConfiguration_ReturnsCorrectConfiguration()
    {
        // Arrange
        var service = new ConfigurationService(_configuration, _mockLogger.Object);

        // Act
        var config = service.GetEventHubConfiguration();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test", config.ConnectionString);
        Assert.Equal("test-hub", config.HubName);
        Assert.Equal(50, config.BatchSize);
        Assert.Equal(60, config.MaxWaitTimeSeconds);
    }

    [Fact]
    public void GetApiConfiguration_ReturnsCorrectConfiguration()
    {
        // Arrange
        var service = new ConfigurationService(_configuration, _mockLogger.Object);

        // Act
        var config = service.GetApiConfiguration();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(500, config.MaxPageSize);
        Assert.Equal(50, config.DefaultPageSize);
        Assert.False(config.EnableSwagger);
    }

    [Fact]
    public void GetServiceConfiguration_WithDefaults_ReturnsDefaultValues()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder().Build();
        var service = new ConfigurationService(emptyConfig, _mockLogger.Object);

        // Act
        var config = service.GetServiceConfiguration();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(Environment.MachineName, config.InstanceId);
        Assert.Equal(Environment.MachineName, config.DomainControllerName);
        Assert.Equal(5, config.CollectionIntervalMinutes);
        Assert.True(config.EnableEventCollection);
        Assert.True(config.EnableUserCollection);
        Assert.True(config.EnableGroupCollection);
    }

    [Fact]
    public void GetActiveDirectoryConfiguration_MissingRequired_ThrowsException()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder().Build();
        var service = new ConfigurationService(emptyConfig, _mockLogger.Object);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => service.GetActiveDirectoryConfiguration());
        Assert.Contains("DomainController", exception.Message);
    }

    [Fact]
    public void GetEventHubConfiguration_MissingRequired_ThrowsException()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder().Build();
        var service = new ConfigurationService(emptyConfig, _mockLogger.Object);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => service.GetEventHubConfiguration());
        Assert.Contains("ConnectionString", exception.Message);
    }

    [Fact]
    public void UpdateServiceConfiguration_UpdatesConfiguration()
    {
        // Arrange
        var service = new ConfigurationService(_configuration, _mockLogger.Object);
        var newConfig = new ServiceConfiguration
        {
            InstanceId = "updated-instance",
            DomainControllerName = "UPDATED-DC01",
            CollectionIntervalMinutes = 20,
            EnableEventCollection = false,
            EnableUserCollection = false,
            EnableGroupCollection = true
        };

        // Act
        service.UpdateServiceConfiguration(newConfig);
        var updatedConfig = service.GetServiceConfiguration();

        // Assert
        Assert.Equal("updated-instance", updatedConfig.InstanceId);
        Assert.Equal("UPDATED-DC01", updatedConfig.DomainControllerName);
        Assert.Equal(20, updatedConfig.CollectionIntervalMinutes);
        Assert.False(updatedConfig.EnableEventCollection);
        Assert.False(updatedConfig.EnableUserCollection);
        Assert.True(updatedConfig.EnableGroupCollection);
    }

    [Fact]
    public void UpdateServiceConfiguration_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new ConfigurationService(_configuration, _mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.UpdateServiceConfiguration(null!));
    }

    [Fact]
    public async Task ReloadConfigurationAsync_ClearsCache()
    {
        // Arrange
        var service = new ConfigurationService(_configuration, _mockLogger.Object);
        
        // Get initial configuration to cache it
        var initialConfig = service.GetServiceConfiguration();
        Assert.Equal("test-instance", initialConfig.InstanceId);

        // Act
        await service.ReloadConfigurationAsync();

        // Assert - Configuration should be reloaded (in this test, it will be the same since we're using the same IConfiguration)
        var reloadedConfig = service.GetServiceConfiguration();
        Assert.Equal("test-instance", reloadedConfig.InstanceId);
    }

    [Fact]
    public async Task SaveConfigurationAsync_CompletesSuccessfully()
    {
        // Arrange
        var service = new ConfigurationService(_configuration, _mockLogger.Object);

        // Act & Assert - Should not throw
        await service.SaveConfigurationAsync();
    }
}
