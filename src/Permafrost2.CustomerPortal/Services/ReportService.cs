using Microsoft.EntityFrameworkCore;
using Permafrost2.Data;
using Permafrost2.Data.Models;

namespace Permafrost2.CustomerPortal.Services;

public class ReportService : IReportService
{
    private readonly PermafrostDbContext _context;

    public ReportService(PermafrostDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ApplicationAccessReport>> GetApplicationAccessReportsAsync()
    {
        var applications = await _context.Applications
            .Where(a => a.IsActive)
            .ToListAsync();

        var reports = new List<ApplicationAccessReport>();

        foreach (var app in applications)
        {
            var report = await GenerateApplicationAccessReportAsync(app.Id);
            reports.Add(report);
        }

        return reports.OrderBy(r => r.ApplicationName);
    }

    public async Task<ApplicationAccessReport> GenerateApplicationAccessReportAsync(Guid applicationId)
    {
        var application = await _context.Applications
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application == null)
            throw new ArgumentException("Application not found", nameof(applicationId));

        var report = new ApplicationAccessReport
        {
            ApplicationId = applicationId,
            ApplicationName = application.Name,
            GeneratedAt = DateTime.UtcNow
        };

        // Get user permissions
        var userPermissions = await _context.ApplicationPermissions
            .Include(ap => ap.Permission)
            .Where(ap => ap.ApplicationId == applicationId && 
                        ap.PrincipalType == "User" && 
                        ap.IsActive)
            .ToListAsync();

        var userIds = userPermissions.Select(up => up.PrincipalId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u);

        report.UserPermissions = userPermissions
            .GroupBy(up => up.PrincipalId)
            .Select(g => new UserPermissionSummary
            {
                UserId = g.Key,
                UserName = users.ContainsKey(g.Key) ? users[g.Key].Username : "Unknown",
                DisplayName = users.ContainsKey(g.Key) ? users[g.Key].DisplayName ?? users[g.Key].Username : "Unknown",
                Permissions = g.Select(p => p.Permission.Name).ToList(),
                GrantedAt = g.Min(p => p.GrantedAt),
                Source = g.First().Source
            })
            .ToList();

        // Get group permissions
        var groupPermissions = await _context.ApplicationPermissions
            .Include(ap => ap.Permission)
            .Where(ap => ap.ApplicationId == applicationId && 
                        ap.PrincipalType == "Group" && 
                        ap.IsActive)
            .ToListAsync();

        var groupIds = groupPermissions.Select(gp => gp.PrincipalId).Distinct().ToList();
        var groups = await _context.Groups
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g);

        report.GroupPermissions = groupPermissions
            .GroupBy(gp => gp.PrincipalId)
            .Select(g => new GroupPermissionSummary
            {
                GroupId = g.Key,
                GroupName = groups.ContainsKey(g.Key) ? groups[g.Key].Name : "Unknown",
                Permissions = g.Select(p => p.Permission.Name).ToList(),
                GrantedAt = g.Min(p => p.GrantedAt),
                Source = g.First().Source
            })
            .ToList();

        report.TotalUsers = report.UserPermissions.Count;
        report.TotalGroups = report.GroupPermissions.Count;
        report.TotalPermissions = userPermissions.Count + groupPermissions.Count;

        return report;
    }

    public async Task<UserAccessReport> GenerateUserAccessReportAsync(Guid userId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new ArgumentException("User not found", nameof(userId));

        var report = new UserAccessReport
        {
            UserId = userId,
            UserName = user.Username,
            DisplayName = user.DisplayName ?? user.Username,
            GeneratedAt = DateTime.UtcNow
        };

        // Get direct application permissions
        var directPermissions = await _context.ApplicationPermissions
            .Include(ap => ap.Application)
            .Include(ap => ap.Permission)
            .Where(ap => ap.PrincipalId == userId && 
                        ap.PrincipalType == "User" && 
                        ap.IsActive)
            .ToListAsync();

        // Get group memberships
        var groupMemberships = await _context.UserGroupMemberships
            .Include(ugm => ugm.Group)
            .Where(ugm => ugm.UserId == userId && ugm.IsActive)
            .ToListAsync();

        // Get permissions through groups
        var groupIds = groupMemberships.Select(gm => gm.GroupId).ToList();
        var groupPermissions = await _context.ApplicationPermissions
            .Include(ap => ap.Application)
            .Include(ap => ap.Permission)
            .Where(ap => groupIds.Contains(ap.PrincipalId) && 
                        ap.PrincipalType == "Group" && 
                        ap.IsActive)
            .ToListAsync();

        var allPermissions = directPermissions.Concat(groupPermissions);

        report.ApplicationPermissions = allPermissions
            .GroupBy(ap => ap.ApplicationId)
            .Select(g => new ApplicationPermissionSummary
            {
                ApplicationId = g.Key,
                ApplicationName = g.First().Application.Name,
                Permissions = g.Select(p => p.Permission.Name).Distinct().ToList(),
                GrantedAt = g.Min(p => p.GrantedAt),
                Source = string.Join(", ", g.Select(p => p.Source).Distinct())
            })
            .ToList();

        report.GroupMemberships = groupMemberships
            .Select(gm => new GroupMembershipSummary
            {
                GroupId = gm.GroupId,
                GroupName = gm.Group.Name,
                GroupType = gm.Group.Type,
                AssignedAt = gm.AssignedAt,
                Source = gm.Source
            })
            .ToList();

        report.TotalApplications = report.ApplicationPermissions.Count;
        report.TotalGroups = report.GroupMemberships.Count;

        return report;
    }

    public async Task<SystemStatusReport> GetSystemStatusReportAsync()
    {
        var report = new SystemStatusReport
        {
            GeneratedAt = DateTime.UtcNow
        };

        // Get basic counts
        report.TotalUsers = await _context.Users.CountAsync(u => u.IsActive);
        report.TotalGroups = await _context.Groups.CountAsync(g => g.IsActive);
        report.TotalApplications = await _context.Applications.CountAsync(a => a.IsActive);
        report.TotalPermissions = await _context.ApplicationPermissions.CountAsync(ap => ap.IsActive);

        // Get data source statuses
        var dataSources = await _context.DataSources.ToListAsync();
        report.DataSources = dataSources.Select(ds => new DataSourceStatus
        {
            Id = ds.Id,
            Name = ds.Name,
            Type = ds.Type,
            IsEnabled = ds.IsEnabled,
            LastRunAt = ds.LastRunAt,
            LastRunStatus = ds.LastRunStatus
        }).ToList();

        // Get recent data collection runs
        report.RecentRuns = (await GetRecentDataCollectionRunsAsync(5)).ToList();

        return report;
    }

    public async Task<IEnumerable<DataCollectionRun>> GetRecentDataCollectionRunsAsync(int count = 10)
    {
        return await _context.DataCollectionRuns
            .Include(dcr => dcr.DataSource)
            .OrderByDescending(dcr => dcr.StartedAt)
            .Take(count)
            .ToListAsync();
    }
}
