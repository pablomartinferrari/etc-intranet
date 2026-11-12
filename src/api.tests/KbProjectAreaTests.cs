using Intranet.Api.KnowledgeBase.Data;
using Intranet.Api.KnowledgeBase.Data.Entities;
using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Intranet.Api.Tests;

public class KbProjectAreaTests
{
    [Fact]
    public void NormalizeAreaTrimsEmptyAndCapsLength()
    {
        Assert.Null(KbProjectFields.NormalizeArea(null));
        Assert.Null(KbProjectFields.NormalizeArea("   "));
        Assert.Equal("Finance", KbProjectFields.NormalizeArea("  Finance  "));
        Assert.Equal(80, KbProjectFields.NormalizeArea(new string('a', 120))!.Length);
    }

    [Fact]
    public async Task CreateAndUpdateRoundTripAreaOnDto()
    {
        await using var db = CreateDb();
        var create = new CreateProjectRequestDto("Bid desk", Area: "  Sales  ");
        var now = DateTimeOffset.UtcNow;
        var project = new KbProject
        {
            Id = Guid.NewGuid(),
            UserOid = "owner",
            Name = create.Name,
            Description = create.Description,
            Instructions = create.Instructions,
            Area = KbProjectFields.NormalizeArea(create.Area),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var createdDto = KbProjectAccessService.ToOwnerDto(project);
        Assert.Equal("Sales", createdDto.Area);
        Assert.Equal("owner", createdDto.Role);

        var update = new UpdateProjectRequestDto(Area: " Operations ");
        project.Area = KbProjectFields.NormalizeArea(update.Area);
        await db.SaveChangesAsync();

        var stored = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == project.Id);
        var updatedDto = KbProjectAccessService.ToOwnerDto(stored);
        Assert.Equal("Operations", stored.Area);
        Assert.Equal("Operations", updatedDto.Area);

        project.Area = KbProjectFields.NormalizeArea("   ");
        await db.SaveChangesAsync();
        stored = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == project.Id);
        Assert.Null(KbProjectAccessService.ToOwnerDto(stored).Area);
    }

    private static KnowledgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new KnowledgeDbContext(options);
    }
}
