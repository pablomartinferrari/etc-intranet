namespace Intranet.Api.Cleat;

public sealed class CleatNotConfiguredException : Exception
{
    public const string UserMessage =
        "Add Cleat__ApiKey to configuration (dotnet user-secrets locally, or an App Setting / Key Vault secret in Azure).";

    public CleatNotConfiguredException()
        : base(UserMessage)
    {
    }
}

public sealed class CleatUpstreamException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    public CleatUpstreamException(string message, int statusCode, string errorCode = "cleat_upstream_error")
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}
