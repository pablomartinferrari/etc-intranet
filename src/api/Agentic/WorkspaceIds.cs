namespace Intranet.Api.Agentic;

/// <summary>Stable ids for the seeded POC catalog (URLs and tests).</summary>
public static class WorkspaceIds
{
    public const string DemoYouOid = "dev-you";
    public const string DemoUserBOid = "dev-user-b";
    public const string DemoUserCOid = "dev-user-c";
    public const string DemoUserDOid = "dev-user-d";
    public const string DemoGroupXOid = "dev-group-x";

    public static readonly Guid ProjectAlpha = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ProjectBeta = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid ChatPlaceholder = Guid.Parse("33333333-3333-3333-3333-333333333331");
    public static readonly Guid ChatAnother = Guid.Parse("33333333-3333-3333-3333-333333333332");
    public static readonly Guid ChatUnfiled = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ChatTeam = Guid.Parse("33333333-3333-3333-3333-333333333334");
    public static readonly Guid ChatSharedByMe = Guid.Parse("33333333-3333-3333-3333-333333333335");

    public static readonly Guid FolderRoot = Guid.Parse("44444444-4444-4444-4444-444444444440");
    public static readonly Guid FolderA = Guid.Parse("44444444-4444-4444-4444-444444444441");
    public static readonly Guid FolderA1 = Guid.Parse("44444444-4444-4444-4444-444444444442");
    public static readonly Guid FolderA2 = Guid.Parse("44444444-4444-4444-4444-444444444443");
    public static readonly Guid FolderB = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid FolderC = Guid.Parse("44444444-4444-4444-4444-444444444445");

    public static readonly Guid DocQuarterly1 = Guid.Parse("55555555-5555-5555-5555-555555555551");
    public static readonly Guid DocQuarterly2 = Guid.Parse("55555555-5555-5555-5555-555555555552");
    public static readonly Guid DocQuarterly3 = Guid.Parse("55555555-5555-5555-5555-555555555553");
    public static readonly Guid DocQuarterly4 = Guid.Parse("55555555-5555-5555-5555-555555555554");
    public static readonly Guid DocQuarterly5 = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid DocSpreadsheet = Guid.Parse("55555555-5555-5555-5555-555555555556");
    public static readonly Guid DocImage = Guid.Parse("55555555-5555-5555-5555-555555555557");
}
