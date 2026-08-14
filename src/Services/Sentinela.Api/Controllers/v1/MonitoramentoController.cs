using Sentinela.Api.Services;

namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/monitoramento")]
[Authorize]
[RequirePermission("machines.view")]
public class MonitoramentoController : ControllerBase
{
    private readonly MonitoramentoFleetClient _client;

    public MonitoramentoController(MonitoramentoFleetClient client)
    {
        _client = client;
    }

    [HttpGet("machines")]
    public async Task<IActionResult> GetMachines(CancellationToken ct)
    {
        try
        {
            return Ok(await _client.GetMachinesAsync(ct));
        }
        catch (Exception)
        {
            return StatusCode(503, new { message = "Monitoramento Mobi indisponível." });
        }
    }

    [HttpGet("machines/{hostname}")]
    public async Task<IActionResult> GetMachine(string hostname, CancellationToken ct)
    {
        try
        {
            var machine = await _client.GetMachineAsync(hostname, ct);
            if (machine is null) return NotFound();
            return Ok(machine);
        }
        catch (Exception)
        {
            return StatusCode(503, new { message = "Monitoramento Mobi indisponível." });
        }
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory(CancellationToken ct)
    {
        try
        {
            return Ok(await _client.GetInventoryAsync(ct));
        }
        catch (Exception)
        {
            return StatusCode(503, new { message = "Monitoramento Mobi indisponível." });
        }
    }
}
