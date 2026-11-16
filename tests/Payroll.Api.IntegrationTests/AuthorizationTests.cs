using System.Net;
using System.Net.Http.Json;

namespace Payroll.Api.IntegrationTests;

/// <summary>Runs the API with JWT bearer auth enabled (as it is in Azure) and checks the policies.</summary>
public class AuthorizationTests(SecuredPayrollApiFactory factory) : IClassFixture<SecuredPayrollApiFactory>
{
    [Fact]
    public async Task Anonymous_request_to_protected_endpoint_returns_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/contractors");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_and_calculator_stay_anonymous()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);

        var calc = await client.PostAsJsonAsync("/api/v1/payroll/calculate", new
        {
            standardHours = 10m, overtimeHours = 0m, hourlyRate = 20m, taxCode = "BR", payFrequency = "Weekly",
        });
        Assert.Equal(HttpStatusCode.OK, calc.StatusCode);
    }

    [Fact]
    public async Task Authenticated_reader_can_read_but_not_administer()
    {
        var client = factory.CreateClientWithRoles(); // valid token, no roles

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/contractors")).StatusCode);

        var create = await client.PostAsJsonAsync("/api/v1/contractors", TestData.NewContractor());
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Payroll_admin_can_create_contractors()
    {
        var client = factory.CreateClientWithRoles("Payroll.Admin");

        var create = await client.PostAsJsonAsync("/api/v1/contractors", TestData.NewContractor());

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_wrong_key_is_rejected()
    {
        var client = factory.CreateClient();
        var token = PayrollApiFactory.CreateToken("Payroll.Admin");
        var tampered = token[..^4] + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/contractors")).StatusCode);
    }
}
