using Microsoft.EntityFrameworkCore;
using Permafrost2.Data;
using Permafrost2.Data.Models;

namespace Permafrost2.CustomerPortal.Services;

public class ApplicationService : IApplicationService
{
    private readonly PermafrostDbContext _context;

    public ApplicationService(PermafrostDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Application>> GetApplicationsAsync()
    {
        return await _context.Applications
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<Application?> GetApplicationByIdAsync(Guid id)
    {
        return await _context.Applications
            .Include(a => a.ApplicationPermissions)
            .ThenInclude(ap => ap.Permission)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<ApplicationPermission>> GetApplicationPermissionsAsync(Guid applicationId)
    {
        return await _context.ApplicationPermissions
            .Include(ap => ap.Permission)
            .Include(ap => ap.Application)
            .Where(ap => ap.ApplicationId == applicationId && ap.IsActive)
            .OrderBy(ap => ap.Permission.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetApplicationUsersAsync(Guid applicationId)
    {
        var userIds = await _context.ApplicationPermissions
            .Where(ap => ap.ApplicationId == applicationId && 
                        ap.PrincipalType == "User" && 
                        ap.IsActive)
            .Select(ap => ap.PrincipalId)
            .Distinct()
            .ToListAsync();

        return await _context.Users
            .Where(u => userIds.Contains(u.Id) && u.IsActive)
            .OrderBy(u => u.DisplayName ?? u.Username)
            .ToListAsync();
    }

    public async Task<IEnumerable<Group>> GetApplicationGroupsAsync(Guid applicationId)
    {
        var groupIds = await _context.ApplicationPermissions
            .Where(ap => ap.ApplicationId == applicationId && 
                        ap.PrincipalType == "Group" && 
                        ap.IsActive)
            .Select(ap => ap.PrincipalId)
            .Distinct()
            .ToListAsync();

        return await _context.Groups
            .Where(g => groupIds.Contains(g.Id) && g.IsActive)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetApplicationStatsAsync()
    {
        var stats = new Dictionary<string, int>();

        stats["TotalApplications"] = await _context.Applications
            .CountAsync(a => a.IsActive);

        stats["ApplicationsWithUsers"] = await _context.ApplicationPermissions
            .Where(ap => ap.PrincipalType == "User" && ap.IsActive)
            .Select(ap => ap.ApplicationId)
            .Distinct()
            .CountAsync();

        stats["ApplicationsWithGroups"] = await _context.ApplicationPermissions
            .Where(ap => ap.PrincipalType == "Group" && ap.IsActive)
            .Select(ap => ap.ApplicationId)
            .Distinct()
            .CountAsync();

        stats["TotalPermissions"] = await _context.ApplicationPermissions
            .CountAsync(ap => ap.IsActive);

        return stats;
    }
}
