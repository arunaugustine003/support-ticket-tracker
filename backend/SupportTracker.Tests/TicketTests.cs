using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportTracker.Controllers;
using SupportTracker.Data;
using SupportTracker.Models;
using Xunit;

public class TicketTests
{
    private static async Task<(SqliteConnection, TicketContext, TicketsController)> Setup()
    {
        var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var db = new TicketContext(new DbContextOptionsBuilder<TicketContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(); return (connection, db, new TicketsController(db));
    }
    [Fact] public async Task CreatePersistsTicket()
    {
        var (connection, db, controller) = await Setup(); await using var c = connection; await using var d = db;
        var result = await controller.Create(new TicketInput { Title = "  Login issue  " }, default);
        Assert.IsType<CreatedAtActionResult>(result); Assert.Equal("Login issue", (await db.Tickets.SingleAsync()).Title);
    }
    [Fact] public void InvalidTicketIsRejectedByValidation()
    {
        var input = new TicketInput { Title = " ", Status = "invalid" }; var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(input, new ValidationContext(input), errors, true));
    }
    [Fact] public async Task ListFiltersStatusAndTitle()
    {
        var (connection, db, controller) = await Setup(); await using var c = connection; await using var d = db;
        await controller.Create(new TicketInput { Title = "Login broken", Status = "open" }, default);
        await controller.Create(new TicketInput { Title = "Login slow", Status = "closed" }, default);
        await controller.Create(new TicketInput { Title = "Other", Status = "open" }, default);
        var result = Assert.IsType<OkObjectResult>(await controller.List("open", "Login", 1, 10));
        var total = (int)result.Value!.GetType().GetProperty("total")!.GetValue(result.Value)!; Assert.Equal(1, total);
    }
}
