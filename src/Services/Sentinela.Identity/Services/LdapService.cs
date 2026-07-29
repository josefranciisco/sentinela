using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Sentinela.Identity.Configuration;
using Sentinela.Identity.Models;

namespace Sentinela.Identity.Services;

public class LdapService : ILdapService
{
    private readonly LdapConfiguration _ldapConfig;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthService _authService;
    private readonly ILogger<LdapService> _logger;

    public LdapService(
        IOptions<LdapConfiguration> ldapConfig,
        UserManager<ApplicationUser> userManager,
        IAuthService authService,
        ILogger<LdapService> logger)
    {
        _ldapConfig = ldapConfig.Value;
        _userManager = userManager;
        _authService = authService;
        _logger = logger;
    }

    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        try
        {
            using var connection = CreateConnection();
            var userDn = await FindUserDn(connection, username);
            if (string.IsNullOrEmpty(userDn))
            {
                return false;
            }

            using var userConnection = new LdapConnection(new LdapDirectoryIdentifier(_ldapConfig.Server, _ldapConfig.Port));
            userConnection.Credential = new NetworkCredential(userDn, password);
            userConnection.AuthType = AuthType.Basic;
            userConnection.SessionOptions.ProtocolVersion = 3;
            if (_ldapConfig.UseSsl)
            {
                userConnection.SessionOptions.SecureSocketLayer = true;
            }
            userConnection.Bind();
            return true;
        }
        catch (LdapException ex)
        {
            _logger.LogWarning(ex, "LDAP authentication failed for user {Username}", username);
            return false;
        }
    }

    public async Task<LdapUserInfo?> GetUserInfoAsync(string username)
    {
        try
        {
            using var connection = CreateConnection();
            var searchRequest = new SearchRequest(
                _ldapConfig.UserSearchBase,
                $"(&(objectClass=user)(sAMAccountName={username}))",
                SearchScope.Subtree,
                new[] { "distinguishedName", "sAMAccountName", "userPrincipalName", "mail", "displayName", "department", "userAccountControl" }
            );

            var response = (SearchResponse)connection.SendRequest(searchRequest);
            if (response.Entries.Count == 0)
            {
                return null;
            }

            var entry = response.Entries[0];
            var groups = await GetUserGroupsAsync(username);

            return new LdapUserInfo(
                GetAttributeValue(entry, "distinguishedName"),
                GetAttributeValue(entry, "sAMAccountName"),
                GetAttributeValue(entry, "userPrincipalName"),
                GetAttributeValue(entry, "mail"),
                GetAttributeValue(entry, "displayName"),
                GetAttributeValue(entry, "department"),
                IsAccountActive(entry),
                groups.ToArray()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get LDAP info for user {Username}", username);
            return null;
        }
    }

    public async Task<List<string>> GetUserGroupsAsync(string username)
    {
        var groups = new List<string>();
        try
        {
            using var connection = CreateConnection();
            var searchRequest = new SearchRequest(
                _ldapConfig.GroupSearchBase,
                $"(&(objectClass=group)(member=sAMAccountName={username}))",
                SearchScope.Subtree,
                new[] { "cn" }
            );

            var response = (SearchResponse)connection.SendRequest(searchRequest);
            foreach (SearchResultEntry entry in response.Entries)
            {
                var cn = GetAttributeValue(entry, "cn");
                if (!string.IsNullOrEmpty(cn))
                {
                    groups.Add(cn);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get LDAP groups for user {Username}", username);
        }
        return groups;
    }

    public async Task<LoginResponse> LoginWithLdapAsync(string username, string password, string? deviceInfo, string? ipAddress)
    {
        var authenticated = await AuthenticateAsync(username, password);
        if (!authenticated)
        {
            throw new UnauthorizedAccessException("LDAP authentication failed.");
        }

        var user = await _userManager.FindByNameAsync(username);
        if (user is null)
        {
            var ldapInfo = await GetUserInfoAsync(username);
            user = new ApplicationUser
            {
                UserName = username,
                Email = ldapInfo?.Email ?? $"{username}@sentinela.local",
                FullName = ldapInfo?.DisplayName,
                Department = ldapInfo?.Department,
                IsActive = ldapInfo?.IsActive ?? true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Failed to create local user from LDAP.");
            }

            await _userManager.AddToRoleAsync(user, "User");
            _logger.LogInformation("Local user created from LDAP for {Username}", username);
        }

        return await _authService.LoginAsync(
            new LoginRequest(username, user.Email ?? string.Empty, password, null, null),
            deviceInfo,
            ipAddress
        );
    }

    public async Task SyncUsersAsync()
    {
        try
        {
            using var connection = CreateConnection();
            var searchRequest = new SearchRequest(
                _ldapConfig.UserSearchBase,
                "(objectClass=user)",
                SearchScope.Subtree,
                new[] { "sAMAccountName", "mail", "displayName", "department", "userAccountControl" }
            );

            var response = (SearchResponse)connection.SendRequest(searchRequest);
            var syncedCount = 0;

            foreach (SearchResultEntry entry in response.Entries)
            {
                var samAccountName = GetAttributeValue(entry, "sAMAccountName");
                if (string.IsNullOrEmpty(samAccountName))
                {
                    continue;
                }

                var existingUser = await _userManager.FindByNameAsync(samAccountName);
                if (existingUser is null)
                {
                    var email = GetAttributeValue(entry, "mail");
                    var newUser = new ApplicationUser
                    {
                        UserName = samAccountName,
                        Email = string.IsNullOrEmpty(email) ? $"{samAccountName}@sentinela.local" : email,
                        FullName = GetAttributeValue(entry, "displayName"),
                        Department = GetAttributeValue(entry, "department"),
                        IsActive = IsAccountActive(entry),
                        CreatedAt = DateTimeOffset.UtcNow
                    };

                    var result = await _userManager.CreateAsync(newUser);
                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(newUser, "User");
                        syncedCount++;
                    }
                }
            }

            _logger.LogInformation("LDAP sync completed. {Count} new users created.", syncedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LDAP sync failed");
            throw;
        }
    }

    private LdapConnection CreateConnection()
    {
        var connection = new LdapConnection(new LdapDirectoryIdentifier(_ldapConfig.Server, _ldapConfig.Port));
        connection.Credential = new NetworkCredential(_ldapConfig.AdminBindDn, _ldapConfig.AdminPassword);
        connection.AuthType = AuthType.Basic;
        connection.SessionOptions.ProtocolVersion = 3;
        if (_ldapConfig.UseSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
        }
        connection.Bind();
        return connection;
    }

    private async Task<string?> FindUserDn(LdapConnection connection, string username)
    {
        var searchRequest = new SearchRequest(
            _ldapConfig.UserSearchBase,
            $"(&(objectClass=user)(sAMAccountName={username}))",
            SearchScope.Subtree,
            new[] { "distinguishedName" }
        );

        var response = (SearchResponse)connection.SendRequest(searchRequest);
        if (response.Entries.Count == 0)
        {
            return null;
        }

        return GetAttributeValue(response.Entries[0], "distinguishedName");
    }

    private static string GetAttributeValue(SearchResultEntry entry, string attributeName)
    {
        if (entry.Attributes.Contains(attributeName))
        {
            var values = entry.Attributes[attributeName].GetValues(typeof(string));
            if (values.Length > 0)
            {
                return values[0] as string ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static bool IsAccountActive(SearchResultEntry entry)
    {
        var uac = GetAttributeValue(entry, "userAccountControl");
        if (string.IsNullOrEmpty(uac) || !int.TryParse(uac, out var uacValue))
        {
            return true;
        }
        return (uacValue & 0x0002) == 0;
    }
}
