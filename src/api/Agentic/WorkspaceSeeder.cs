using Intranet.Api.Agentic.Data;
using Intranet.Api.Agentic.Repository;
using Intranet.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intranet.Api.Agentic;

public sealed class WorkspaceSeeder
{
    private readonly IntranetDbContext _db;
    private readonly IRepositorySearch _search;
    private readonly AgenticOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<WorkspaceSeeder> _logger;

    public WorkspaceSeeder(
        IntranetDbContext db,
        IRepositorySearch search,
        IOptions<AgenticOptions> options,
        IWebHostEnvironment environment,
        ILogger<WorkspaceSeeder> logger)
    {
        _db = db;
        _search = search;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.SeedDemoData)
        {
            return;
        }

        if (await _db.AgenticProjects.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var owner = _options.DemoUserOid;

        _db.AgenticProjects.AddRange(
            new AgenticProject
            {
                Id = WorkspaceIds.ProjectAlpha,
                OwnerOid = owner,
                Name = "Project Alpha",
                Instructions = "You are helping the Alpha bid team. Prefer repository files in Folder A. Cite the quarterly report when discussing numbers. Keep answers short and name the next follow-up.",
                CreatedAt = now.AddDays(-12),
                UpdatedAt = now.AddHours(-2),
            },
            new AgenticProject
            {
                Id = WorkspaceIds.ProjectBeta,
                OwnerOid = owner,
                Name = "Project Beta",
                Instructions = "Beta is the internal ops project. Use Folder B when asked about procedures.",
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now.AddDays(-1),
            });

        _db.AgenticChats.AddRange(
            new AgenticChat
            {
                Id = WorkspaceIds.ChatPlaceholder,
                OwnerOid = owner,
                ProjectId = WorkspaceIds.ProjectAlpha,
                Title = "Chat title placeholder",
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now.AddHours(-3),
            },
            new AgenticChat
            {
                Id = WorkspaceIds.ChatAnother,
                OwnerOid = owner,
                ProjectId = WorkspaceIds.ProjectAlpha,
                Title = "Another chat",
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddHours(-5),
            },
            new AgenticChat
            {
                Id = WorkspaceIds.ChatUnfiled,
                OwnerOid = owner,
                Title = "Unfiled chat",
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddHours(-8),
            },
            new AgenticChat
            {
                Id = WorkspaceIds.ChatTeam,
                OwnerOid = WorkspaceIds.DemoUserBOid,
                Title = "Team chat",
                RealtimeCollaboration = true,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddMinutes(-20),
            },
            new AgenticChat
            {
                Id = WorkspaceIds.ChatSharedByMe,
                OwnerOid = owner,
                Title = "Chat title",
                RealtimeCollaboration = true,
                CreatedAt = now.AddDays(-6),
                UpdatedAt = now.AddHours(-10),
            });

        _db.AgenticChatShares.AddRange(
            new AgenticChatShare
            {
                Id = WorkspaceIds.ShareTeamYou,
                ChatId = WorkspaceIds.ChatTeam,
                PrincipalType = AgenticRoles.User,
                PrincipalOid = owner,
                PrincipalDisplayName = "You",
                PrincipalEmail = "you@etc.local",
                Role = AgenticRoles.Editor,
                CreatedAt = now.AddDays(-1),
                CreatedByOid = WorkspaceIds.DemoUserBOid,
            },
            new AgenticChatShare
            {
                Id = WorkspaceIds.ShareTeamC,
                ChatId = WorkspaceIds.ChatTeam,
                PrincipalType = AgenticRoles.User,
                PrincipalOid = WorkspaceIds.DemoUserCOid,
                PrincipalDisplayName = "User C",
                PrincipalEmail = "user.c@etc.local",
                Role = AgenticRoles.Editor,
                CreatedAt = now.AddDays(-1),
                CreatedByOid = WorkspaceIds.DemoUserBOid,
            },
            new AgenticChatShare
            {
                Id = WorkspaceIds.ShareTeamGroupX,
                ChatId = WorkspaceIds.ChatTeam,
                PrincipalType = AgenticRoles.Group,
                PrincipalOid = WorkspaceIds.DemoGroupXOid,
                PrincipalDisplayName = "Group X",
                Role = AgenticRoles.Editor,
                CreatedAt = now.AddDays(-1),
                CreatedByOid = WorkspaceIds.DemoUserBOid,
            },
            new AgenticChatShare
            {
                Id = WorkspaceIds.ShareByMeB,
                ChatId = WorkspaceIds.ChatSharedByMe,
                PrincipalType = AgenticRoles.User,
                PrincipalOid = WorkspaceIds.DemoUserBOid,
                PrincipalDisplayName = "User B",
                PrincipalEmail = "user.b@etc.local",
                Role = AgenticRoles.Editor,
                CreatedAt = now.AddDays(-5),
                CreatedByOid = owner,
            },
            new AgenticChatShare
            {
                Id = WorkspaceIds.ShareByMeC,
                ChatId = WorkspaceIds.ChatSharedByMe,
                PrincipalType = AgenticRoles.User,
                PrincipalOid = WorkspaceIds.DemoUserCOid,
                PrincipalDisplayName = "User C",
                PrincipalEmail = "user.c@etc.local",
                Role = AgenticRoles.Viewer,
                CreatedAt = now.AddDays(-5),
                CreatedByOid = owner,
            },
            new AgenticChatShare
            {
                Id = WorkspaceIds.ShareByMeGroupX,
                ChatId = WorkspaceIds.ChatSharedByMe,
                PrincipalType = AgenticRoles.Group,
                PrincipalOid = WorkspaceIds.DemoGroupXOid,
                PrincipalDisplayName = "Group X",
                Role = AgenticRoles.Editor,
                CreatedAt = now.AddDays(-5),
                CreatedByOid = owner,
            });

        _db.AgenticChatMessages.AddRange(
            Message(WorkspaceIds.ChatPlaceholder, "assistant", owner, "You", "I pulled the latest Alpha notes from Folder A. The quarterly report is the best source for the numbers you asked about.", now.AddHours(-4)),
            Message(WorkspaceIds.ChatPlaceholder, "user", owner, "You", "Summarize the quarterly report for the Alpha review.", now.AddHours(-3.5)),
            Message(WorkspaceIds.ChatPlaceholder, "assistant", "agent", "Assistant", "Here is a short brief of the quarterly report in Folder A. Revenue is called out in Document 1; the risk section lives in Document 2.", now.AddHours(-3), CitationsJson(WorkspaceIds.DocQuarterly1, "Document 1", "Root / Folder A / Subfolder A1")),
            Message(WorkspaceIds.ChatTeam, "user", WorkspaceIds.DemoUserBOid, "User B", "Can we walk through the quarterly report together before the Alpha review?", now.AddMinutes(-40)),
            Message(WorkspaceIds.ChatTeam, "assistant", "agent", "Assistant", "Yes. I will answer the group and cite the repository docs as we go.", now.AddMinutes(-39)),
            Message(WorkspaceIds.ChatTeam, "user", WorkspaceIds.DemoUserCOid, "User C", "Please keep Folder A in scope.", now.AddMinutes(-25)));

        SeedRepository(now);
        await _db.SaveChangesAsync(cancellationToken);

        _db.AgenticProjectFiles.AddRange(
            new AgenticProjectFile
            {
                ProjectId = WorkspaceIds.ProjectAlpha,
                DocumentId = WorkspaceIds.DocQuarterly1,
                AddedAt = now.AddDays(-10),
            },
            new AgenticProjectFile
            {
                ProjectId = WorkspaceIds.ProjectAlpha,
                DocumentId = WorkspaceIds.DocQuarterly2,
                AddedAt = now.AddDays(-10),
            },
            new AgenticProjectFile
            {
                ProjectId = WorkspaceIds.ProjectAlpha,
                DocumentId = WorkspaceIds.DocSpreadsheet,
                AddedAt = now.AddDays(-9),
            });
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var document in await _db.AgenticRepoDocuments.ToListAsync(cancellationToken))
        {
            await _search.ReindexDocumentAsync(document, cancellationToken);
        }

        _logger.LogInformation("Seeded agentic workspace demo catalog.");
    }

    private void SeedRepository(DateTimeOffset now)
    {
        _db.AgenticRepoFolders.AddRange(
            Folder(WorkspaceIds.FolderRoot, null, "Root", "Root", now),
            Folder(WorkspaceIds.FolderA, WorkspaceIds.FolderRoot, "Folder A", "Root / Folder A", now),
            Folder(WorkspaceIds.FolderA1, WorkspaceIds.FolderA, "Subfolder A1", "Root / Folder A / Subfolder A1", now),
            Folder(WorkspaceIds.FolderA2, WorkspaceIds.FolderA, "Subfolder A2", "Root / Folder A / Subfolder A2", now),
            Folder(WorkspaceIds.FolderB, WorkspaceIds.FolderRoot, "Folder B", "Root / Folder B", now),
            Folder(WorkspaceIds.FolderC, WorkspaceIds.FolderRoot, "Folder C", "Root / Folder C", now));

        AddDocument(WorkspaceIds.DocQuarterly1, WorkspaceIds.FolderA1, "Document 1", "document", now.AddDays(-6),
            "Quarterly report — Document 1. Alpha pipeline highlights and recommended next steps. The quarterly report notes three pursuits that need close-out this week.");
        AddDocument(WorkspaceIds.DocQuarterly2, WorkspaceIds.FolderA1, "Document 2", "document", now.AddDays(-5),
            "Quarterly report appendix. Risks and owners. Use this quarterly report section when the agent is asked about follow-ups.");
        AddDocument(WorkspaceIds.DocQuarterly3, WorkspaceIds.FolderA2, "Document 3", "document", now.AddDays(-4),
            "Folder A working notes that mention the quarterly report kickoff and the shared Team chat.");
        AddDocument(WorkspaceIds.DocQuarterly4, WorkspaceIds.FolderA1, "Document 4", "document", now.AddDays(-3),
            "Draft talking points taken from the quarterly report for the Project Alpha review.");
        AddDocument(WorkspaceIds.DocQuarterly5, WorkspaceIds.FolderA2, "Document 5", "document", now.AddDays(-2),
            "A shorter quarterly report recap intended for Group X.");
        AddDocument(WorkspaceIds.DocSpreadsheet, WorkspaceIds.FolderA1, "Spreadsheet 1", "spreadsheet", now.AddDays(-4),
            "Name,Owner,Status\nAlpha,You,Open\nBeta,User B,Watch");
        AddDocument(WorkspaceIds.DocImage, WorkspaceIds.FolderA1, "Image 1", "image", now.AddDays(-1),
            "Image caption: site photo referenced from the quarterly report walkthrough.");
    }

    private void AddDocument(Guid id, Guid folderId, string name, string fileType, DateTimeOffset modified, string text)
    {
        var relative = $"{id:N}{ExtensionFor(fileType)}";
        var root = RepositoryStorage.ResolveRoot(_options, _environment);
        Directory.CreateDirectory(root);
        var full = Path.Combine(root, relative);
        File.WriteAllText(full, text);
        _db.AgenticRepoDocuments.Add(new AgenticRepoDocument
        {
            Id = id,
            FolderId = folderId,
            Name = name,
            FileType = fileType,
            MimeType = fileType == "spreadsheet" ? "text/csv" : fileType == "image" ? "image/png" : "text/plain",
            SizeBytes = text.Length,
            OwnerName = "ETC",
            StoragePath = relative,
            ExtractedText = text,
            IsIndexed = false,
            ModifiedAt = modified,
        });
    }

    private static AgenticRepoFolder Folder(Guid id, Guid? parent, string name, string path, DateTimeOffset now) =>
        new()
        {
            Id = id,
            ParentId = parent,
            Name = name,
            Path = path,
            IsPublic = true,
            IsIndexed = true,
            CreatedAt = now,
        };

    private static AgenticChatMessage Message(
        Guid chatId,
        string role,
        string authorOid,
        string authorName,
        string content,
        DateTimeOffset created,
        string? citations = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            Role = role,
            AuthorOid = authorOid,
            AuthorName = authorName,
            Content = content,
            CitationsJson = citations,
            CreatedAt = created,
        };

    private static string CitationsJson(Guid documentId, string title, string path) =>
        $$"""[{"type":"repository","title":"{{title}}","snippet":"quarterly report","documentId":"{{documentId}}","path":"{{path}}"}]""";

    private static string ExtensionFor(string fileType) => fileType switch
    {
        "spreadsheet" => ".csv",
        "image" => ".txt",
        _ => ".txt",
    };
}
