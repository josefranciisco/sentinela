using Sentinela.Persistence.Models;

namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[RequirePermission("machines.view")]
public class SoftwareController : ControllerBase
{
    private readonly IRepository<SoftwareInventoryItem> _softwareRepo;
    private readonly IRepository<Computer> _computerRepo;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<SoftwareController> _logger;

    public SoftwareController(
        IRepository<SoftwareInventoryItem> softwareRepo,
        IRepository<Computer> computerRepo,
        ICacheService cache,
        IMapper mapper,
        ILogger<SoftwareController> logger)
    {
        _softwareRepo = softwareRepo;
        _computerRepo = computerRepo;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<SoftwareInventoryDto>>> GetSoftwareInventory(
        [FromQuery] string? search = null,
        [FromQuery] bool? authorized = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _softwareRepo.Query().Where(s => !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) || s.Publisher.Contains(search));
        if (authorized.HasValue)
            query = query.Where(s => s.IsAuthorized == authorized.Value);

        var items = await query.ToListAsync();

        var grouped = items
            .GroupBy(s => new { s.Name, s.Version })
            .Select(g => new SoftwareInventoryDto
            {
                Name = g.Key.Name,
                Version = g.Key.Version,
                Publisher = g.First().Publisher,
                InstallCount = g.Count(),
                IsAuthorized = g.First().IsAuthorized,
                Category = g.First().Category,
                FirstSeen = g.Min(s => s.FirstDetected),
                LastSeen = g.Max(s => s.LastDetected)
            })
            .OrderByDescending(s => s.InstallCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(grouped);
    }

    [HttpGet("{id}/computers")]
    public async Task<ActionResult<List<ComputerDto>>> GetComputersWithSoftware(Guid id)
    {
        var software = await _softwareRepo.GetByIdAsync(id);
        if (software is null) return NotFound();

        var computerIds = await _softwareRepo.Query()
            .Where(s => s.Name == software.Name && s.Version == software.Version && !s.IsDeleted)
            .Select(s => s.ComputerId)
            .Distinct()
            .ToListAsync();

        var computers = await _computerRepo.Query()
            .Where(c => computerIds.Contains(c.Id) && !c.IsDeleted)
            .ToListAsync();

        return Ok(_mapper.Map<List<ComputerDto>>(computers));
    }

    [HttpGet("unauthorized")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<List<SoftwareInventoryDto>>> GetUnauthorizedSoftware(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var items = await _softwareRepo.Query()
            .Where(s => !s.IsAuthorized && !s.IsDeleted)
            .ToListAsync();

        var grouped = items
            .GroupBy(s => new { s.Name, s.Version })
            .Select(g => new SoftwareInventoryDto
            {
                Name = g.Key.Name,
                Version = g.Key.Version,
                Publisher = g.First().Publisher,
                InstallCount = g.Count(),
                IsAuthorized = false,
                Category = g.First().Category,
                FirstSeen = g.Min(s => s.FirstDetected),
                LastSeen = g.Max(s => s.LastDetected)
            })
            .OrderByDescending(s => s.InstallCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(grouped);
    }
}
