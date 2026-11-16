using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Payroll.Api.Infrastructure;

/// <summary>Configuration section <c>Auth</c>.</summary>
public sealed class ApiAuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>When <c>false</c> (local dev default) every policy allows anonymous access.</summary>
    public bool Enabled { get; set; }

    /// <summary>OIDC authority, e.g. <c>https://login.microsoftonline.com/{tenant}/v2.0</c> for Microsoft Entra ID.</summary>
    public string? Authority { get; set; }

    /// <summary>Expected <c>aud</c> claim (the API's App ID URI or client id).</summary>
    public string? Audience { get; set; }

    /// <summary>Expected <c>iss</c> claim when using a symmetric key.</summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Symmetric signing key for <b>local testing only</b> (integration tests mint tokens with it).
    /// In Azure, leave empty and use <see cref="Authority"/> so keys come from the identity provider.
    /// </summary>
    public string? SigningKey { get; set; }
}

public static class Policies
{
    /// <summary>Any authenticated caller.</summary>
    public const string PayrollRead = "payroll.read";

    /// <summary>Callers holding the <c>Payroll.Admin</c> app role (approve timesheets, run payroll).</summary>
    public const string PayrollAdmin = "payroll.admin";

    public const string AdminRole = "Payroll.Admin";
}

internal static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddPayrollAuth(this IServiceCollection services)
    {
        services.AddOptions<ApiAuthOptions>().BindConfiguration(ApiAuthOptions.SectionName);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured lazily from options so tests (and Key Vault-backed app settings) can override values.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<ApiAuthOptions>>((jwt, authOptions) =>
            {
                var auth = authOptions.Value;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = !string.IsNullOrWhiteSpace(auth.Audience),
                    ValidAudience = auth.Audience,
                    ValidateIssuer = !string.IsNullOrWhiteSpace(auth.Issuer) || !string.IsNullOrWhiteSpace(auth.Authority),
                    ValidIssuer = auth.Issuer,
                    RoleClaimType = "roles",
                    NameClaimType = "name",
                    ClockSkew = TimeSpan.FromMinutes(1),
                };

                if (!string.IsNullOrWhiteSpace(auth.Authority))
                {
                    jwt.Authority = auth.Authority;
                }
                else if (!string.IsNullOrWhiteSpace(auth.SigningKey))
                {
                    jwt.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey));
                }
            });

        services.AddAuthorization();
        services.AddOptions<AuthorizationOptions>()
            .Configure<IOptions<ApiAuthOptions>>((options, authOptions) =>
            {
                var enabled = authOptions.Value.Enabled;

                options.AddPolicy(Policies.PayrollRead, policy =>
                {
                    if (enabled) { policy.RequireAuthenticatedUser(); }
                    else { policy.RequireAssertion(_ => true); }
                });

                options.AddPolicy(Policies.PayrollAdmin, policy =>
                {
                    if (enabled) { policy.RequireAuthenticatedUser().RequireRole(Policies.AdminRole); }
                    else { policy.RequireAssertion(_ => true); }
                });
            });

        return services;
    }
}
