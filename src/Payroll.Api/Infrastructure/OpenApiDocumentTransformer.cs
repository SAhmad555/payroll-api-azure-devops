using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Payroll.Api.Infrastructure;

/// <summary>Adds API title/description and the JWT bearer security scheme to the OpenAPI document.</summary>
internal sealed class OpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Umbrella Payroll API",
            Version = "v1",
            Description =
                "Portfolio demo: contractor timesheets → gross pay → simplified PAYE income tax & National Insurance → payslip. " +
                "All rates are ILLUSTRATIVE sample values — not tax advice, not for real payroll. All data is fictional.",
            License = new OpenApiLicense { Name = "MIT" },
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Microsoft Entra ID access token. Not required when Auth:Enabled=false (local development).",
        };

        return Task.CompletedTask;
    }
}
