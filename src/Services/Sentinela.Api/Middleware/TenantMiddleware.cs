using System.Security.Claims;
using Sentinela.Persistence;
using Sentinela.Shared.Core.Interfaces;

namespace Sentinela.Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantAccessor = context.RequestServices.GetRequiredService<ITenantAccessor>() as TenantAccessor;

        if (tenantAccessor is not null)
        {
            // Try to get tenant from JWT claim first
            var tenantIdClaim = context.User?.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tenantId))
            {
                tenantAccessor.SetTenantId(tenantId);
            }
            else
            {
                // Fallback to X-Tenant-Id header
                var headerTenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
                if (!string.IsNullOrEmpty(headerTenantId) && Guid.TryParse(headerTenantId, out var headerTenant))
                {
                    tenantAccessor.SetTenantId(headerTenant);
                }
                else
                {
                    tenantAccessor.SetTenantId(Guid.Empty);
                }
            }
        }

        await _next(context);
    }
}
