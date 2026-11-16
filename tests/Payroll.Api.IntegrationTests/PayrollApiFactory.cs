using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Payroll.Api.IntegrationTests;

/// <summary>
/// Boots the real API in-memory against a private, shared-cache in-memory SQLite database.
/// One keep-alive connection holds the database open for the lifetime of the factory.
/// </summary>
public class PayrollApiFactory : WebApplicationFactory<Program>
{
    public const string TestIssuer = "https://tests.payroll-demo.local";
    public const string TestAudience = "api://payroll-demo";

    // HS256 needs >= 256-bit key. Test-only value; never used outside the test host.
    public const string TestSigningKey = "integration-tests-only-signing-key-0123456789abcdef";

    private readonly string _connectionString = $"Data Source=payroll-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive;

    public PayrollApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected virtual bool AuthEnabled => false;

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Payroll"] = _connectionString,
            ["Database:Provider"] = "Sqlite",
            ["Database:InitializeOnStartup"] = "true",
            ["Database:SeedSampleData"] = "true",
            ["Auth:Enabled"] = AuthEnabled ? "true" : "false",
            ["Auth:Issuer"] = TestIssuer,
            ["Auth:Audience"] = TestAudience,
            ["Auth:SigningKey"] = TestSigningKey,
            ["Auth:Authority"] = "",
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "",
        }));
    }

    /// <summary>Mints a short-lived HS256 token the test host will accept.</summary>
    public static string CreateToken(params string[] roles)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "test-user",
            ["name"] = "Integration Test",
        };

        if (roles.Length > 0)
        {
            claims["roles"] = roles;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = TestIssuer,
            Audience = TestAudience,
            Claims = claims,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)), SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public HttpClient CreateClientWithRoles(params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(roles));
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}

/// <summary>Same API with JWT auth switched on.</summary>
public sealed class SecuredPayrollApiFactory : PayrollApiFactory
{
    protected override bool AuthEnabled => true;
}

internal static class TestData
{
    private static int _sequence = 100_000;

    /// <summary>Unique, fictional employee number + NI number (HMRC reserved "QQ" prefix).</summary>
    public static (string EmployeeNumber, string Nino) NextIdentity()
    {
        var n = Interlocked.Increment(ref _sequence);
        return ($"TST-{n}", $"QQ{n:D6}D");
    }

    public static object NewContractor(string taxCode = "1257L", decimal hourlyRate = 32.50m, string frequency = "Weekly")
    {
        var (employeeNumber, nino) = NextIdentity();
        return new
        {
            employeeNumber,
            firstName = "Test",
            lastName = "Contractor",
            email = $"{employeeNumber.ToLowerInvariant()}@example.com",
            nationalInsuranceNumber = nino,
            taxCode,
            hourlyRate,
            payFrequency = frequency,
        };
    }
}
