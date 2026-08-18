using Sentinela.Api.Services;

namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hesk")]
[Authorize]
public class HeskController : ControllerBase
{
    private readonly HeskTicketFeedStore _store;

    public HeskController(HeskTicketFeedStore store)
    {
        _store = store;
    }

    [HttpGet("tickets")]
    public IActionResult GetTickets() => Ok(_store.Get());
}
