using System.ComponentModel.DataAnnotations;
namespace SupportTracker.Models;

public class Ticket
{
    public int Id { get; set; }
    [MaxLength(120)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "open";
    [MaxLength(20)] public string Priority { get; set; } = "medium";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TicketInput : IValidatableObject
{
    [Required, StringLength(120)] public string Title { get; set; } = "";
    [MaxLength(4000)] public string Description { get; set; } = "";
    [Required] public string Status { get; set; } = "open";
    [Required] public string Priority { get; set; } = "medium";
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Title)) yield return new("Title is required.", [nameof(Title)]);
        if (!TicketRules.Statuses.Contains(Status)) yield return new("Status must be open, in_progress or closed.", [nameof(Status)]);
        if (!TicketRules.Priorities.Contains(Priority)) yield return new("Priority must be low, medium or high.", [nameof(Priority)]);
    }
}
public static class TicketRules
{
    public static readonly string[] Statuses = ["open", "in_progress", "closed"];
    public static readonly string[] Priorities = ["low", "medium", "high"];
}
