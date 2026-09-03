using Microsoft.AspNetCore.Mvc;

namespace Intranet.Api.Help;

[ApiController]
[Route("api/help")]
public sealed class HelpController(HelpAskService help) : ControllerBase
{
    [HttpPost("ask")]
    public async Task<ActionResult<HelpAskResponse>> Ask(
        [FromBody] HelpAskRequest? body,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await help.AskAsync(body?.Question, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "invalid_question", message = ex.Message });
        }
    }
}
