namespace Odisea.SharedKernel;

public readonly record struct DateRange
{
    public DateOnly From { get; }
    public DateOnly To { get; }

    public DateRange(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ArgumentException($"Range end {to:yyyy-MM-dd} is before start {from:yyyy-MM-dd}.", nameof(to));

        From = from;
        To = to;
    }

    public int Nights => To.DayNumber - From.DayNumber;

    public bool Contains(DateOnly date) => date >= From && date <= To;

    public bool Overlaps(DateRange other) => From <= other.To && other.From <= To;

    public override string ToString() => $"{From:yyyy-MM-dd} – {To:yyyy-MM-dd}";
}
