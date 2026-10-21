using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rtok.Store.Model;

/// <summary>For <c>dotnet ef migrations add</c> and the build-time script generator only; the
/// store opens its context itself.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<RtokDbContext>
{
    public RtokDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<RtokDbContext>().UseSqlite("Data Source=:memory:").Options);
}
