using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Permafrost.EdgeService.Models;
using System.Diagnostics;

namespace Permafrost.EdgeService.Services;

public class ActiveDirectoryService : IActiveDirectoryService, IDisposable
{
    private readonly ILogger<ActiveDirectoryService> _logger;
    private readonly ActiveDirectoryConfiguration _config;
    private readonly ActivitySource _activitySource = ActivitySourceProvider.ActivitySource;
    private LdapConnection? _connection;
    private readonly object _connectionLock = new();

    public ActiveDirectoryService(ILogger<ActiveDirectoryService> logger, IOptions<ActiveDirectoryConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public async Task<PaginatedResponse<DomainUser>> GetUsersAsync(UserQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetUsersAsync");
        activity?.SetTag("page", parameters.Page);
        activity?.SetTag("pageSize", parameters.PageSize);

        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            var filter = BuildUserFilter(parameters);
            var attributes = new[] { 
                "objectGUID", "sAMAccountName", "userPrincipalName", "displayName", 
                "givenName", "sn", "mail", "department", "title", "manager", 
                "description", "userAccountControl", "lastLogon", "pwdLastSet", 
                "accountExpires", "whenCreated", "whenChanged", "distinguishedName", "memberOf" 
            };

            var searchRequest = new SearchRequest(_config.SearchBase, filter, SearchScope.Subtree, attributes);
            searchRequest.SizeLimit = _config.MaxResults;

            var response = (SearchResponse)await Task.Run(() => connection.SendRequest(searchRequest), cancellationToken);
            var users = new List<DomainUser>();

            foreach (SearchResultEntry entry in response.Entries)
            {
                var user = MapToUser(entry);
                if (user != null)
                {
                    users.Add(user);
                }
            }

            // Apply pagination
            var totalCount = users.Count;
            var pagedUsers = users
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToList();

            var totalPages = (int)Math.Ceiling((double)totalCount / parameters.PageSize);

            return new PaginatedResponse<DomainUser>
            {
                Data = pagedUsers,
                Success = true,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users from Active Directory");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            return new PaginatedResponse<DomainUser>
            {
                Data = Enumerable.Empty<DomainUser>(),
                Success = false,
                Message = ex.Message,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
                TotalCount = 0,
                TotalPages = 0
            };
        }
    }

    public async Task<PaginatedResponse<DomainGroup>> GetGroupsAsync(GroupQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetGroupsAsync");
        activity?.SetTag("page", parameters.Page);
        activity?.SetTag("pageSize", parameters.PageSize);

        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            var filter = BuildGroupFilter(parameters);
            var attributes = new[] { 
                "objectGUID", "sAMAccountName", "displayName", "description", 
                "distinguishedName", "groupType", "whenCreated", "whenChanged", "member" 
            };

            var searchRequest = new SearchRequest(_config.SearchBase, filter, SearchScope.Subtree, attributes);
            searchRequest.SizeLimit = _config.MaxResults;

            var response = (SearchResponse)await Task.Run(() => connection.SendRequest(searchRequest), cancellationToken);
            var groups = new List<DomainGroup>();

            foreach (SearchResultEntry entry in response.Entries)
            {
                var group = MapToGroup(entry);
                if (group != null)
                {
                    groups.Add(group);
                }
            }

            // Apply pagination
            var totalCount = groups.Count;
            var pagedGroups = groups
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToList();

            var totalPages = (int)Math.Ceiling((double)totalCount / parameters.PageSize);

            return new PaginatedResponse<DomainGroup>
            {
                Data = pagedGroups,
                Success = true,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get groups from Active Directory");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            return new PaginatedResponse<DomainGroup>
            {
                Data = Enumerable.Empty<DomainGroup>(),
                Success = false,
                Message = ex.Message,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
                TotalCount = 0,
                TotalPages = 0
            };
        }
    }

    public async Task<PaginatedResponse<DomainEvent>> GetEventsAsync(EventQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetEventsAsync");
        
        // For now, return empty events as Windows Event Log integration would require additional implementation
        // This would typically query Windows Event Log for security events
        await Task.Delay(1, cancellationToken);

        return new PaginatedResponse<DomainEvent>
        {
            Data = Enumerable.Empty<DomainEvent>(),
            Success = true,
            Message = "Event collection not yet implemented",
            Page = parameters.Page,
            PageSize = parameters.PageSize,
            TotalCount = 0,
            TotalPages = 0
        };
    }

    public async Task<DomainUser?> GetUserByIdAsync(string objectGuid, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetUserByIdAsync");
        activity?.SetTag("objectGuid", objectGuid);

        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            var filter = $"(objectGUID={FormatGuidForLdap(objectGuid)})";
            var attributes = new[] { 
                "objectGUID", "sAMAccountName", "userPrincipalName", "displayName", 
                "givenName", "sn", "mail", "department", "title", "manager", 
                "description", "userAccountControl", "lastLogon", "pwdLastSet", 
                "accountExpires", "whenCreated", "whenChanged", "distinguishedName", "memberOf" 
            };

            var searchRequest = new SearchRequest(_config.SearchBase, filter, SearchScope.Subtree, attributes);
            var response = (SearchResponse)await Task.Run(() => connection.SendRequest(searchRequest), cancellationToken);

            if (response.Entries.Count > 0)
            {
                return MapToUser(response.Entries[0]);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user {ObjectGuid} from Active Directory", objectGuid);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }

    public async Task<DomainGroup?> GetGroupByIdAsync(string objectGuid, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetGroupByIdAsync");
        activity?.SetTag("objectGuid", objectGuid);

        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            var filter = $"(objectGUID={FormatGuidForLdap(objectGuid)})";
            var attributes = new[] { 
                "objectGUID", "sAMAccountName", "displayName", "description", 
                "distinguishedName", "groupType", "whenCreated", "whenChanged", "member" 
            };

            var searchRequest = new SearchRequest(_config.SearchBase, filter, SearchScope.Subtree, attributes);
            var response = (SearchResponse)await Task.Run(() => connection.SendRequest(searchRequest), cancellationToken);

            if (response.Entries.Count > 0)
            {
                return MapToGroup(response.Entries[0]);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get group {ObjectGuid} from Active Directory", objectGuid);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return null;
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
                Message = isConnected ? "Active Directory connection is healthy" : "Failed to connect to Active Directory",
                Details = new Dictionary<string, object>
                {
                    ["DomainController"] = _config.DomainController,
                    ["Port"] = _config.Port,
                    ["SearchBase"] = _config.SearchBase,
                    ["UseSsl"] = _config.UseSsl
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
            var connection = await GetConnectionAsync(cancellationToken);
            
            // Simple search to test connection
            var searchRequest = new SearchRequest(_config.SearchBase, "(objectClass=*)", SearchScope.Base, new[] { "objectClass" });
            searchRequest.SizeLimit = 1;
            
            var response = await Task.Run(() => connection.SendRequest(searchRequest), cancellationToken);
            return response != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Active Directory connection test failed");
            return false;
        }
    }

    private async Task<LdapConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection != null)
        {
            return _connection;
        }

        lock (_connectionLock)
        {
            if (_connection != null)
            {
                return _connection;
            }

            var identifier = new LdapDirectoryIdentifier(_config.DomainController, _config.Port);
            _connection = new LdapConnection(identifier);

            if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.Password))
            {
                _connection.Credential = new NetworkCredential(_config.Username, _config.Password);
            }

            _connection.SessionOptions.ProtocolVersion = 3;
            
            if (_config.UseSsl)
            {
                _connection.SessionOptions.SecureSocketLayer = true;
            }

            _connection.Bind();
        }

        return _connection;
    }

    private string BuildUserFilter(UserQueryParameters parameters)
    {
        var filters = new List<string> { "(&(objectClass=user)(objectCategory=person)" };

        if (!string.IsNullOrEmpty(parameters.Filter))
        {
            filters.Add($"(|(sAMAccountName=*{parameters.Filter}*)(displayName=*{parameters.Filter}*)(mail=*{parameters.Filter}*))");
        }

        if (!string.IsNullOrEmpty(parameters.Department))
        {
            filters.Add($"(department=*{parameters.Department}*)");
        }

        if (parameters.Status.HasValue)
        {
            switch (parameters.Status.Value)
            {
                case UserStatus.Active:
                    filters.Add("(!(userAccountControl:1.2.840.113556.1.4.803:=2))");
                    break;
                case UserStatus.Disabled:
                    filters.Add("(userAccountControl:1.2.840.113556.1.4.803:=2)");
                    break;
            }
        }

        filters.Add(")");
        return string.Join("", filters);
    }

    private string BuildGroupFilter(GroupQueryParameters parameters)
    {
        var filters = new List<string> { "(&(objectClass=group)" };

        if (!string.IsNullOrEmpty(parameters.Filter))
        {
            filters.Add($"(|(sAMAccountName=*{parameters.Filter}*)(displayName=*{parameters.Filter}*))");
        }

        if (!string.IsNullOrEmpty(parameters.DistinguishedNameContains))
        {
            filters.Add($"(distinguishedName=*{parameters.DistinguishedNameContains}*)");
        }

        if (parameters.GroupType.HasValue)
        {
            switch (parameters.GroupType.Value)
            {
                case GroupType.Security:
                    filters.Add("(groupType:1.2.840.113556.1.4.803:=2147483648)");
                    break;
                case GroupType.Distribution:
                    filters.Add("(!(groupType:1.2.840.113556.1.4.803:=2147483648))");
                    break;
            }
        }

        filters.Add(")");
        return string.Join("", filters);
    }

    private DomainUser? MapToUser(SearchResultEntry entry)
    {
        try
        {
            var objectGuid = GetAttributeValue(entry, "objectGUID");
            var samAccountName = GetAttributeValue(entry, "sAMAccountName");

            if (string.IsNullOrEmpty(objectGuid) || string.IsNullOrEmpty(samAccountName))
            {
                return null;
            }

            var userAccountControl = GetAttributeValue(entry, "userAccountControl");
            var isEnabled = string.IsNullOrEmpty(userAccountControl) ||
                           (int.TryParse(userAccountControl, out var uac) && (uac & 2) == 0);

            return new DomainUser
            {
                ObjectGuid = FormatGuidFromBytes(entry.Attributes["objectGUID"]?.GetValues(typeof(byte[]))?.FirstOrDefault() as byte[]),
                SamAccountName = samAccountName,
                UserPrincipalName = GetAttributeValue(entry, "userPrincipalName"),
                DisplayName = GetAttributeValue(entry, "displayName"),
                GivenName = GetAttributeValue(entry, "givenName"),
                Surname = GetAttributeValue(entry, "sn"),
                Email = GetAttributeValue(entry, "mail"),
                Department = GetAttributeValue(entry, "department"),
                Title = GetAttributeValue(entry, "title"),
                ManagerDn = GetAttributeValue(entry, "manager"),
                Description = GetAttributeValue(entry, "description"),
                Enabled = isEnabled,
                LastLogon = ParseFileTime(GetAttributeValue(entry, "lastLogon")),
                PasswordLastSet = ParseFileTime(GetAttributeValue(entry, "pwdLastSet")),
                AccountExpires = ParseFileTime(GetAttributeValue(entry, "accountExpires")),
                WhenCreated = ParseGeneralizedTime(GetAttributeValue(entry, "whenCreated")),
                WhenChanged = ParseGeneralizedTime(GetAttributeValue(entry, "whenChanged")),
                DistinguishedName = GetAttributeValue(entry, "distinguishedName"),
                MemberOf = GetMultiValueAttribute(entry, "memberOf")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map user entry: {DN}", entry.DistinguishedName);
            return null;
        }
    }

    private DomainGroup? MapToGroup(SearchResultEntry entry)
    {
        try
        {
            var objectGuid = GetAttributeValue(entry, "objectGUID");
            var samAccountName = GetAttributeValue(entry, "sAMAccountName");
            var distinguishedName = GetAttributeValue(entry, "distinguishedName");

            if (string.IsNullOrEmpty(objectGuid) || string.IsNullOrEmpty(samAccountName) || string.IsNullOrEmpty(distinguishedName))
            {
                return null;
            }

            var groupTypeValue = GetAttributeValue(entry, "groupType");
            var groupType = GroupType.Security; // Default

            if (int.TryParse(groupTypeValue, out var gt))
            {
                groupType = (gt & -2147483648) != 0 ? GroupType.Security : GroupType.Distribution;
            }

            return new DomainGroup
            {
                ObjectGuid = FormatGuidFromBytes(entry.Attributes["objectGUID"]?.GetValues(typeof(byte[]))?.FirstOrDefault() as byte[]),
                SamAccountName = samAccountName,
                DisplayName = GetAttributeValue(entry, "displayName"),
                Description = GetAttributeValue(entry, "description"),
                DistinguishedName = distinguishedName,
                GroupType = groupType,
                WhenCreated = ParseGeneralizedTime(GetAttributeValue(entry, "whenCreated")),
                WhenChanged = ParseGeneralizedTime(GetAttributeValue(entry, "whenChanged")),
                Members = GetMultiValueAttribute(entry, "member")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map group entry: {DN}", entry.DistinguishedName);
            return null;
        }
    }

    private string? GetAttributeValue(SearchResultEntry entry, string attributeName)
    {
        return entry.Attributes[attributeName]?.GetValues(typeof(string))?.FirstOrDefault()?.ToString();
    }

    private List<string> GetMultiValueAttribute(SearchResultEntry entry, string attributeName)
    {
        var values = entry.Attributes[attributeName]?.GetValues(typeof(string));
        return values?.Cast<string>().ToList() ?? new List<string>();
    }

    private DateTime? ParseFileTime(string? fileTimeString)
    {
        if (string.IsNullOrEmpty(fileTimeString) || !long.TryParse(fileTimeString, out var fileTime) || fileTime == 0)
        {
            return null;
        }

        try
        {
            return DateTime.FromFileTime(fileTime);
        }
        catch
        {
            return null;
        }
    }

    private DateTime? ParseGeneralizedTime(string? generalizedTimeString)
    {
        if (string.IsNullOrEmpty(generalizedTimeString))
        {
            return null;
        }

        try
        {
            return DateTime.ParseExact(generalizedTimeString, "yyyyMMddHHmmss.fZ", null);
        }
        catch
        {
            return null;
        }
    }

    private string FormatGuidFromBytes(byte[]? guidBytes)
    {
        if (guidBytes == null || guidBytes.Length != 16)
        {
            return Guid.NewGuid().ToString();
        }

        return new Guid(guidBytes).ToString();
    }

    private string FormatGuidForLdap(string guid)
    {
        if (Guid.TryParse(guid, out var parsedGuid))
        {
            var bytes = parsedGuid.ToByteArray();
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.Append($"\\{b:X2}");
            }
            return sb.ToString();
        }
        return guid;
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _activitySource?.Dispose();
    }
}
