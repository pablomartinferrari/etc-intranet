namespace Intranet.Api.Data.Entities;

public class SiteMessage
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public required string Body { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
