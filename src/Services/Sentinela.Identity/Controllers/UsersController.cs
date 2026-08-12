using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentinela.Identity.Models;
using Sentinela.Identity.Stores;
using Sentinela.Persistence;
using Sentinela.Shared.Domain.Identity;

namespace Sentinela.Identity.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SentinelaDbContext _context;
    private readonly IdentityDbContext _identityContext;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        SentinelaDbContext context,
        IdentityDbContext identityContext,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _context = context;
        _identityContext = identityContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null)
    {
        var tenantId = CurrentTenantId;
        var query = _userManager.Users.Where(u => u.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.UserName!.Contains(search) ||
                u.Email!.Contains(search) ||
                (u.FullName != null && u.FullName.Contains(search)));
        }

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await GetRoleCodeListAsync(user.Id, tenantId);
            result.Add(ToDto(user, roles));
        }

        return Ok(result);
    }

    [HttpGet("groups")]
    public async Task<ActionResult<object>> GetGrouped()
    {
        var tenantId = CurrentTenantId;
        var users = await _userManager.Users
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.UserName)
            .ToListAsync();

        return Ok(users.Select(u => new { id = u.Id, label = u.FullName ?? u.UserName, value = u.Id }));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new AuthErrorResponse("INVALID_INPUT", "Invalid input.", null));

        var tenantId = CurrentTenantId;

        if (await _userManager.FindByNameAsync(dto.Username) is not null)
            return Conflict(new AuthErrorResponse("USERNAME_TAKEN", "Username is already taken.", null));
        if (!string.IsNullOrWhiteSpace(dto.Email) && await _userManager.FindByEmailAsync(dto.Email) is not null)
            return Conflict(new AuthErrorResponse("EMAIL_TAKEN", "Email is already registered.", null));

        var user = new ApplicationUser
        {
            UserName = dto.Username,
            Email = dto.Email,
            FullName = dto.FullName,
            Department = dto.Department,
            IsActive = dto.IsActive ?? true,
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new AuthErrorResponse("USER_CREATE_FAILED",
                string.Join("; ", result.Errors.Select(e => e.Description)), null));

        if (dto.RoleIds?.Any() == true)
        {
            await AssignRolesAsync(user.Id, tenantId, dto.RoleIds);
        }
        else
        {
            var defaultRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.IsDefault && !r.IsDeleted);
            if (defaultRole is not null)
            {
                _context.UserRoles.Add(new UserRole(user.Id, defaultRole.Id) { TenantId = tenantId });
                await _context.SaveChangesAsync();
            }
        }

        _logger.LogInformation("User created: {Username} for tenant {TenantId}", user.UserName, tenantId);
        var roles = await GetRoleCodeListAsync(user.Id, tenantId);
        return CreatedAtAction(nameof(GetUsers), new { }, ToDto(user, roles));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserDto dto)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != CurrentTenantId)
            return NotFound(new AuthErrorResponse("USER_NOT_FOUND", "User not found.", null));

        if (dto.FullName is not null) user.FullName = dto.FullName;
        if (dto.Department is not null) user.Department = dto.Department;
        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new AuthErrorResponse("USER_UPDATE_FAILED",
                string.Join("; ", result.Errors.Select(e => e.Description)), null));

        if (dto.RoleIds is not null)
            await SetRolesAsync(user.Id, CurrentTenantId, dto.RoleIds);

        _logger.LogInformation("User {UserId} updated", id);
        var roles = await GetRoleCodeListAsync(user.Id, CurrentTenantId);
        return Ok(ToDto(user, roles));
    }

    [HttpPost("{id}/roles")]
    public async Task<IActionResult> SetUserRoles(Guid id, [FromBody] SetUserRolesDto dto)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != CurrentTenantId)
            return NotFound(new AuthErrorResponse("USER_NOT_FOUND", "User not found.", null));

        await SetRolesAsync(user.Id, CurrentTenantId, dto.RoleIds);
        _logger.LogInformation("Roles set for user {UserId}", id);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateUser(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != CurrentTenantId)
            return NotFound(new AuthErrorResponse("USER_NOT_FOUND", "User not found.", null));

        if (user.Id == ChannelAdminId)
            return BadRequest(new AuthErrorResponse("CANNOT_DEACTIVATE_SELF", "You cannot deactivate your own account.", null));

        user.IsActive = false;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} deactivated", id);
        return NoContent();
    }

    [HttpDelete("{id}/permanent")]
    public async Task<IActionResult> DeleteUserPermanently(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != CurrentTenantId)
            return NotFound(new AuthErrorResponse("USER_NOT_FOUND", "User not found.", null));

        if (user.Id == ChannelAdminId)
            return BadRequest(new AuthErrorResponse("CANNOT_DELETE_SELF", "You cannot delete your own account.", null));

        var adminRoles = await GetRoleCodeListAsync(user.Id, CurrentTenantId);
        if (adminRoles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase))
            return BadRequest(new AuthErrorResponse("CANNOT_DELETE_SUPERADMIN", "SuperAdmin accounts cannot be deleted.", null));

        var identityRoles = await _identityContext.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .ToListAsync();
        _identityContext.UserRoles.RemoveRange(identityRoles);

        var identityClaims = await _identityContext.UserClaims
            .Where(uc => uc.UserId == user.Id)
            .ToListAsync();
        _identityContext.UserClaims.RemoveRange(identityClaims);

        var identityLogins = await _identityContext.UserLogins
            .Where(ul => ul.UserId == user.Id)
            .ToListAsync();
        _identityContext.UserLogins.RemoveRange(identityLogins);

        var identityTokens = await _identityContext.UserTokens
            .Where(ut => ut.UserId == user.Id)
            .ToListAsync();
        _identityContext.UserTokens.RemoveRange(identityTokens);

        var refreshTokens = await _identityContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync();
        _identityContext.RefreshTokens.RemoveRange(refreshTokens);

        var appUserRoles = await _context.UserRoles
            .Where(ur => ur.UserId == user.Id && !ur.IsDeleted)
            .ToListAsync();
        _context.UserRoles.RemoveRange(appUserRoles);

        await _identityContext.SaveChangesAsync();
        await _context.SaveChangesAsync();

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(new AuthErrorResponse("USER_DELETE_FAILED",
                string.Join("; ", result.Errors.Select(e => e.Description)), null));

        _logger.LogInformation("User {UserId} permanently deleted", id);
        return NoContent();
    }

    [HttpPost("{id}/lock")]
    public async Task<IActionResult> LockUser(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != CurrentTenantId)
            return NotFound(new AuthErrorResponse("USER_NOT_FOUND", "User not found.", null));

        user.IsActive = false;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} locked", id);
        return NoContent();
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> UnlockUser(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != CurrentTenantId)
            return NotFound(new AuthErrorResponse("USER_NOT_FOUND", "User not found.", null));

        user.IsActive = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} unlocked", id);
        return NoContent();
    }

    private Guid CurrentTenantId
    {
        get
        {
            var claim = User.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var tenantId))
                return tenantId;
            return Guid.Parse("00000000-0000-0000-0000-000000000001");
        }
    }

    private Guid? ChannelAdminId
    {
        get
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            return !string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    private async Task<List<string>> GetRoleCodeListAsync(Guid userId, Guid tenantId)
    {
        var roleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId && ur.TenantId == tenantId && !ur.IsDeleted)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        return await _context.Roles
            .Where(r => roleIds.Contains(r.Id) && !r.IsDeleted)
            .Select(r => r.Name)
            .ToListAsync();
    }

    private async Task AssignRolesAsync(Guid userId, Guid tenantId, List<Guid> roleIds)
    {
        var valid = await _context.Roles
            .Where(r => roleIds.Contains(r.Id) && r.TenantId == tenantId && !r.IsDeleted)
            .Select(r => r.Id)
            .ToListAsync();

        foreach (var roleId in valid)
        {
            var existing = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && !ur.IsDeleted);
            if (existing is null)
                _context.UserRoles.Add(new UserRole(userId, roleId) { TenantId = tenantId });
        }

        await _context.SaveChangesAsync();
    }

    private async Task SetRolesAsync(Guid userId, Guid tenantId, List<Guid> roleIds)
    {
        var current = await _context.UserRoles
            .Where(ur => ur.UserId == userId && ur.TenantId == tenantId && !ur.IsDeleted)
            .ToListAsync();

        foreach (var ur in current)
            ur.MarkAsDeleted();

        var valid = await _context.Roles
            .Where(r => roleIds.Contains(r.Id) && r.TenantId == tenantId && !r.IsDeleted)
            .Select(r => r.Id)
            .ToListAsync();

        foreach (var roleId in valid)
            _context.UserRoles.Add(new UserRole(userId, roleId) { TenantId = tenantId });

        await _context.SaveChangesAsync();
    }

    private static UserDto ToDto(ApplicationUser user, List<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Department = user.Department,
            IsActive = user.IsActive,
            TenantId = user.TenantId,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            TwoFactorEnabled = user.TwoFactorEnabled,
            Roles = roles
        };
    }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; }
    public Guid? TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class CreateUserDto
{
    [Required]
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    [Required]
    public string Password { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Department { get; set; }
    public bool? IsActive { get; set; }
    public List<Guid>? RoleIds { get; set; }
}

public class UpdateUserDto
{
    public string? FullName { get; set; }
    public string? Department { get; set; }
    public bool? IsActive { get; set; }
    public List<Guid>? RoleIds { get; set; }
}

public class SetUserRolesDto
{
    [Required]
    public List<Guid> RoleIds { get; set; } = new();
}