using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Intranet.Api.MultifamilyLbp.Data;

#nullable disable

namespace Intranet.Api.MultifamilyLbp.Data.Migrations;

[DbContext(typeof(MultifamilyDbContext))]
partial class MultifamilyDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.11");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MultifamilyDbContext).Assembly);
    }
}
