namespace Intranet.Api.Help;

/// <summary>
/// Optional rewriter for help answers. Implementations must return null when
/// the model is down or times out so callers can fall back to the curated map.
/// </summary>
public interface IHelpLlm
{
    Task<HelpLlmTurn?> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken);
}
