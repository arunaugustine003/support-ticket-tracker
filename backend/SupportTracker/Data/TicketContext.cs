using Microsoft.EntityFrameworkCore;
using SupportTracker.Models;
namespace SupportTracker.Data;
public class TicketContext(DbContextOptions<TicketContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>().HasIndex(t => new { t.Status, t.CreatedAt });
        modelBuilder.Entity<Ticket>().Property(t => t.Description).HasMaxLength(4000);
    }
}
