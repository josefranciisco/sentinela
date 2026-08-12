using Microsoft.EntityFrameworkCore;
using Sentinela.Persistence;
using Sentinela.Shared.Domain.Identity;

namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[RequirePermission("roles.view")]
public class PermissionsController : ControllerBase
{
    private readonly SentinelaDbContext _context;

    public PermissionsController(SentinelaDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<PermissionDto>>> GetPermissions()
    {
        var permissions = await _context.Permissions
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Code)
            .ToListAsync();

        return Ok(permissions.Select(p => new PermissionDto
        {
            Code = p.Code,
            Name = p.Name,
            Description = p.Description,
            Category = p.Category
        }));
    }

    [HttpGet("grouped")]
    public async Task<ActionResult<Dictionary<string, List<PermissionDto>>>> GetPermissionsGrouped()
    {
        var permissions = await _context.Permissions
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Code)
            .ToListAsync();

        var grouped = permissions
            .GroupBy(p => p.Category)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => new PermissionDto
                {
                    Code = p.Code,
                    Name = p.Name,
                    Description = p.Description,
                    Category = p.Category
                }).ToList()
            );

        return Ok(grouped);
    }

    [HttpGet("definitions")]
    public ActionResult GetPermissionDefinitions()
    {
        return Ok(Permissions.PermissionDefinitions.Select(p => new
        {
            Code = p.Key,
            Name = p.Value.Name,
            Category = p.Value.Category
        }));
    }
}

public class PermissionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
