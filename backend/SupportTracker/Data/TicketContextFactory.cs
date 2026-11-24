using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace SupportTracker.Data;
// Keeps EF tooling independent of application startup and a running database.
public sealed class TicketContextFactory : IDesignTimeDbContextFactory<TicketContext>
{
    public TicketContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TicketContext>()
            .UseSqlServer("Server=localhost;Database=SupportTracker;User Id=sa;Password=NotUsed_123!;TrustServerCertificate=True")
            .Options;
        return new TicketContext(options);
    }
}
