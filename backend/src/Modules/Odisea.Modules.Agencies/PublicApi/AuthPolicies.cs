namespace Odisea.Modules.Agencies.PublicApi;

/// Authorization vocabulary other modules build their endpoint policies on.
public static class AuthPolicies
{
    public const string Operator = "Operator";

    /// Operator with role Admin: irreversible money decisions (confirm, cancel a paid booking, void a payment).
    public const string OperatorAdmin = "OperatorAdmin";

    public const string Agency = "Agency";
}

public static class AuthClaims
{
    public const string UserType = "user_type";
    public const string AgencyId = "agency_id";
    public const string Role = "role";

    /// "api_key" when the caller authenticated with an agency API key; absent for user logins.
    public const string AuthMethod = "auth_method";

    public const string UserTypeOperator = "operator";
    public const string UserTypeAgency = "agency";

    /// Must equal OperatorRole.Admin.ToString() — a unit test pins the two together.
    public const string RoleOperatorAdmin = "Admin";

    /// The role an API key authenticates as. Not an AgencyUserRole: keys are machines, not people.
    public const string RoleApiClient = "ApiClient";

    public const string AuthMethodApiKey = "api_key";
}
