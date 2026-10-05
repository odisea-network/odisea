namespace Odisea.SharedKernel;

public readonly record struct Pax
{
    public int Adults { get; }
    public int Children { get; }

    public Pax(int adults, int children = 0)
    {
        if (adults < 1)
            throw new ArgumentException("At least one adult is required.", nameof(adults));
        if (children < 0)
            throw new ArgumentException("Children cannot be negative.", nameof(children));

        Adults = adults;
        Children = children;
    }

    public int Total => Adults + Children;

    public override string ToString() => Children > 0 ? $"{Adults} AD + {Children} CH" : $"{Adults} AD";
}
