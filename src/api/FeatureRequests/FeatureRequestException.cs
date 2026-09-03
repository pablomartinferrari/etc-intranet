namespace Intranet.Api.FeatureRequests;

public sealed class FeatureRequestException : Exception
{
    public FeatureRequestException(string error, string message, int statusCode)
        : base(message)
    {
        Error = error;
        StatusCode = statusCode;
    }

    public string Error { get; }

    public int StatusCode { get; }

    public static FeatureRequestException BadRequest(string error, string message) =>
        new(error, message, StatusCodes.Status400BadRequest);

    public static FeatureRequestException Forbidden(string error, string message) =>
        new(error, message, StatusCodes.Status403Forbidden);
}
