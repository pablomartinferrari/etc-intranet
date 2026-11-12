namespace Intranet.Api.Agentic.Data;

public sealed class AgenticProject
{
    public Guid Id { get; set; }
    public string OwnerOid { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Instructions { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<AgenticChat> Chats { get; set; } = new List<AgenticChat>();
    public ICollection<AgenticProjectFile> Files { get; set; } = new List<AgenticProjectFile>();
    public ICollection<AgenticProjectShare> Shares { get; set; } = new List<AgenticProjectShare>();
}

public sealed class AgenticProjectFile
{
    public Guid ProjectId { get; set; }
    public Guid DocumentId { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public AgenticProject Project { get; set; } = null!;
    public AgenticRepoDocument Document { get; set; } = null!;
}

public sealed class AgenticProjectShare
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string PrincipalType { get; set; } = "user";
    public string PrincipalOid { get; set; } = "";
    public string PrincipalDisplayName { get; set; } = "";
    public string? PrincipalEmail { get; set; }
    public string Role { get; set; } = "viewer";
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedByOid { get; set; } = "";
    public AgenticProject Project { get; set; } = null!;
}

public sealed class AgenticChat
{
    public Guid Id { get; set; }
    public string OwnerOid { get; set; } = "";
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = "New chat";
    public bool RealtimeCollaboration { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public AgenticProject? Project { get; set; }
    public ICollection<AgenticChatMessage> Messages { get; set; } = new List<AgenticChatMessage>();
    public ICollection<AgenticChatShare> Shares { get; set; } = new List<AgenticChatShare>();
}

public sealed class AgenticChatMessage
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public string Role { get; set; } = "user";
    public string AuthorOid { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string Content { get; set; } = "";
    public string? CitationsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public AgenticChat Chat { get; set; } = null!;
}

public sealed class AgenticChatShare
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public string PrincipalType { get; set; } = "user";
    public string PrincipalOid { get; set; } = "";
    public string PrincipalDisplayName { get; set; } = "";
    public string? PrincipalEmail { get; set; }
    public string Role { get; set; } = "viewer";
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedByOid { get; set; } = "";
    public AgenticChat Chat { get; set; } = null!;
}

public sealed class AgenticRepoFolder
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "/";
    public bool IsPublic { get; set; } = true;
    public bool IsIndexed { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public AgenticRepoFolder? Parent { get; set; }
    public ICollection<AgenticRepoFolder> Children { get; set; } = new List<AgenticRepoFolder>();
    public ICollection<AgenticRepoDocument> Documents { get; set; } = new List<AgenticRepoDocument>();
}

public sealed class AgenticRepoDocument
{
    public Guid Id { get; set; }
    public Guid FolderId { get; set; }
    public string Name { get; set; } = "";
    public string FileType { get; set; } = "document";
    public string MimeType { get; set; } = "text/plain";
    public long SizeBytes { get; set; }
    public string OwnerName { get; set; } = "ETC";
    public string StoragePath { get; set; } = "";
    public string? ExtractedText { get; set; }
    public bool IsIndexed { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public AgenticRepoFolder Folder { get; set; } = null!;
    public ICollection<AgenticRepoChunk> Chunks { get; set; } = new List<AgenticRepoChunk>();
}

public sealed class AgenticRepoChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = "";
    public AgenticRepoDocument Document { get; set; } = null!;
}
