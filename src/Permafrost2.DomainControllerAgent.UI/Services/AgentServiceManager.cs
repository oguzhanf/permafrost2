using System.ServiceProcess;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Permafrost2.DomainControllerAgent.UI.Services;

public class AgentServiceManager : IAgentServiceManager
{
    private readonly ILogger<AgentServiceManager> _logger;
    private readonly string _serviceName = "Permafrost2 Domain Controller Agent";

    public AgentServiceManager(ILogger<AgentServiceManager> logger)
    {
        _logger = logger;
    }

    public async Task<ServiceControllerStatus> GetServiceStatusAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var service = new ServiceController(_serviceName);
                return service.Status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get service status");
                return ServiceControllerStatus.Stopped;
            }
        });
    }

    public async Task StartServiceAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using var service = new ServiceController(_serviceName);
                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    _logger.LogInformation("Service started successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start service");
                throw;
            }
        });
    }

    public async Task StopServiceAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using var service = new ServiceController(_serviceName);
                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    _logger.LogInformation("Service stopped successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop service");
                throw;
            }
        });
    }

    public async Task RestartServiceAsync()
    {
        await StopServiceAsync();
        await Task.Delay(2000); // Wait 2 seconds
        await StartServiceAsync();
    }

    public async Task<string> GetServiceLogsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                    "Permafrost2", "DomainControllerAgent", "logs");
                
                if (!Directory.Exists(logPath))
                    return "No logs found.";

                var logFiles = Directory.GetFiles(logPath, "*.log")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Take(1);

                if (!logFiles.Any())
                    return "No log files found.";

                var latestLog = logFiles.First();
                var lines = File.ReadAllLines(latestLog);
                
                // Return last 100 lines
                return string.Join(Environment.NewLine, lines.TakeLast(100));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read service logs");
                return $"Error reading logs: {ex.Message}";
            }
        });
    }

    public async Task<Dictionary<string, object>> GetServiceConfigurationAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Permafrost2", "DomainControllerAgent", "appsettings.json");

                if (!File.Exists(configPath))
                    return new Dictionary<string, object>();

                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                return config ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read service configuration");
                return new Dictionary<string, object>();
            }
        });
    }

    public async Task UpdateServiceConfigurationAsync(Dictionary<string, object> configuration)
    {
        await Task.Run(() =>
        {
            try
            {
                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Permafrost2", "DomainControllerAgent", "appsettings.json");

                var directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory!);

                var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
                
                _logger.LogInformation("Service configuration updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update service configuration");
                throw;
            }
        });
    }
}
