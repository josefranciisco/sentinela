using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sentinela.Shared.Core.Interfaces;

namespace Sentinela.Api.Services;

public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string UserId => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User?.FindFirst("sub")?.Value
        ?? string.Empty;

    public string Username => User?.FindFirst(ClaimTypes.Name)?.Value
        ?? User?.FindFirst("unique_name")?.Value
        ?? string.Empty;

    public string Email => User?.FindFirst(ClaimTypes.Email)?.Value
        ?? User?.FindFirst("email")?.Value
        ?? string.Empty;

    public string[] Roles => User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
        ?? Array.Empty<string>();

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public string IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
        ?? string.Empty;

    public Guid TenantId
    {
        get
        {
            var tenantIdClaim = User?.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tenantId))
            {
                return tenantId;
            }
            return Guid.Empty;
        }
    }
}
