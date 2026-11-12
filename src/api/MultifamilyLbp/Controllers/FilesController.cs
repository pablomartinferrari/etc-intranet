using Microsoft.AspNetCore.Mvc;

namespace Intranet.Api.MultifamilyLbp.Controllers;

/// <summary>
/// Placeholder for Excel upload → parse → SharePoint / Postgres (port from SPFx services).
/// </summary>
[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    [HttpPost("upload")]
    [ProducesResponseType(typeof(UploadStubResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<UploadStubResponse> UploadStub()
    {
        return StatusCode(501, new UploadStubResponse(
            "Not implemented. Add multipart upload, parse with ClosedXML/EPPlus, persist to Postgres or Graph."));
    }
}

public record UploadStubResponse(string Message);
