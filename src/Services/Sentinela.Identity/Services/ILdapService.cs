using Sentinela.Identity.Models;

namespace Sentinela.Identity.Services;

public interface ILdapService
{
    Task<bool> AuthenticateAsync(string username, string password);
    Task<LdapUserInfo?> GetUserInfoAsync(string username);
    Task<List<string>> GetUserGroupsAsync(string username);
    Task<LoginResponse> LoginWithLdapAsync(string username, string password, string? deviceInfo, string? ipAddress);
    Task SyncUsersAsync();
}

public record LdapUserInfo(string DistinguishedName, string SamAccountName, string UserPrincipalName, string Email, string DisplayName, string Department, bool IsActive, string[] Groups);
