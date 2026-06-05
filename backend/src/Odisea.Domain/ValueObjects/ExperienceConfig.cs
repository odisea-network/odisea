namespace Odisea.Domain.ValueObjects;

public record ExperienceConfig(
    string Type = "grid",
    int Columns = 3,
    string CardStyle = "default",
    bool ShowPrice = true,
    bool Inquiry = true,
    bool OpenNewTab = false
);
