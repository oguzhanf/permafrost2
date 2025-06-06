using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using Permafrost2.Shared.DTOs;

namespace Permafrost2.DomainControllerAgent.Services;

public class DomainDataCollector : IDomainDataCollector
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DomainDataCollector> _logger;

    public DomainDataCollector(IConfiguration configuration, ILogger<DomainDataCollector> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<DomainUserDto>> CollectUsersAsync()
    {
        var users = new List<DomainUserDto>();
        
        try
        {
            var domainName = _configuration["DomainController:DomainName"];
            var useCurrentCredentials = _configuration.GetValue<bool>("DomainController:UseCurrentCredentials", true);
            var maxUsers = _configuration.GetValue<int>("DomainController:MaxUsersPerBatch", 1000);
            var includeDisabled = _configuration.GetValue<bool>("DomainController:IncludeDisabledUsers", false);
            var includeSystem = _configuration.GetValue<bool>("DomainController:IncludeSystemAccounts", false);

            using var context = new PrincipalContext(ContextType.Domain, domainName);
            using var searcher = new PrincipalSearcher(new UserPrincipal(context));
            
            var results = searcher.FindAll();
            var count = 0;

            foreach (UserPrincipal user in results)
            {
                if (count >= maxUsers) break;

                try
                {
                    // Skip disabled users if not included
                    if (!includeDisabled && user.Enabled == false) continue;
                    
                    // Skip system accounts if not included
                    if (!includeSystem && IsSystemAccount(user.SamAccountName)) continue;

                    var userDto = new DomainUserDto
                    {
                        Username = user.SamAccountName ?? string.Empty,
                        Email = user.EmailAddress,
                        DisplayName = user.DisplayName,
                        FirstName = user.GivenName,
                        LastName = user.Surname,
                        IsActive = user.Enabled ?? true,
                        AccountDisabled = !(user.Enabled ?? true),
                        AccountLocked = user.IsAccountLockedOut(),
                        PasswordNeverExpires = user.PasswordNeverExpires,
                        LastLogon = user.LastLogon,
                        PasswordLastSet = user.LastPasswordSet,
                        DistinguishedName = user.DistinguishedName,
                        ObjectSid = user.Sid?.ToString()
                    };

                    // Get additional attributes using DirectoryEntry
                    if (user.GetUnderlyingObject() is DirectoryEntry directoryEntry)
                    {
                        userDto.Department = GetPropertyValue(directoryEntry, "department");
                        userDto.JobTitle = GetPropertyValue(directoryEntry, "title");
                        userDto.Manager = GetPropertyValue(directoryEntry, "manager");
                        
                        // Get group memberships
                        userDto.GroupMemberships = GetUserGroups(user);
                    }

                    users.Add(userDto);
                    count++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process user {Username}", user.SamAccountName);
                }
            }

            _logger.LogInformation("Collected {Count} domain users", users.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect domain users");
            throw;
        }

        return users;
    }

    public async Task<List<object>> CollectGroupsAsync()
    {
        // TODO: Implement group collection
        await Task.CompletedTask;
        return new List<object>();
    }

    public async Task<List<object>> CollectPoliciesAsync()
    {
        // TODO: Implement policy collection
        await Task.CompletedTask;
        return new List<object>();
    }

    public async Task<bool> TestDomainConnectionAsync()
    {
        try
        {
            var domainName = _configuration["DomainController:DomainName"];
            using var context = new PrincipalContext(ContextType.Domain, domainName);
            
            // Try to get the current user to test connection
            var currentUser = UserPrincipal.Current;
            return currentUser != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Domain connection test failed");
            return false;
        }
    }

    private string? GetPropertyValue(DirectoryEntry entry, string propertyName)
    {
        try
        {
            if (entry.Properties.Contains(propertyName) && entry.Properties[propertyName].Count > 0)
            {
                return entry.Properties[propertyName][0]?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get property {PropertyName}", propertyName);
        }
        return null;
    }

    private List<string> GetUserGroups(UserPrincipal user)
    {
        var groups = new List<string>();
        try
        {
            var userGroups = user.GetGroups();
            foreach (var group in userGroups)
            {
                if (group.Name != null)
                {
                    groups.Add(group.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get groups for user {Username}", user.SamAccountName);
        }
        return groups;
    }

    private bool IsSystemAccount(string? username)
    {
        if (string.IsNullOrEmpty(username)) return true;
        
        var systemAccounts = new[]
        {
            "administrator", "guest", "krbtgt", "defaultaccount",
            "wdagutilityaccount", "healthmailbox"
        };

        return systemAccounts.Any(sa => username.Equals(sa, StringComparison.OrdinalIgnoreCase)) ||
               username.EndsWith("$", StringComparison.OrdinalIgnoreCase) ||
               username.StartsWith("SM_", StringComparison.OrdinalIgnoreCase) ||
               username.StartsWith("MSOL_", StringComparison.OrdinalIgnoreCase);
    }
}
