using Intranet.Api.MultifamilyLbp.Models;

namespace Intranet.Api.MultifamilyLbp.Services;

public interface IParserClient
{
    Task<IReadOnlyList<XrfReadingDto>> ParseFileAsync(byte[] fileContent, string fileName, CancellationToken cancellationToken = default);
}
