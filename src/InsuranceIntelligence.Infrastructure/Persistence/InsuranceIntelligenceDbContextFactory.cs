using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public class InsuranceIntelligenceDbContextFactory : IDesignTimeDbContextFactory<InsuranceIntelligenceDbContext>
{
    public InsuranceIntelligenceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>();
        
        // Use a dummy connection string for design-time tooling
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=IIP_Design;Trusted_Connection=True;MultipleActiveResultSets=true");

        return new InsuranceIntelligenceDbContext(optionsBuilder.Options);
    }
}
