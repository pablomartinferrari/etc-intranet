namespace Intranet.Api.Help;

public sealed record HelpAskRequest(string? Question);

public sealed record HelpLinkDto(string Label, string Path);

public sealed record HelpAskResponse(
    string Answer,
    IReadOnlyList<HelpLinkDto> Links,
    string Source);

public sealed record IntranetPlace(
    string Id,
    string Title,
    string Path,
    string Purpose,
    string HowToGetThere,
    IReadOnlyList<string> Keywords);

public sealed record IntranetFaq(
    string Id,
    IReadOnlyList<string> Phrases,
    string Answer,
    IReadOnlyList<string> PlaceIds);

public sealed record HelpMapAnswer(
    string Answer,
    IReadOnlyList<HelpLinkDto> Links,
    IReadOnlyList<string> PlaceIds);
