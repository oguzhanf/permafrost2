using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Permafrost.EdgeService.Models;
using System.Diagnostics;

namespace Permafrost.EdgeService.Services;

public class DataCollectionWorker : BackgroundService
{
    private readonly ILogger<DataCollectionWorker> _logger;
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly IEventHubService _eventHubService;
    private readonly IConfigurationService _configurationService;
    private readonly ActivitySource _activitySource = ActivitySourceProvider.ActivitySource;

    public DataCollectionWorker(
        ILogger<DataCollectionWorker> logger,
        IActiveDirectoryService activeDirectoryService,
        IEventHubService eventHubService,
        IConfigurationService configurationService)
    {
        _logger = logger;
        _activeDirectoryService = activeDirectoryService;
        _eventHubService = eventHubService;
        _configurationService = configurationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data Collection Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var config = _configurationService.GetServiceConfiguration();
                
                using var activity = _activitySource.StartActivity("DataCollectionCycle");
                activity?.SetTag("instanceId", config.InstanceId);
                activity?.SetTag("domainController", config.DomainControllerName);

                _logger.LogDebug("Starting data collection cycle");

                // Collect and send user data
                if (config.EnableUserCollection)
                {
                    await CollectAndSendUsersAsync(stoppingToken);
                }

                // Collect and send group data
                if (config.EnableGroupCollection)
                {
                    await CollectAndSendGroupsAsync(stoppingToken);
                }

                // Collect and send event data
                if (config.EnableEventCollection)
                {
                    await CollectAndSendEventsAsync(stoppingToken);
                }

                _logger.LogDebug("Data collection cycle completed");

                // Wait for the configured interval
                var delay = TimeSpan.FromMinutes(config.CollectionIntervalMinutes);
                _logger.LogDebug("Waiting {Delay} before next collection cycle", delay);
                
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Data collection worker cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in data collection cycle");
                
                // Wait a shorter time before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Data Collection Worker stopped");
    }

    private async Task CollectAndSendUsersAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("CollectAndSendUsers");
        
        try
        {
            _logger.LogDebug("Collecting user data from Active Directory");
            
            var parameters = new UserQueryParameters
            {
                Page = 1,
                PageSize = 1000 // Collect all users in batches
            };

            var allUsers = new List<DomainUser>();
            int currentPage = 1;
            
            while (true)
            {
                parameters.Page = currentPage;
                var response = await _activeDirectoryService.GetUsersAsync(parameters, cancellationToken);
                
                if (!response.Success || !response.Data?.Any() == true)
                {
                    break;
                }

                allUsers.AddRange(response.Data);
                
                if (currentPage >= response.TotalPages)
                {
                    break;
                }
                
                currentPage++;
            }

            if (allUsers.Any())
            {
                _logger.LogInformation("Collected {Count} users, sending to Event Hub", allUsers.Count);
                await _eventHubService.SendUserDataAsync(allUsers, cancellationToken);
                activity?.SetTag("userCount", allUsers.Count);
            }
            else
            {
                _logger.LogDebug("No users collected");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect and send user data");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task CollectAndSendGroupsAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("CollectAndSendGroups");
        
        try
        {
            _logger.LogDebug("Collecting group data from Active Directory");
            
            var parameters = new GroupQueryParameters
            {
                Page = 1,
                PageSize = 1000 // Collect all groups in batches
            };

            var allGroups = new List<DomainGroup>();
            int currentPage = 1;
            
            while (true)
            {
                parameters.Page = currentPage;
                var response = await _activeDirectoryService.GetGroupsAsync(parameters, cancellationToken);
                
                if (!response.Success || !response.Data?.Any() == true)
                {
                    break;
                }

                allGroups.AddRange(response.Data);
                
                if (currentPage >= response.TotalPages)
                {
                    break;
                }
                
                currentPage++;
            }

            if (allGroups.Any())
            {
                _logger.LogInformation("Collected {Count} groups, sending to Event Hub", allGroups.Count);
                await _eventHubService.SendGroupDataAsync(allGroups, cancellationToken);
                activity?.SetTag("groupCount", allGroups.Count);
            }
            else
            {
                _logger.LogDebug("No groups collected");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect and send group data");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task CollectAndSendEventsAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("CollectAndSendEvents");
        
        try
        {
            _logger.LogDebug("Collecting event data from Active Directory");
            
            var parameters = new EventQueryParameters
            {
                Page = 1,
                PageSize = 1000,
                FromDate = DateTime.UtcNow.AddHours(-1) // Collect events from last hour
            };

            var response = await _activeDirectoryService.GetEventsAsync(parameters, cancellationToken);
            
            if (response.Success && response.Data?.Any() == true)
            {
                var events = response.Data.ToList();
                _logger.LogInformation("Collected {Count} events, sending to Event Hub", events.Count);
                await _eventHubService.SendEventDataAsync(events, cancellationToken);
                activity?.SetTag("eventCount", events.Count);
            }
            else
            {
                _logger.LogDebug("No events collected");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect and send event data");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Don't rethrow for events as they're not critical
        }
    }

    public override void Dispose()
    {
        _activitySource?.Dispose();
        base.Dispose();
    }
}
