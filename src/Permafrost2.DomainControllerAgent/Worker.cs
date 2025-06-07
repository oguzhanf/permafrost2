using Permafrost2.DomainControllerAgent.Services;
using Permafrost2.Shared.DTOs;
using System.Text.Json;

namespace Permafrost2.DomainControllerAgent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IEnhancedLogger _enhancedLogger;
    private readonly IConfiguration _configuration;
    private readonly IApiClient _apiClient;
    private readonly IDomainDataCollector _domainDataCollector;
    private readonly Services.IConfigurationManager _configurationManager;
    private readonly IUpdateService _updateService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IErrorReportingService _errorReportingService;
    private readonly IRecoveryService _recoveryService;
    private readonly ICertificateService _certificateService;

    private Guid? _agentId;
    private DateTime _lastHeartbeat = DateTime.MinValue;
    private DateTime _lastDataCollection = DateTime.MinValue;
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private DateTime _lastErrorReport = DateTime.MinValue;
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private AgentConfigurationDto _agentConfiguration = new();

    public Worker(
        ILogger<Worker> logger,
        IEnhancedLogger enhancedLogger,
        IConfiguration configuration,
        IApiClient apiClient,
        IDomainDataCollector domainDataCollector,
        Services.IConfigurationManager configurationManager,
        IUpdateService updateService,
        IServiceProvider serviceProvider,
        IErrorReportingService errorReportingService,
        IRecoveryService recoveryService,
        ICertificateService certificateService)
    {
        _logger = logger;
        _enhancedLogger = enhancedLogger;
        _configuration = configuration;
        _apiClient = apiClient;
        _domainDataCollector = domainDataCollector;
        _configurationManager = configurationManager;
        _updateService = updateService;
        _serviceProvider = serviceProvider;
        _errorReportingService = errorReportingService;
        _recoveryService = recoveryService;
        _certificateService = certificateService;
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

                // Check for updates if needed
                await CheckForUpdatesIfNeededAsync();

                // Report errors if needed
                await ReportErrorsIfNeededAsync();

                // Perform health checks and recovery if needed
                await PerformHealthCheckAndRecoveryAsync();

                // Check certificate renewal if needed
                await CheckCertificateRenewalIfNeededAsync();

                // Wait for next cycle
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in main worker loop");
                await _errorReportingService.ReportErrorAsync(
                    AgentErrorSeverity.High,
                    AgentErrorCategory.Service,
                    "Worker.ExecuteAsync",
                    "Error in main worker loop",
                    ex);

                // Attempt recovery from service crash
                if (_recoveryService.ShouldAttemptRecovery(RecoveryScenario.ServiceCrash))
                {
                    var recoveryResult = await _recoveryService.AttemptRecoveryAsync(RecoveryScenario.ServiceCrash, ex);
                    if (recoveryResult.Success)
                    {
                        _logger.LogInformation("Successfully recovered from service crash");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to recover from service crash: {Error}", recoveryResult.ErrorMessage);
                    }
                }

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
            await _errorReportingService.ReportErrorAsync(
                AgentErrorSeverity.Critical,
                AgentErrorCategory.Service,
                "Worker.InitializeAgentAsync",
                "Failed to initialize agent",
                ex);
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

                    // Attempt API connection recovery
                    if (_recoveryService.ShouldAttemptRecovery(RecoveryScenario.ApiConnection))
                    {
                        var recoveryResult = await _recoveryService.AttemptRecoveryAsync(RecoveryScenario.ApiConnection);
                        if (recoveryResult.Success)
                        {
                            _logger.LogInformation("API connection recovered after heartbeat failure");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send heartbeat");
                await _errorReportingService.ReportErrorAsync(
                    AgentErrorSeverity.Medium,
                    AgentErrorCategory.NetworkConnectivity,
                    "Worker.SendHeartbeatIfNeededAsync",
                    "Failed to send heartbeat",
                    ex);
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
        using var operation = _enhancedLogger.BeginOperation($"CollectAndSubmit{dataType}", new { DataType = dataType });

        try
        {
            _enhancedLogger.LogInformation($"Collecting {dataType} data...", new { DataType = dataType });

            byte[] data;
            int recordCount;

            // Data collection phase
            using (var collectionOperation = _enhancedLogger.BeginOperation($"Collect{dataType}Data"))
            {
                switch (dataType.ToLower())
                {
                    case "users":
                        var users = await _domainDataCollector.CollectUsersAsync();
                        data = JsonSerializer.SerializeToUtf8Bytes(users);
                        recordCount = users.Count;
                        collectionOperation.AddMetric("RecordCount", recordCount);
                        collectionOperation.AddMetric("DataSizeBytes", data.Length);
                        break;

                    case "groups":
                        var groups = await _domainDataCollector.CollectGroupsAsync();
                        data = JsonSerializer.SerializeToUtf8Bytes(groups);
                        recordCount = groups.Count;
                        collectionOperation.AddMetric("RecordCount", recordCount);
                        collectionOperation.AddMetric("DataSizeBytes", data.Length);
                        break;

                    case "policies":
                        var policies = await _domainDataCollector.CollectPoliciesAsync();
                        data = JsonSerializer.SerializeToUtf8Bytes(policies);
                        recordCount = policies.Count;
                        collectionOperation.AddMetric("RecordCount", recordCount);
                        collectionOperation.AddMetric("DataSizeBytes", data.Length);
                        break;

                    case "organizationalunits":
                        var organizationalUnits = await _domainDataCollector.CollectOrganizationalUnitsAsync();
                        data = JsonSerializer.SerializeToUtf8Bytes(organizationalUnits);
                        recordCount = organizationalUnits.Count;
                        collectionOperation.AddMetric("RecordCount", recordCount);
                        collectionOperation.AddMetric("DataSizeBytes", data.Length);
                        break;

                    case "trustrelationships":
                        var trustRelationships = await _domainDataCollector.CollectTrustRelationshipsAsync();
                        data = JsonSerializer.SerializeToUtf8Bytes(trustRelationships);
                        recordCount = trustRelationships.Count;
                        collectionOperation.AddMetric("RecordCount", recordCount);
                        collectionOperation.AddMetric("DataSizeBytes", data.Length);
                        break;

                    default:
                        _enhancedLogger.LogWarning($"Unknown data type: {dataType}", new { DataType = dataType });
                        operation.MarkAsFailed($"Unknown data type: {dataType}");
                        return;
                }
            }

            operation.AddMetric("RecordCount", recordCount);
            operation.AddMetric("DataSizeBytes", data.Length);

            if (recordCount > 0)
            {
                // Data submission phase
                using (var submissionOperation = _enhancedLogger.BeginOperation($"Submit{dataType}Data"))
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

                    submissionOperation.AddMetric("RecordCount", recordCount);
                    submissionOperation.AddMetric("DataSizeBytes", data.Length);

                    var response = await _apiClient.SubmitDataAsync(request);
                    if (response.Success)
                    {
                        _enhancedLogger.LogDataCollection(dataType, recordCount, TimeSpan.Zero, true);
                        _enhancedLogger.LogInformation($"Successfully submitted {recordCount} {dataType} records",
                            new { DataType = dataType, RecordCount = recordCount });
                        submissionOperation.MarkAsSuccessful();
                        operation.MarkAsSuccessful();
                    }
                    else
                    {
                        var errorMessage = $"Failed to submit {dataType} data: {response.Message}";
                        _enhancedLogger.LogDataCollection(dataType, recordCount, TimeSpan.Zero, false, errorMessage);
                        _enhancedLogger.LogError(new Exception(errorMessage), errorMessage,
                            new { DataType = dataType, RecordCount = recordCount, ResponseMessage = response.Message });
                        submissionOperation.MarkAsFailed(errorMessage);
                        operation.MarkAsFailed(errorMessage);
                    }
                }
            }
            else
            {
                _enhancedLogger.LogInformation($"No {dataType} data to submit", new { DataType = dataType });
                operation.MarkAsSuccessful();
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to collect and submit {dataType} data";
            _enhancedLogger.LogError(ex, errorMessage, new { DataType = dataType });
            operation.MarkAsFailed(ex.Message);
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

    private async Task CheckForUpdatesIfNeededAsync()
    {
        if (_agentId == null) return;

        var now = DateTime.UtcNow;
        var updateCheckInterval = TimeSpan.FromHours(6); // Check for updates every 6 hours

        if (now - _lastUpdateCheck >= updateCheckInterval)
        {
            try
            {
                _logger.LogInformation("Checking for agent updates...");

                var updateResult = await _updateService.CheckForUpdatesAsync();
                if (updateResult.UpdateAvailable && updateResult.UpdateInfo != null)
                {
                    _logger.LogInformation("Update available: {Version}", updateResult.UpdateInfo.Version);

                    // Check if there's a scheduled update
                    var scheduledUpdate = await _updateService.GetScheduledUpdateInfoAsync();
                    if (scheduledUpdate == null)
                    {
                        // Schedule update for next maintenance window (2 AM)
                        var nextMaintenanceWindow = GetNextMaintenanceWindow();

                        if (updateResult.UpdateInfo.IsCritical)
                        {
                            // Apply critical updates immediately
                            _logger.LogWarning("Critical update available, applying immediately");
                            await ApplyUpdateAsync(updateResult.UpdateInfo);
                        }
                        else
                        {
                            // Schedule non-critical updates for maintenance window
                            await _updateService.ScheduleUpdateAsync(updateResult.UpdateInfo, nextMaintenanceWindow);
                            _logger.LogInformation("Update scheduled for {ScheduledTime}", nextMaintenanceWindow);
                        }
                    }
                }
                else if (updateResult.ErrorMessage != null)
                {
                    _logger.LogWarning("Update check failed: {Error}", updateResult.ErrorMessage);
                }
                else
                {
                    _logger.LogDebug("No updates available");
                }

                _lastUpdateCheck = now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check for updates");
            }
        }

        // Check if it's time to apply a scheduled update
        await CheckScheduledUpdateAsync();
    }

    private async Task CheckScheduledUpdateAsync()
    {
        try
        {
            var scheduledUpdate = await _updateService.GetScheduledUpdateInfoAsync();
            if (scheduledUpdate != null && DateTime.UtcNow >= scheduledUpdate.ScheduledTime)
            {
                _logger.LogInformation("Applying scheduled update: {Version}", scheduledUpdate.UpdateInfo.Version);
                await ApplyUpdateAsync(scheduledUpdate.UpdateInfo);
                await _updateService.CancelScheduledUpdateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check scheduled update");
        }
    }

    private async Task ApplyUpdateAsync(Permafrost2.Shared.DTOs.UpdateInfo updateInfo)
    {
        try
        {
            _logger.LogInformation("Applying update to version {Version}", updateInfo.Version);

            var result = await _updateService.ApplyUpdateAsync(updateInfo);
            if (result.Success)
            {
                _logger.LogInformation("Update applied successfully. Backup created at: {BackupPath}", result.BackupPath);

                if (result.RequiresRestart)
                {
                    _logger.LogInformation("Update requires restart. Restarting service...");
                    // The service will be restarted by the installer
                    Environment.Exit(0);
                }
            }
            else
            {
                _logger.LogError("Update failed: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply update");
        }
    }

    private DateTime GetNextMaintenanceWindow()
    {
        var now = DateTime.Now;
        var maintenanceTime = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0); // 2 AM

        if (now >= maintenanceTime)
        {
            // If it's already past 2 AM today, schedule for tomorrow
            maintenanceTime = maintenanceTime.AddDays(1);
        }

        return maintenanceTime;
    }

    private async Task ReportErrorsIfNeededAsync()
    {
        if (_agentId == null) return;

        var now = DateTime.UtcNow;
        var errorReportInterval = TimeSpan.FromMinutes(15); // Report errors every 15 minutes

        if (now - _lastErrorReport >= errorReportInterval)
        {
            try
            {
                var pendingErrors = await _errorReportingService.GetPendingErrorsAsync();
                if (pendingErrors.Count > 0)
                {
                    _logger.LogDebug("Reporting {ErrorCount} errors to server", pendingErrors.Count);

                    var request = new AgentErrorReportRequest
                    {
                        AgentId = _agentId.Value,
                        Errors = pendingErrors,
                        ReportedAt = now
                    };

                    var response = await _apiClient.ReportErrorsAsync(request);
                    if (response.Success)
                    {
                        // Mark errors as sent
                        var errorIds = pendingErrors.Select(e => e.ErrorId).ToList();
                        await _errorReportingService.MarkErrorsAsSentAsync(errorIds);

                        _logger.LogInformation("Successfully reported {ProcessedCount} errors to server ({DuplicateCount} duplicates)",
                            response.ProcessedErrorCount, response.DuplicateErrorCount);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to report errors to server: {Message}", response.Message);
                        await _errorReportingService.ReportErrorAsync(
                            AgentErrorSeverity.Medium,
                            AgentErrorCategory.NetworkConnectivity,
                            "Worker.ReportErrorsIfNeededAsync",
                            $"Failed to report errors to server: {response.Message}");
                    }
                }

                // Cleanup old errors (older than 7 days)
                await _errorReportingService.CleanupOldErrorsAsync(TimeSpan.FromDays(7));

                _lastErrorReport = now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to report errors to server");
                await _errorReportingService.ReportErrorAsync(
                    AgentErrorSeverity.Medium,
                    AgentErrorCategory.Service,
                    "Worker.ReportErrorsIfNeededAsync",
                    "Failed to report errors to server",
                    ex);
            }
        }
    }

    private async Task PerformHealthCheckAndRecoveryAsync()
    {
        var now = DateTime.UtcNow;
        var healthCheckInterval = TimeSpan.FromMinutes(10); // Health check every 10 minutes

        if (now - _lastHealthCheck >= healthCheckInterval)
        {
            try
            {
                _logger.LogDebug("Performing health check");

                var healthResult = await _recoveryService.PerformHealthCheckAsync();

                if (!healthResult.IsHealthy)
                {
                    _logger.LogWarning("Health check failed: {Status}", healthResult.OverallStatus);

                    // Check each component and attempt recovery if needed
                    foreach (var component in healthResult.ComponentResults.Where(c => !c.IsHealthy))
                    {
                        await AttemptComponentRecoveryAsync(component);
                    }

                    // Report health check failure
                    await _errorReportingService.ReportErrorAsync(
                        AgentErrorSeverity.Medium,
                        AgentErrorCategory.Service,
                        "Worker.PerformHealthCheckAndRecoveryAsync",
                        $"Health check failed: {healthResult.OverallStatus}");
                }
                else
                {
                    _logger.LogDebug("Health check passed");

                    // Exit degradation mode if we're healthy
                    var degradationStatus = _recoveryService.GetDegradationStatus();
                    if (degradationStatus.IsActive)
                    {
                        await _recoveryService.ExitDegradationModeAsync();
                        _logger.LogInformation("Exited degradation mode - system is healthy");
                    }
                }

                _lastHealthCheck = now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform health check");
                await _errorReportingService.ReportErrorAsync(
                    AgentErrorSeverity.Medium,
                    AgentErrorCategory.Service,
                    "Worker.PerformHealthCheckAndRecoveryAsync",
                    "Failed to perform health check",
                    ex);
            }
        }
    }

    private async Task AttemptComponentRecoveryAsync(ComponentHealth component)
    {
        RecoveryScenario scenario = component.ComponentName switch
        {
            "API Connectivity" => RecoveryScenario.ApiConnection,
            "Domain Connectivity" => RecoveryScenario.DomainConnection,
            "Configuration Integrity" => RecoveryScenario.ConfigurationCorruption,
            "System Resources" => component.ErrorMessage?.Contains("memory") == true
                ? RecoveryScenario.MemoryPressure
                : RecoveryScenario.DiskSpace,
            _ => RecoveryScenario.ServiceCrash
        };

        if (_recoveryService.ShouldAttemptRecovery(scenario))
        {
            _logger.LogInformation("Attempting recovery for unhealthy component: {Component}", component.ComponentName);

            var recoveryResult = await _recoveryService.AttemptRecoveryAsync(scenario);

            if (recoveryResult.Success)
            {
                _logger.LogInformation("Successfully recovered component: {Component} in {Duration}ms",
                    component.ComponentName, recoveryResult.RecoveryDuration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("Failed to recover component: {Component} - {Error}",
                    component.ComponentName, recoveryResult.ErrorMessage);
            }
        }
    }

    private async Task CheckCertificateRenewalIfNeededAsync()
    {
        if (_agentId == null) return;

        try
        {
            var certificateConfig = await _certificateService.GetCertificateConfigurationAsync();

            // Only check if certificate authentication is enabled and auto-renewal is enabled
            if (!certificateConfig.UseCertificateAuthentication || !certificateConfig.AutoRenewCertificate)
                return;

            if (string.IsNullOrEmpty(certificateConfig.CertificateThumbprint))
            {
                // No certificate configured, try to generate one
                await InitializeCertificateAsync();
                return;
            }

            // Check if certificate needs renewal
            var needsRenewal = await _certificateService.NeedsRenewalAsync(
                certificateConfig.CertificateThumbprint,
                certificateConfig.RenewalThresholdDays);

            if (needsRenewal)
            {
                _logger.LogInformation("Certificate needs renewal, requesting new certificate from server");

                var renewalRequest = new CertificateRenewalRequest
                {
                    AgentId = _agentId.Value,
                    CurrentThumbprint = certificateConfig.CertificateThumbprint,
                    ValidityDays = 365,
                    RevokeOldCertificate = true
                };

                var renewalResponse = await _apiClient.RenewCertificateAsync(renewalRequest);
                if (renewalResponse.Success)
                {
                    // Install the new certificate
                    var installed = await _certificateService.InstallCertificateAsync(
                        renewalResponse.NewCertificateData!,
                        renewalResponse.NewPrivateKeyData!);

                    if (installed)
                    {
                        // Update configuration with new certificate thumbprint
                        certificateConfig.CertificateThumbprint = renewalResponse.NewThumbprint;
                        await _certificateService.UpdateCertificateConfigurationAsync(certificateConfig);

                        _logger.LogInformation("Certificate renewed successfully, new thumbprint: {Thumbprint}",
                            renewalResponse.NewThumbprint);
                    }
                    else
                    {
                        _logger.LogError("Failed to install renewed certificate");
                        await _errorReportingService.ReportErrorAsync(
                            AgentErrorSeverity.High,
                            AgentErrorCategory.Configuration,
                            "Worker.CheckCertificateRenewalIfNeededAsync",
                            "Failed to install renewed certificate");
                    }
                }
                else
                {
                    _logger.LogError("Certificate renewal failed: {Message}", renewalResponse.Message);
                    await _errorReportingService.ReportErrorAsync(
                        AgentErrorSeverity.High,
                        AgentErrorCategory.NetworkConnectivity,
                        "Worker.CheckCertificateRenewalIfNeededAsync",
                        $"Certificate renewal failed: {renewalResponse.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check certificate renewal");
            await _errorReportingService.ReportErrorAsync(
                AgentErrorSeverity.Medium,
                AgentErrorCategory.Configuration,
                "Worker.CheckCertificateRenewalIfNeededAsync",
                "Failed to check certificate renewal",
                ex);
        }
    }

    private async Task InitializeCertificateAsync()
    {
        if (_agentId == null) return;

        try
        {
            _logger.LogInformation("Initializing certificate for agent authentication");

            var request = new CertificateGenerationRequest
            {
                AgentId = _agentId.Value,
                CommonName = $"{Environment.MachineName}.{Environment.UserDomainName}",
                Organization = "Permafrost2",
                OrganizationalUnit = "Domain Controller Agent",
                ValidityDays = 365
            };

            var response = await _apiClient.RequestCertificateAsync(request);
            if (response.Success)
            {
                // Install the certificate
                var installed = await _certificateService.InstallCertificateAsync(
                    response.CertificateData!,
                    response.PrivateKeyData!);

                if (installed)
                {
                    // Update configuration to use the new certificate
                    var certificateConfig = await _certificateService.GetCertificateConfigurationAsync();
                    certificateConfig.UseCertificateAuthentication = true;
                    certificateConfig.CertificateThumbprint = response.Thumbprint;
                    await _certificateService.UpdateCertificateConfigurationAsync(certificateConfig);

                    _logger.LogInformation("Certificate initialized successfully, thumbprint: {Thumbprint}",
                        response.Thumbprint);
                }
                else
                {
                    _logger.LogError("Failed to install generated certificate");
                    await _errorReportingService.ReportErrorAsync(
                        AgentErrorSeverity.High,
                        AgentErrorCategory.Configuration,
                        "Worker.InitializeCertificateAsync",
                        "Failed to install generated certificate");
                }
            }
            else
            {
                _logger.LogError("Certificate generation failed: {Message}", response.Message);
                await _errorReportingService.ReportErrorAsync(
                    AgentErrorSeverity.High,
                    AgentErrorCategory.NetworkConnectivity,
                    "Worker.InitializeCertificateAsync",
                    $"Certificate generation failed: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize certificate");
            await _errorReportingService.ReportErrorAsync(
                AgentErrorSeverity.Medium,
                AgentErrorCategory.Configuration,
                "Worker.InitializeCertificateAsync",
                "Failed to initialize certificate",
                ex);
        }
    }
}
