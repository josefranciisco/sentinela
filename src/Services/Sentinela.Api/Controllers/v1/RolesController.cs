using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Sentinela.Persistence;
using Sentinela.Shared.Domain.Identity;

namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[RequirePermission("roles.view")]
public class RolesController : ControllerBase
{
    private readonly SentinelaDbContext _context;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<RolesController> _logger;

    public RolesController(
        SentinelaDbContext context,
        ITenantAccessor tenantAccessor,
        ILogger<RolesController> logger)
    {
        _context = context;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<RoleDto>>> GetRoles()
    {
        var tenantId = _tenantAccessor.TenantId;
        var roles = await _context.Roles
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync();

        return Ok(roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            IsSystemRole = r.IsSystemRole,
            IsDefault = r.IsDefault,
            Permissions = r.RolePermissions
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Code)
                .ToList(),
            UserCount = 0
        }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoleDto>> GetRole(Guid id)
    {
        var tenantId = _tenantAccessor.TenantId;
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted);

        if (role is null) return NotFound();

        return Ok(new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            IsDefault = role.IsDefault,
            Permissions = role.RolePermissions
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Code)
                .ToList(),
            UserCount = await _context.UserRoles
                .CountAsync(ur => ur.RoleId == id && !ur.IsDeleted)
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleDto dto)
    {
        var tenantId = _tenantAccessor.TenantId;

        if (await _context.Roles.AnyAsync(r => r.Name == dto.Name && r.TenantId == tenantId && !r.IsDeleted))
            return BadRequest("Role with this name already exists.");

        var role = new Role(dto.Name, dto.Description);
        role.TenantId = tenantId;

        if (dto.PermissionCodes?.Any() == true)
        {
            var permissions = await _context.Permissions
                .Where(p => dto.PermissionCodes.Contains(p.Code) && !p.IsDeleted)
                .ToListAsync();

            var rolePermissions = permissions.Select(p => new RolePermission(role.Id, p.Id)).ToList();
            role.SetPermissions(rolePermissions);
        }

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role created: {RoleName} for tenant {TenantId}", role.Name, tenantId);

        return CreatedAtAction(nameof(GetRole), new { id = role.Id }, new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            IsDefault = role.IsDefault,
            Permissions = dto.PermissionCodes ?? new List<string>()
        });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleDto dto)
    {
        var tenantId = _tenantAccessor.TenantId;
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted);

        if (role is null) return NotFound();
        if (role.IsSystemRole && role.Name != dto.Name)
            return BadRequest("Cannot rename a system role.");

        if (await _context.Roles.AnyAsync(r => r.Name == dto.Name && r.TenantId == tenantId && !r.IsDeleted && r.Id != id))
            return BadRequest("Role with this name already exists.");

        role.UpdateName(dto.Name);
        role.UpdateDescription(dto.Description);

        if (dto.PermissionCodes is not null)
        {
            var existingPermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == id && !rp.IsDeleted)
                .ToListAsync();

            _context.RolePermissions.RemoveRange(existingPermissions);

            var permissions = await _context.Permissions
                .Where(p => dto.PermissionCodes.Contains(p.Code) && !p.IsDeleted)
                .ToListAsync();

            var newRolePermissions = permissions.Select(p => new RolePermission(id, p.Id)).ToList();
            foreach (var rp in newRolePermissions)
            {
                rp.TenantId = tenantId;
                _context.RolePermissions.Add(rp);
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Role updated: {RoleName} for tenant {TenantId}", role.Name, tenantId);

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var tenantId = _tenantAccessor.TenantId;
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted);

        if (role is null) return NotFound();
        if (role.IsSystemRole)
            return BadRequest("Cannot delete a system role.");

        var userCount = await _context.UserRoles
            .CountAsync(ur => ur.RoleId == id && !ur.IsDeleted);

        if (userCount > 0)
            return BadRequest($"Cannot delete role with {userCount} user(s) assigned.");

        role.MarkAsDeleted();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role deleted: {RoleName} for tenant {TenantId}", role.Name, tenantId);

        return NoContent();
    }

    [HttpPost("{id}/duplicate")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<RoleDto>> DuplicateRole(Guid id, [FromBody] DuplicateRoleDto dto)
    {
        var tenantId = _tenantAccessor.TenantId;
        var sourceRole = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted);

        if (sourceRole is null) return NotFound();

        var newName = dto.Name ?? $"{sourceRole.Name} (Cópia)";

        if (await _context.Roles.AnyAsync(r => r.Name == newName && r.TenantId == tenantId && !r.IsDeleted))
            return BadRequest("Role with this name already exists.");

        var newRole = new Role(newName, sourceRole.Description);
        newRole.TenantId = tenantId;

        var permissionIds = sourceRole.RolePermissions.Select(rp => rp.PermissionId).ToList();
        var permissions = await _context.Permissions
            .Where(p => permissionIds.Contains(p.Id) && !p.IsDeleted)
            .ToListAsync();

        var rolePermissions = permissions.Select(p => new RolePermission(newRole.Id, p.Id)).ToList();
        newRole.SetPermissions(rolePermissions);

        _context.Roles.Add(newRole);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role duplicated: {SourceRoleName} -> {NewRoleName} for tenant {TenantId}", sourceRole.Name, newRole.Name, tenantId);

        return CreatedAtAction(nameof(GetRole), new { id = newRole.Id }, new RoleDto
        {
            Id = newRole.Id,
            Name = newRole.Name,
            Description = newRole.Description,
            IsSystemRole = newRole.IsSystemRole,
            IsDefault = newRole.IsDefault,
            Permissions = permissions.Select(p => p.Code).ToList()
        });
    }
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsDefault { get; set; }
    public List<string> Permissions { get; set; } = new();
    public int UserCount { get; set; }
}

public class CreateRoleDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? PermissionCodes { get; set; }
}

public class UpdateRoleDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? PermissionCodes { get; set; }
}

public class DuplicateRoleDto
{
    public string? Name { get; set; }
}
