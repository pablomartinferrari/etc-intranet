namespace Intranet.Api.Help;

public sealed record HelpAskRequest(string? Question);

/// <summary>
/// Safe ops diagnostic for hosted-model binding. Booleans only — never keys.
/// </summary>
public sealed record HelpStatusResponse(bool FallbackConfigured, bool EmbeddingsConfigured);

public sealed record HelpLinkDto(string Label, string Path);

public sealed record HelpAskResponse(
    string Answer,
    IReadOnlyList<HelpLinkDto> Links,
    string Source,
    string? Provider = null,
    string? Model = null,
    string? UnavailableReason = null);

/// <summary>
/// One successful chat-completion turn used by Help. <see cref="Content"/> is the
/// model text (expected JSON). Provider/model are surfaced in the UI.
/// </summary>
public sealed record HelpLlmTurn(
    string Content,
    string Provider,
    string Model,
    bool IsFallback);

public sealed record IntranetPlace(
    string Id,
    string Title,
    IReadOnlyList<string> Aliases,
    string Path,
    IReadOnlyList<string> Paths,
    string HowToGetThere,
    string Purpose,
    IReadOnlyList<string> RelatedPlaceIds,
    IReadOnlyList<string> CommonQuestions,
    IReadOnlyList<string> DataSources,
    string? AudienceNotes,
    string FallbackAnswer,
    IReadOnlyList<string>? Tips = null);

public sealed record HelpMapAnswer(
    string Answer,
    IReadOnlyList<HelpLinkDto> Links,
    IReadOnlyList<string> PlaceIds);

public sealed record HelpFrontendPlace(
    string Id,
    string Title,
    string Path,
    IReadOnlyList<string> Aliases,
    string Purpose,
    IReadOnlyList<string> CommonQuestions,
    string FallbackAnswer);

public sealed record HelpFrontendCatalog(
    IReadOnlyList<string> SuggestedQuestions,
    IReadOnlyList<HelpFrontendPlace> Places);

internal readonly record struct ParsedHelpLlm(
    string Answer,
    IReadOnlyList<string> PlaceIds);
