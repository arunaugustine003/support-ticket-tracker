using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportTracker.Data;
using SupportTracker.Models;
namespace SupportTracker.Controllers;

[ApiController]
[Route("api/tickets")]
public class TicketsController(TicketContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if ((status is not null && !TicketRules.Statuses.Contains(status)) || page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid query parameters", Detail = "Use a valid status, page >= 1, and pageSize between 1 and 100." });
        var query = db.Tickets.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(t => t.Title.Contains(search.Trim()));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { items, total, page, pageSize });
    }
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var statuses = await db.Tickets.GroupBy(t => t.Status).Select(g => new { key = g.Key, count = g.Count() }).ToListAsync(ct);
        var priorities = await db.Tickets.GroupBy(t => t.Priority).Select(g => new { key = g.Key, count = g.Count() }).ToListAsync(ct);
        return Ok(new { byStatus = TicketRules.Statuses.ToDictionary(s => s, s => statuses.FirstOrDefault(x => x.key == s)?.count ?? 0), byPriority = TicketRules.Priorities.ToDictionary(s => s, s => priorities.FirstOrDefault(x => x.key == s)?.count ?? 0) });
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        return ticket is null ? NotFound(new ProblemDetails { Status = 404, Title = "Ticket not found" }) : Ok(ticket);
    }
    [HttpPost]
    public async Task<IActionResult> Create(TicketInput input, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var ticket = new Ticket { Title = input.Title.Trim(), Description = input.Description ?? "", Status = input.Status, Priority = input.Priority, CreatedAt = now, UpdatedAt = now };
        db.Tickets.Add(ticket); await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, ticket);
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TicketInput input, CancellationToken ct)
    {
        var ticket = await db.Tickets.FindAsync([id], ct);
        if (ticket is null) return NotFound(new ProblemDetails { Status = 404, Title = "Ticket not found" });
        ticket.Title = input.Title.Trim(); ticket.Description = input.Description ?? ""; ticket.Status = input.Status; ticket.Priority = input.Priority; ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return Ok(ticket);
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ticket = await db.Tickets.FindAsync([id], ct);
        if (ticket is null) return NotFound(new ProblemDetails { Status = 404, Title = "Ticket not found" });
        db.Tickets.Remove(ticket); await db.SaveChangesAsync(ct); return NoContent();
    }
}
