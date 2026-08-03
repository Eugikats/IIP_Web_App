using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public class InsuranceIntelligenceDbContextFactory : IDesignTimeDbContextFactory<InsuranceIntelligenceDbContext>
{
    public InsuranceIntelligenceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>();
        
        // Migration generation uses localdb by default. A deployment operator may
        // explicitly supply the canonical runtime variable for an approved Azure
        // SQL migration; the connection string is never written to source/output.
        var connectionString = Environment.GetEnvironmentVariable("INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING");
        optionsBuilder.UseSqlServer(string.IsNullOrWhiteSpace(connectionString)
            ? "Server=(localdb)\\mssqllocaldb;Database=IIP_Design;Trusted_Connection=True;MultipleActiveResultSets=true"
            : connectionString);

        return new InsuranceIntelligenceDbContext(optionsBuilder.Options);
    }
}
