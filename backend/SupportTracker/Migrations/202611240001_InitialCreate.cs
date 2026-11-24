using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupportTracker.Data;
#nullable disable
namespace SupportTracker.Migrations;
[DbContext(typeof(TicketContext))]
[Migration("202611240001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "Tickets", columns: table => new {
            Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
            Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
            Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
            Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_Tickets", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_Tickets_Status_CreatedAt", table: "Tickets", columns: new[] { "Status", "CreatedAt" });
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "Tickets");
}
