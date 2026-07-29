using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentinela.Identity.Models;
using Sentinela.Identity.Services;
using Sentinela.Identity.Stores;

namespace Sentinela.Identity.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILdapService? _ldapService;
    private readonly IdentityDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILdapService? ldapService,
        IdentityDbContext context,
        ILogger<AdminController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _ldapService = ldapService;
        _context = context;
        _logger = logger;
    }

    [HttpGet("users")]
    public async Task<ActionResult<PaginatedResult<UserInfo>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null)
    {
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(u =>
                u.UserName!.Contains(search) ||
                u.Email!.Contains(search) ||
                (u.FullName != null && u.FullName.Contains(search)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync();

        if (!string.IsNullOrEmpty(role))
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role);
            var userIds = usersInRole.Select(u => u.Id).ToHashSet();
            query = query.Where(u => userIds.Contains(u.Id));
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userInfos = new List<UserInfo>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userInfos.Add(new UserInfo(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                roles.ToArray(),
                user.TwoFactorEnabled
            ));
        }

        return Ok(new PaginatedResult<UserInfo>(userInfos, totalCount, page, pageSize));
    }

    [HttpPost("users/{id}/lock")]
    public async Task<IActionResult> LockUser(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound(new AuthErrorResponse("USER_NOT_FOUND", "User not found.", null));
        }

        user.IsActive = false;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} locked by admin {AdminId}", id, GetAdminId());
        return NoContent();
    }

    [HttpPost("users/{id}/unlock")]
    public async Task<IActionResult> UnlockUser(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound(new AuthErrorResponse("USER_NOT_FOUND", "User not found.", null));
        }

        user.IsActive = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} unlocked by admin {AdminId}", id, GetAdminId());
        return NoContent();
    }

    [HttpPost("users/{id}/roles")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] string[] roles)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound(new AuthErrorResponse("USER_NOT_FOUND", "User not found.", null));
        }

        var existingRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, existingRoles);

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new ApplicationRole { Name = role });
            }
        }

        var result = await _userManager.AddToRolesAsync(user, roles);
        if (!result.Succeeded)
        {
            return BadRequest(new AuthErrorResponse("ROLE_ASSIGNMENT_FAILED",
                string.Join("; ", result.Errors.Select(e => e.Description)), null));
        }

        _logger.LogInformation("Roles assigned to user {UserId} by admin {AdminId}: {Roles}", id, GetAdminId(), string.Join(", ", roles));
        return NoContent();
    }

    [HttpPost("ldap/sync")]
    public async Task<IActionResult> SyncLdap()
    {
        if (_ldapService is null)
        {
            return BadRequest(new AuthErrorResponse("LDAP_NOT_CONFIGURED", "LDAP is not configured.", null));
        }

        await _ldapService.SyncUsersAsync();
        _logger.LogInformation("LDAP sync triggered by admin {AdminId}", GetAdminId());
        return Ok(new { message = "LDAP sync completed." });
    }

    [HttpGet("audit-logs")]
    public async Task<ActionResult<PaginatedResult<AuditLogEntry>>> GetAuditLogs([FromQuery] AuditLogFilter filter)
    {
        var query = _context.Set<AuditLogEntry>().AsQueryable();

        if (filter.UserId.HasValue)
        {
            query = query.Where(l => l.UserId == filter.UserId.Value);
        }

        if (!string.IsNullOrEmpty(filter.Action))
        {
            query = query.Where(l => l.Action.Contains(filter.Action));
        }

        if (filter.From.HasValue)
        {
            query = query.Where(l => l.Timestamp >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(l => l.Timestamp <= filter.To.Value);
        }

        var totalCount = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return Ok(new PaginatedResult<AuditLogEntry>(logs, totalCount, filter.Page, filter.PageSize));
    }

    private Guid GetAdminId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid admin token.");
        }
        return userId;
    }
}

public record PaginatedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

public record AuditLogEntry(Guid Id, Guid? UserId, string Action, string? Details, DateTimeOffset Timestamp, string? IpAddress);

public record AuditLogFilter(int Page = 1, int PageSize = 20, Guid? UserId = null, string? Action = null, DateTimeOffset? From = null, DateTimeOffset? To = null);
