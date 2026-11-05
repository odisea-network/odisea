using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Domain;

/// One day of the program as the customer will live it. Days are numbered from 1 without gaps.
public class ProgramItineraryDay : Entity
{
    public const int MaxDays = 60;

    public required Guid ProgramId { get; set; }
    public required int Day { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public MealPlan Meals { get; set; } = MealPlan.None;

    /// Where the night is spent, e.g. "Antalya" or "On board".
    public string? Overnight { get; set; }
}

[Flags]
public enum MealPlan
{
    None = 0,
    Breakfast = 1,
    Lunch = 2,
    Dinner = 4,
}
