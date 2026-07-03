using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations;

[DbContext(typeof(Persistence.InsuranceIntelligenceDbContext))]
[Migration("202607020001_AddCorePortPermissionTables")]
partial class AddCorePortPermissionTables
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
    }
}
