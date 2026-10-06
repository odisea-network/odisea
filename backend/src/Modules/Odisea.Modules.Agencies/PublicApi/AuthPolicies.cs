namespace Odisea.Modules.Agencies.PublicApi;

/// Authorization vocabulary other modules build their endpoint policies on.
public static class AuthPolicies
{
    public const string Operator = "Operator";
    public const string Agency = "Agency";
}

public static class AuthClaims
{
    public const string UserType = "user_type";
    public const string AgencyId = "agency_id";
    public const string Role = "role";

    public const string UserTypeOperator = "operator";
    public const string UserTypeAgency = "agency";
}
