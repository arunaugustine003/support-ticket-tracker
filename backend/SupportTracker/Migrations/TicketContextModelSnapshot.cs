using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SupportTracker.Data;
#nullable disable
namespace SupportTracker.Migrations;
[DbContext(typeof(TicketContext))]
public class TicketContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.11").HasAnnotation("Relational:MaxIdentifierLength", 128);
        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);
        modelBuilder.Entity("SupportTracker.Models.Ticket", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));
            b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
            b.Property<string>("Description").IsRequired().HasMaxLength(4000).HasColumnType("nvarchar(4000)");
            b.Property<string>("Priority").IsRequired().HasMaxLength(20).HasColumnType("nvarchar(20)");
            b.Property<string>("Status").IsRequired().HasMaxLength(20).HasColumnType("nvarchar(20)");
            b.Property<string>("Title").IsRequired().HasMaxLength(120).HasColumnType("nvarchar(120)");
            b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
            b.HasKey("Id"); b.HasIndex("Status", "CreatedAt"); b.ToTable("Tickets");
        });
    }
}
