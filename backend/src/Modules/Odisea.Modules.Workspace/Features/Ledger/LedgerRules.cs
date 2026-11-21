using System.Text.Json;
using System.Text.Json.Serialization;
using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Features.Ledger;

public sealed class LedgerOptions
{
    public const string Section = "Workspace:Ledger";

    /// "Today" for due dates and overdue checks is the calendar date in this zone, not in UTC.
    public string TimeZone { get; set; } = "Europe/Sofia";

    internal DateOnly Today(IClock clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, ResolveZone()).DateTime);

    internal TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}

/// The due dates a list filter or a dashboard figure looks at, both ends included.
public readonly record struct DueWindow(DateOnly From, DateOnly To)
{
    public const int MaxDays = 366;

    public bool Contains(DateOnly date) => date >= From && date <= To;

    public static DueWindow Overdue(DateOnly today) => new(DateOnly.MinValue, today.AddDays(-1));

    public static DueWindow Upcoming(DateOnly today, int days) => new(today, today.AddDays(Math.Clamp(days, 0, MaxDays)));

    /// Overdue only, the coming days only, or both when both are asked for.
    public static DueWindow? For(DateOnly today, int? dueWithinDays, bool overdueOnly) =>
        (dueWithinDays, overdueOnly) switch
        {
            (null, false) => null,
            (null, true) => Overdue(today),
            ({ } days, false) => Upcoming(today, days),
            ({ } days, true) => new(DateOnly.MinValue, Upcoming(today, days).To),
        };
}

/// Input checks shared by the ledger services. Every rejection carries a stable code.
internal static class LedgerRules
{
    public const decimal MaxAmount = 99_999_999.99m;
    public const string DefaultCurrency = "EUR";

    public static string Required(string? value, int maxLength, string field)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0)
            throw new LedgerValidationException($"{field}_required", $"'{field}' is required.");
        if (text.Length > maxLength)
            throw new LedgerValidationException($"{field}_too_long", $"'{field}' may be at most {maxLength} characters.");
        return text;
    }

    public static string? Optional(string? value, int maxLength, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        if (text.Length > maxLength)
            throw new LedgerValidationException($"{field}_too_long", $"'{field}' may be at most {maxLength} characters.");
        return text;
    }

    public static decimal Amount(decimal value, string field, bool allowZero)
    {
        if (value < 0 || (value == 0 && !allowZero) || value > MaxAmount || value != LedgerCalculator.Round(value))
            throw new LedgerValidationException($"{field}_invalid",
                $"'{field}' must be {(allowZero ? "zero or more" : "more than zero")}, at most {MaxAmount} and have no more than two decimals.");
        return value;
    }

    public static string Currency(string? value)
    {
        var code = string.IsNullOrWhiteSpace(value) ? DefaultCurrency : value.Trim().ToUpperInvariant();
        if (code.Length != 3 || !code.All(char.IsAsciiLetterUpper))
            throw new LedgerValidationException("currency_invalid", "The currency must be a three-letter code such as EUR.");
        return code;
    }

    public static string Reason(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0)
            throw new LedgerValidationException("reason_required", "A reason is required for this change.");
        if (text.Length > 500)
            throw new LedgerValidationException("reason_too_long", "The reason may be at most 500 characters.");
        return text;
    }
}

/// Collects field-level differences for one audit event.
internal sealed class LedgerChanges
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, object> _fields = [];

    public bool Any => _fields.Count > 0;

    public LedgerChanges Value<T>(string field, T from, T to)
    {
        if (!EqualityComparer<T>.Default.Equals(from, to))
            _fields[field] = new { from, to };
        return this;
    }

    /// For the first value of something new (a payment, a created reservation).
    public LedgerChanges Set<T>(string field, T to)
    {
        _fields[field] = new { to };
        return this;
    }

    /// Names and notes can hold personal data, so the audit says only that they changed.
    public LedgerChanges Text(string field, string? from, string? to)
    {
        if (!string.Equals(from, to, StringComparison.Ordinal))
            _fields[field] = new { changed = true };
        return this;
    }

    public string ToJson() => JsonSerializer.Serialize(_fields, Json);
}
