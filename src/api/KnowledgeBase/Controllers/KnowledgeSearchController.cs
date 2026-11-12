using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;

namespace Intranet.Api.KnowledgeBase.Controllers;

[ApiController]
[Route("api/kb")]
public sealed class KnowledgeSearchController : ControllerBase
{
    private readonly SemanticSearchService _search;
    private readonly RagService _rag;

    public KnowledgeSearchController(SemanticSearchService search, RagService rag)
    {
        _search = search;
        _rag = rag;
    }

    [HttpPost("search")]
    public async Task<ActionResult<SearchResponseDto>> Search(
        [FromBody] SearchRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query is required.");
        }

        var response = await _search.SearchAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("query")]
    public async Task<ActionResult<object>> Query(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query is required.");
        }

        if (SemanticSearchService.IsSearchIntent(request.Query))
        {
            var search = await _search.SearchAsync(
                new SearchRequestDto(request.Query, Limit: 20),
                cancellationToken);
            return Ok(new { mode = "search", results = search.Results });
        }

        var userOid = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        var chat = await _rag.ChatAsync(request, userOid, cancellationToken);
        return Ok(new { mode = "chat", sessionId = chat.SessionId, answer = chat.Answer, citations = chat.Citations });
    }
}
