namespace Odisea.Application.Common.Interfaces;

public interface IOperatorContext
{
    bool HasOperator { get; }

    // Null for callers without an operator tenant (agencies, PlatformAdmin).
    // Operator-scoped reads filter on it; operator-scoped writes require it.
    Guid? OperatorId { get; }
}
