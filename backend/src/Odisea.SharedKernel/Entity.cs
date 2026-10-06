namespace Odisea.SharedKernel;

public abstract class Entity
{
    // Version-7 GUIDs are time-ordered, keeping Postgres B-tree inserts append-only.
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
