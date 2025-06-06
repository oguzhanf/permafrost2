using Microsoft.EntityFrameworkCore;
using Permafrost2.Data;
using Permafrost2.Data.Models;

namespace Permafrost2.CustomerPortal.Services;

public class UserService : IUserService
{
    private readonly PermafrostDbContext _context;

    public UserService(PermafrostDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        return await _context.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName ?? u.Username)
            .ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.GroupMemberships)
            .ThenInclude(gm => gm.Group)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<IEnumerable<Group>> GetUserGroupsAsync(Guid userId)
    {
        return await _context.UserGroupMemberships
            .Include(ugm => ugm.Group)
            .Where(ugm => ugm.UserId == userId && ugm.IsActive && ugm.Group.IsActive)
            .Select(ugm => ugm.Group)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<ApplicationPermission>> GetUserPermissionsAsync(Guid userId)
    {
        // Get direct user permissions
        var directPermissions = await _context.ApplicationPermissions
            .Include(ap => ap.Application)
            .Include(ap => ap.Permission)
            .Where(ap => ap.PrincipalId == userId && 
                        ap.PrincipalType == "User" && 
                        ap.IsActive)
            .ToListAsync();

        // Get permissions through group memberships
        var userGroupIds = await _context.UserGroupMemberships
            .Where(ugm => ugm.UserId == userId && ugm.IsActive)
            .Select(ugm => ugm.GroupId)
            .ToListAsync();

        var groupPermissions = await _context.ApplicationPermissions
            .Include(ap => ap.Application)
            .Include(ap => ap.Permission)
            .Where(ap => userGroupIds.Contains(ap.PrincipalId) && 
                        ap.PrincipalType == "Group" && 
                        ap.IsActive)
            .ToListAsync();

        return directPermissions.Concat(groupPermissions)
            .OrderBy(ap => ap.Application.Name)
            .ThenBy(ap => ap.Permission.Name);
    }

    public async Task<Dictionary<string, int>> GetUserStatsAsync()
    {
        var stats = new Dictionary<string, int>();

        stats["TotalUsers"] = await _context.Users
            .CountAsync(u => u.IsActive);

        stats["UsersWithGroups"] = await _context.UserGroupMemberships
            .Where(ugm => ugm.IsActive)
            .Select(ugm => ugm.UserId)
            .Distinct()
            .CountAsync();

        stats["UsersWithDirectPermissions"] = await _context.ApplicationPermissions
            .Where(ap => ap.PrincipalType == "User" && ap.IsActive)
            .Select(ap => ap.PrincipalId)
            .Distinct()
            .CountAsync();

        stats["TotalGroups"] = await _context.Groups
            .CountAsync(g => g.IsActive);

        return stats;
    }
}
