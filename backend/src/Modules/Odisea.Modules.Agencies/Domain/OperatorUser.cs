namespace Odisea.Modules.Agencies.Domain;

public class OperatorUser : UserAccount
{
    public OperatorRole Role { get; set; } = OperatorRole.Operator;
}

public enum OperatorRole
{
    Admin,
    Operator,
}
