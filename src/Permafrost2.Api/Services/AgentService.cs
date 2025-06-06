using Microsoft.EntityFrameworkCore;
using Permafrost2.Data;
using Permafrost2.Data.Models;
using Permafrost2.Shared.DTOs;
using System.Text.Json;

namespace Permafrost2.Api.Services;

public class AgentService : IAgentService
{
    private readonly PermafrostDbContext _context;
    private readonly ILogger<AgentService> _logger;

    public AgentService(PermafrostDbContext context, ILogger<AgentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AgentRegistrationResponse> RegisterAgentAsync(AgentRegistrationRequest request)
    {
        try
        {
            // Check if agent already exists
            var existingAgent = await _context.Agents
                .FirstOrDefaultAsync(a => a.MachineName == request.MachineName && a.Type == request.Type);

            Agent agent;
            if (existingAgent != null)
            {
                // Update existing agent
                existingAgent.Name = request.Name;
                existingAgent.Version = request.Version;
                existingAgent.IpAddress = request.IpAddress;
                existingAgent.Domain = request.Domain;
                existingAgent.OperatingSystem = request.OperatingSystem;
                existingAgent.Configuration = request.Configuration != null ? JsonSerializer.Serialize(request.Configuration) : null;
                existingAgent.IsActive = true;
                existingAgent.LastUpdated = DateTime.UtcNow;
                agent = existingAgent;
            }
            else
            {
                // Create new agent
                agent = new Agent
                {
                    Name = request.Name,
                    Type = request.Type,
                    Version = request.Version,
                    MachineName = request.MachineName,
                    IpAddress = request.IpAddress,
                    Domain = request.Domain,
                    OperatingSystem = request.OperatingSystem,
                    Configuration = request.Configuration != null ? JsonSerializer.Serialize(request.Configuration) : null,
                    IsActive = true,
                    Status = "Registered"
                };
                _context.Agents.Add(agent);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Agent {AgentName} ({Type}) registered successfully from {MachineName}", 
                request.Name, request.Type, request.MachineName);

            return new AgentRegistrationResponse
            {
                AgentId = agent.Id,
                Success = true,
                Message = "Agent registered successfully",
                ApiKey = GenerateApiKey(agent.Id),
                Configuration = GetDefaultConfiguration(request.Type)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent {AgentName} from {MachineName}", 
                request.Name, request.MachineName);
            
            return new AgentRegistrationResponse
            {
                Success = false,
                Message = "Registration failed: " + ex.Message
            };
        }
    }

    public async Task<AgentHeartbeatResponse> ProcessHeartbeatAsync(AgentHeartbeatRequest request)
    {
        try
        {
            var agent = await _context.Agents.FindAsync(request.AgentId);
            if (agent == null)
            {
                return new AgentHeartbeatResponse
                {
                    Success = false,
                    Message = "Agent not found"
                };
            }

            agent.LastHeartbeat = DateTime.UtcNow;
            agent.Status = request.Status;
            agent.StatusMessage = request.StatusMessage;
            agent.IsOnline = true;

            await _context.SaveChangesAsync();

            return new AgentHeartbeatResponse
            {
                Success = true,
                Message = "Heartbeat processed",
                UpdateAvailable = false // TODO: Implement update checking
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process heartbeat for agent {AgentId}", request.AgentId);
            
            return new AgentHeartbeatResponse
            {
                Success = false,
                Message = "Heartbeat processing failed"
            };
        }
    }

    public async Task<DataSubmissionResponse> ProcessDataSubmissionAsync(DataSubmissionRequest request)
    {
        try
        {
            var agent = await _context.Agents.FindAsync(request.AgentId);
            if (agent == null)
            {
                return new DataSubmissionResponse
                {
                    Success = false,
                    Message = "Agent not found"
                };
            }

            var submission = new AgentDataSubmission
            {
                AgentId = request.AgentId,
                DataType = request.DataType,
                RecordCount = request.RecordCount,
                DataSizeBytes = request.Data.Length,
                FileHash = request.DataHash,
                Metadata = request.Metadata,
                Status = "Pending"
            };

            _context.AgentDataSubmissions.Add(submission);

            // Process the data based on type
            await ProcessSubmittedData(request, submission);

            agent.LastDataCollection = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Data submission {SubmissionId} processed for agent {AgentId}, type: {DataType}, records: {RecordCount}",
                submission.Id, request.AgentId, request.DataType, request.RecordCount);

            return new DataSubmissionResponse
            {
                SubmissionId = submission.Id,
                Success = true,
                Message = "Data submitted successfully",
                ProcessedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process data submission for agent {AgentId}", request.AgentId);
            
            return new DataSubmissionResponse
            {
                Success = false,
                Message = "Data submission failed: " + ex.Message
            };
        }
    }

    public async Task<IEnumerable<AgentStatusDto>> GetAgentsAsync()
    {
        var agents = await _context.Agents
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return agents.Select(a => new AgentStatusDto
        {
            Id = a.Id,
            Name = a.Name,
            Type = a.Type,
            Version = a.Version,
            MachineName = a.MachineName,
            IpAddress = a.IpAddress,
            Domain = a.Domain,
            IsActive = a.IsActive,
            IsOnline = a.IsOnline,
            Status = a.Status,
            StatusMessage = a.StatusMessage,
            RegisteredAt = a.RegisteredAt,
            LastHeartbeat = a.LastHeartbeat,
            LastDataCollection = a.LastDataCollection
        });
    }

    public async Task<AgentStatusDto?> GetAgentAsync(Guid agentId)
    {
        var agent = await _context.Agents.FindAsync(agentId);
        if (agent == null) return null;

        return new AgentStatusDto
        {
            Id = agent.Id,
            Name = agent.Name,
            Type = agent.Type,
            Version = agent.Version,
            MachineName = agent.MachineName,
            IpAddress = agent.IpAddress,
            Domain = agent.Domain,
            IsActive = agent.IsActive,
            IsOnline = agent.IsOnline,
            Status = agent.Status,
            StatusMessage = agent.StatusMessage,
            RegisteredAt = agent.RegisteredAt,
            LastHeartbeat = agent.LastHeartbeat,
            LastDataCollection = agent.LastDataCollection
        };
    }

    public async Task<bool> UpdateAgentConfigurationAsync(Guid agentId, AgentConfigurationDto configuration)
    {
        var agent = await _context.Agents.FindAsync(agentId);
        if (agent == null) return false;

        agent.Configuration = JsonSerializer.Serialize(configuration);
        agent.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateAgentAsync(Guid agentId)
    {
        var agent = await _context.Agents.FindAsync(agentId);
        if (agent == null) return false;

        agent.IsActive = false;
        agent.IsOnline = false;
        agent.Status = "Deactivated";
        agent.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AgentDataSubmission>> GetAgentSubmissionsAsync(Guid agentId, int limit = 50)
    {
        return await _context.AgentDataSubmissions
            .Where(s => s.AgentId == agentId)
            .OrderByDescending(s => s.SubmittedAt)
            .Take(limit)
            .ToListAsync();
    }

    private string GenerateApiKey(Guid agentId)
    {
        // Simple API key generation - in production, use proper cryptographic methods
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"agent_{agentId}_{DateTime.UtcNow.Ticks}"));
    }

    private AgentConfigurationDto GetDefaultConfiguration(string agentType)
    {
        var config = new AgentConfigurationDto();
        
        switch (agentType.ToLower())
        {
            case "domaincontroller":
                config.EnabledDataTypes = new List<string> { "Users", "Groups", "Policies" };
                config.DataCollectionIntervalMinutes = 60;
                break;
            case "server":
                config.EnabledDataTypes = new List<string> { "Events", "LocalUsers", "LocalGroups" };
                config.DataCollectionIntervalMinutes = 30;
                break;
            case "workstation":
                config.EnabledDataTypes = new List<string> { "Events", "LocalUsers" };
                config.DataCollectionIntervalMinutes = 120;
                break;
        }

        return config;
    }

    private async Task ProcessSubmittedData(DataSubmissionRequest request, AgentDataSubmission submission)
    {
        try
        {
            switch (request.DataType.ToLower())
            {
                case "users":
                    await ProcessUserData(request.Data, submission);
                    break;
                case "groups":
                    await ProcessGroupData(request.Data, submission);
                    break;
                default:
                    _logger.LogWarning("Unknown data type: {DataType}", request.DataType);
                    break;
            }

            submission.Status = "Completed";
            submission.ProcessedAt = DateTime.UtcNow;
            submission.ProcessedCount = submission.RecordCount;
        }
        catch (Exception ex)
        {
            submission.Status = "Failed";
            submission.ErrorDetails = ex.Message;
            submission.ErrorCount = submission.RecordCount;
            _logger.LogError(ex, "Failed to process {DataType} data for submission {SubmissionId}", 
                request.DataType, submission.Id);
        }
    }

    private async Task ProcessUserData(byte[] data, AgentDataSubmission submission)
    {
        // Deserialize user data and save to database
        var json = System.Text.Encoding.UTF8.GetString(data);
        var users = JsonSerializer.Deserialize<List<DomainUserDto>>(json);
        
        if (users == null) return;

        foreach (var userDto in users)
        {
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == userDto.Username);

            if (existingUser != null)
            {
                // Update existing user
                existingUser.Email = userDto.Email;
                existingUser.DisplayName = userDto.DisplayName;
                existingUser.FirstName = userDto.FirstName;
                existingUser.LastName = userDto.LastName;
                existingUser.Department = userDto.Department;
                existingUser.JobTitle = userDto.JobTitle;
                existingUser.Manager = userDto.Manager;
                existingUser.IsActive = userDto.IsActive;
                existingUser.LastUpdated = DateTime.UtcNow;
                existingUser.Source = "DomainController";
            }
            else
            {
                // Create new user
                var user = new User
                {
                    Username = userDto.Username,
                    Email = userDto.Email,
                    DisplayName = userDto.DisplayName,
                    FirstName = userDto.FirstName,
                    LastName = userDto.LastName,
                    Department = userDto.Department,
                    JobTitle = userDto.JobTitle,
                    Manager = userDto.Manager,
                    IsActive = userDto.IsActive,
                    Source = "DomainController",
                    SourceId = userDto.ObjectSid
                };
                _context.Users.Add(user);
            }
        }
    }

    private async Task ProcessGroupData(byte[] data, AgentDataSubmission submission)
    {
        // TODO: Implement group data processing
        await Task.CompletedTask;
    }
}
