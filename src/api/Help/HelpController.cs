using Intranet.Api.KnowledgeBase.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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

    /// <summary>
    /// Authenticated diagnostic: whether hosted Fallback / Embeddings bound a key.
    /// Never returns secret values.
    /// </summary>
    [HttpGet("status")]
    public ActionResult<HelpStatusResponse> Status([FromServices] IOptions<KnowledgeBaseOptions> options)
    {
        var kb = options.Value;
        return Ok(new HelpStatusResponse(kb.Fallback.IsConfigured, kb.IsEmbeddingsConfigured));
    }
}
