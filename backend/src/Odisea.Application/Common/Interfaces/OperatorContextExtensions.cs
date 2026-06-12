namespace Odisea.Application.Common.Interfaces;

public static class OperatorContextExtensions
{
    // Unwraps the operator id for endpoints that cannot operate without one.
    // Throws when the caller has no operator tenant; callers map this to a 400
    // rather than letting it surface as a 500.
    public static Guid RequireOperator(this IOperatorContext ctx) =>
        ctx.OperatorId ?? throw new InvalidOperationException(
            "Endpoint requires an operator-scoped caller; current user has no operator tenantId.");
}
