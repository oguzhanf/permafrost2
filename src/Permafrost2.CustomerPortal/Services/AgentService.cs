using Microsoft.EntityFrameworkCore;
using Permafrost2.Data;
using Permafrost2.Data.Models;
using Permafrost2.Shared.DTOs;

namespace Permafrost2.CustomerPortal.Services;

public class AgentService : IAgentService
{
    private readonly PermafrostDbContext _context;

    public AgentService(PermafrostDbContext context)
    {
        _context = context;
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

    public async Task<AgentStatusDto?> GetAgentByIdAsync(Guid agentId)
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

    public async Task<IEnumerable<AgentDataSubmission>> GetAgentSubmissionsAsync(Guid agentId, int limit = 50)
    {
        return await _context.AgentDataSubmissions
            .Where(s => s.AgentId == agentId)
            .OrderByDescending(s => s.SubmittedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetAgentStatsAsync()
    {
        var stats = new Dictionary<string, int>();

        stats["TotalAgents"] = await _context.Agents.CountAsync(a => a.IsActive);
        stats["OnlineAgents"] = await _context.Agents.CountAsync(a => a.IsActive && a.IsOnline);
        stats["DomainControllerAgents"] = await _context.Agents.CountAsync(a => a.IsActive && a.Type == "DomainController");
        stats["ServerAgents"] = await _context.Agents.CountAsync(a => a.IsActive && a.Type == "Server");
        stats["WorkstationAgents"] = await _context.Agents.CountAsync(a => a.IsActive && a.Type == "Workstation");
        stats["TotalSubmissions"] = await _context.AgentDataSubmissions.CountAsync();
        stats["TodaySubmissions"] = await _context.AgentDataSubmissions
            .CountAsync(s => s.SubmittedAt.Date == DateTime.Today);

        return stats;
    }
}
