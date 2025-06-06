using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Permafrost2.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PermafrostDbContext>
{
    public PermafrostDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PermafrostDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=permafrostdb;Trusted_Connection=true;MultipleActiveResultSets=true");

        return new PermafrostDbContext(optionsBuilder.Options);
    }
}
