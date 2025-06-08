using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Permafrost.EdgeService.Models;
using System.Diagnostics;
using System.Text.Json;

namespace Permafrost.EdgeService.Services;

public class EventHubService : IEventHubService, IDisposable
{
    private readonly ILogger<EventHubService> _logger;
    private readonly EventHubConfiguration _config;
    private readonly ActivitySource _activitySource = ActivitySourceProvider.ActivitySource;
    private EventHubProducerClient? _producerClient;
    private readonly object _clientLock = new();

    public EventHubService(ILogger<EventHubService> logger, IOptions<EventHubConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public async Task SendUserDataAsync(IEnumerable<DomainUser> users, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("SendUserDataAsync");
        var userList = users.ToList();
        activity?.SetTag("userCount", userList.Count);

        try
        {
            if (!userList.Any())
            {
                _logger.LogDebug("No users to send to Event Hub");
                return;
            }

            var client = await GetProducerClientAsync(cancellationToken);
            var batches = CreateBatches(userList, "user_data");

            foreach (var batch in batches)
            {
                await client.SendAsync(batch, cancellationToken);
                _logger.LogDebug("Sent batch of {Count} user events to Event Hub", batch.Count);
            }

            _logger.LogInformation("Successfully sent {Count} users to Event Hub", userList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send user data to Event Hub");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task SendGroupDataAsync(IEnumerable<DomainGroup> groups, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("SendGroupDataAsync");
        var groupList = groups.ToList();
        activity?.SetTag("groupCount", groupList.Count);

        try
        {
            if (!groupList.Any())
            {
                _logger.LogDebug("No groups to send to Event Hub");
                return;
            }

            var client = await GetProducerClientAsync(cancellationToken);
            var batches = CreateBatches(groupList, "group_data");

            foreach (var batch in batches)
            {
                await client.SendAsync(batch, cancellationToken);
                _logger.LogDebug("Sent batch of {Count} group events to Event Hub", batch.Count);
            }

            _logger.LogInformation("Successfully sent {Count} groups to Event Hub", groupList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send group data to Event Hub");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task SendEventDataAsync(IEnumerable<DomainEvent> events, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("SendEventDataAsync");
        var eventList = events.ToList();
        activity?.SetTag("eventCount", eventList.Count);

        try
        {
            if (!eventList.Any())
            {
                _logger.LogDebug("No events to send to Event Hub");
                return;
            }

            var client = await GetProducerClientAsync(cancellationToken);
            var batches = CreateBatches(eventList, "domain_event");

            foreach (var batch in batches)
            {
                await client.SendAsync(batch, cancellationToken);
                _logger.LogDebug("Sent batch of {Count} domain events to Event Hub", batch.Count);
            }

            _logger.LogInformation("Successfully sent {Count} domain events to Event Hub", eventList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event data to Event Hub");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var isConnected = await TestConnectionAsync(cancellationToken);
            
            return new HealthStatus
            {
                IsHealthy = isConnected,
                Message = isConnected ? "Event Hub connection is healthy" : "Failed to connect to Event Hub",
                Details = new Dictionary<string, object>
                {
                    ["HubName"] = _config.HubName,
                    ["BatchSize"] = _config.BatchSize,
                    ["MaxWaitTimeSeconds"] = _config.MaxWaitTimeSeconds
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthStatus
            {
                IsHealthy = false,
                Message = ex.Message,
                Details = new Dictionary<string, object>
                {
                    ["Exception"] = ex.GetType().Name
                }
            };
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await GetProducerClientAsync(cancellationToken);
            
            // Get Event Hub properties to test connection
            var properties = await client.GetEventHubPropertiesAsync(cancellationToken);
            return properties != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Event Hub connection test failed");
            return false;
        }
    }

    private async Task<EventHubProducerClient> GetProducerClientAsync(CancellationToken cancellationToken = default)
    {
        if (_producerClient != null)
        {
            return _producerClient;
        }

        lock (_clientLock)
        {
            if (_producerClient != null)
            {
                return _producerClient;
            }

            _producerClient = new EventHubProducerClient(_config.ConnectionString, _config.HubName);
        }

        return _producerClient;
    }

    private List<EventDataBatch> CreateBatches<T>(IEnumerable<T> items, string eventType)
    {
        var batches = new List<EventDataBatch>();
        var client = GetProducerClientAsync().Result;
        var currentBatch = client.CreateBatchAsync().Result;

        foreach (var item in items)
        {
            var eventData = CreateEventData(item, eventType);
            
            if (!currentBatch.TryAdd(eventData))
            {
                // Current batch is full, start a new one
                if (currentBatch.Count > 0)
                {
                    batches.Add(currentBatch);
                }
                
                currentBatch = client.CreateBatchAsync().Result;
                
                if (!currentBatch.TryAdd(eventData))
                {
                    _logger.LogWarning("Single event data is too large for batch, skipping");
                    continue;
                }
            }
        }

        // Add the last batch if it has events
        if (currentBatch.Count > 0)
        {
            batches.Add(currentBatch);
        }

        return batches;
    }

    private EventData CreateEventData<T>(T item, string eventType)
    {
        var json = JsonSerializer.Serialize(item, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var eventData = new EventData(json);
        
        // Add metadata
        eventData.Properties.Add("eventType", eventType);
        eventData.Properties.Add("timestamp", DateTimeOffset.UtcNow);
        eventData.Properties.Add("source", "permafrost-edge-service");
        eventData.Properties.Add("version", "1.0.0");

        // Add content type
        eventData.ContentType = "application/json";

        return eventData;
    }

    public void Dispose()
    {
        _producerClient?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(30));
        _activitySource?.Dispose();
    }
}
