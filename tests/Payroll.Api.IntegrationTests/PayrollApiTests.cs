using System.Net;
using System.Net.Http.Json;

namespace Payroll.Api.IntegrationTests;

public class PayrollApiTests(PayrollApiFactory factory) : IClassFixture<PayrollApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(PayrollApiFactory.Json))!;

    private async Task<ContractorResponse> CreateContractor(object? body = null)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/contractors", body ?? TestData.NewContractor());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read<ContractorResponse>(response);
    }

    private async Task<TimesheetResponse> CreateWeeklyTimesheet(Guid contractorId, DateOnly monday)
    {
        var entries = Enumerable.Range(0, 5)
            .Select(d => (object)new { workDate = monday.AddDays(d), hours = 7.5m, type = "Standard" })
            .Append(new { workDate = monday.AddDays(5), hours = 4m, type = "Overtime" });

        var response = await _client.PostAsJsonAsync("/api/v1/timesheets", new
        {
            contractorId,
            periodStart = monday,
            periodEnd = monday.AddDays(6),
            entries,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read<TimesheetResponse>(response);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy_including_database()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = await Read<HealthResponse>(response);
        Assert.Equal("Healthy", health.Status);
        Assert.Contains(health.Checks, c => c.Name == "database" && c.Status == "Healthy");
    }

    [Fact]
    public async Task Liveness_and_readiness_probes_respond()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_describes_versioned_routes()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("/api/v1/contractors", json, StringComparison.Ordinal);
        Assert.Contains("Umbrella Payroll API", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seeded_fictional_contractors_are_listed()
    {
        var page = await _client.GetFromJsonAsync<PagedResponse<ContractorResponse>>("/api/v1/contractors?search=UMB-", PayrollApiFactory.Json);

        Assert.NotNull(page);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal("UMB-0001", page.Items[0].EmployeeNumber);
    }

    [Fact]
    public async Task Create_then_get_contractor_round_trips()
    {
        var created = await CreateContractor();

        var fetched = await _client.GetFromJsonAsync<ContractorResponse>($"/api/v1/contractors/{created.Id}", PayrollApiFactory.Json);

        Assert.Equal(created, fetched);
        Assert.Equal("Weekly", fetched!.PayFrequency);
    }

    [Fact]
    public async Task Create_contractor_with_invalid_data_returns_validation_problem()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/contractors", new
        {
            employeeNumber = "",
            firstName = "Bad",
            lastName = "Data",
            email = "not-an-email",
            nationalInsuranceNumber = "12345",
            taxCode = "S1257L",
            hourlyRate = 0,
            payFrequency = "Weekly",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await Read<ProblemResponse>(response);
        Assert.NotNull(problem.Errors);
        Assert.Contains("EmployeeNumber", problem.Errors.Keys);
        Assert.Contains("Email", problem.Errors.Keys);
        Assert.Contains("NationalInsuranceNumber", problem.Errors.Keys);
        Assert.Contains("TaxCode", problem.Errors.Keys);
        Assert.Contains("HourlyRate", problem.Errors.Keys);
    }

    [Fact]
    public async Task Duplicate_employee_number_returns_409()
    {
        var body = TestData.NewContractor();
        await CreateContractor(body);

        var response = await _client.PostAsJsonAsync("/api/v1/contractors", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("conflict", (await Read<ProblemResponse>(response)).Code);
    }

    [Fact]
    public async Task Unknown_contractor_returns_404_problem_details()
    {
        var response = await _client.GetAsync($"/api/v1/contractors/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await Read<ProblemResponse>(response);
        Assert.Equal(404, problem.Status);
        Assert.Equal("not_found", problem.Code);
    }

    [Fact]
    public async Task Timesheet_to_payslip_end_to_end()
    {
        var contractor = await CreateContractor(TestData.NewContractor(taxCode: "1257L", hourlyRate: 32.50m));
        var sheet = await CreateWeeklyTimesheet(contractor.Id, new DateOnly(2026, 11, 2));
        Assert.Equal("Draft", sheet.Status);
        Assert.Equal(41.5m, sheet.TotalHours);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/v1/timesheets/{sheet.Id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/v1/timesheets/{sheet.Id}/approve", null)).StatusCode);

        var payslipResponse = await _client.PostAsync($"/api/v1/timesheets/{sheet.Id}/payslip", null);
        Assert.Equal(HttpStatusCode.Created, payslipResponse.StatusCode);

        var payslip = await Read<PayslipResponse>(payslipResponse);
        Assert.Equal(1_413.75m, payslip.AssignmentIncome);
        Assert.Equal(1_226.21m, payslip.GrossPay);
        Assert.Equal(248.60m, payslip.IncomeTax);
        Assert.Equal(63.18m, payslip.EmployeeNationalInsurance);
        Assert.Equal(914.43m, payslip.NetPay);
        Assert.Contains("Not tax advice", payslip.Disclaimer, StringComparison.Ordinal);

        // Timesheet is now Processed and cannot be paid twice.
        var processed = await _client.GetFromJsonAsync<TimesheetResponse>($"/api/v1/timesheets/{sheet.Id}", PayrollApiFactory.Json);
        Assert.Equal("Processed", processed!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/v1/timesheets/{sheet.Id}/payslip", null)).StatusCode);

        var payslips = await _client.GetFromJsonAsync<List<PayslipResponse>>($"/api/v1/contractors/{contractor.Id}/payslips", PayrollApiFactory.Json);
        Assert.Single(payslips!);

        var fetched = await _client.GetFromJsonAsync<PayslipResponse>($"/api/v1/payslips/{payslip.Id}", PayrollApiFactory.Json);
        Assert.Equal(payslip.NetPay, fetched!.NetPay);
    }

    [Fact]
    public async Task Approving_a_draft_timesheet_returns_422_with_domain_code()
    {
        var contractor = await CreateContractor();
        var sheet = await CreateWeeklyTimesheet(contractor.Id, new DateOnly(2026, 10, 5));

        var response = await _client.PostAsync($"/api/v1/timesheets/{sheet.Id}/approve", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("timesheet.not_submitted", (await Read<ProblemResponse>(response)).Code);
    }

    [Fact]
    public async Task Overlapping_timesheet_returns_409()
    {
        var contractor = await CreateContractor();
        await CreateWeeklyTimesheet(contractor.Id, new DateOnly(2026, 9, 7));

        var response = await _client.PostAsJsonAsync("/api/v1/timesheets", new
        {
            contractorId = contractor.Id,
            periodStart = new DateOnly(2026, 9, 10),
            periodEnd = new DateOnly(2026, 9, 16),
            entries = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Timesheet_entry_outside_period_returns_422()
    {
        var contractor = await CreateContractor();

        var response = await _client.PostAsJsonAsync("/api/v1/timesheets", new
        {
            contractorId = contractor.Id,
            periodStart = new DateOnly(2026, 8, 3),
            periodEnd = new DateOnly(2026, 8, 9),
            entries = new[] { new { workDate = new DateOnly(2026, 8, 20), hours = 8m, type = "Standard" } },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("timesheet.date_outside_period", (await Read<ProblemResponse>(response)).Code);
    }

    [Fact]
    public async Task Reject_requires_admin_flow_and_reason()
    {
        var contractor = await CreateContractor();
        var sheet = await CreateWeeklyTimesheet(contractor.Id, new DateOnly(2026, 7, 6));
        await _client.PostAsync($"/api/v1/timesheets/{sheet.Id}/submit", null);

        var response = await _client.PostAsJsonAsync($"/api/v1/timesheets/{sheet.Id}/reject", new { reason = "Overtime not pre-approved" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Rejected", (await Read<TimesheetResponse>(response)).Status);
    }

    [Fact]
    public async Task Calculator_returns_gross_to_net_breakdown()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/payroll/calculate", new
        {
            standardHours = 37.5m,
            overtimeHours = 4m,
            hourlyRate = 32.50m,
            taxCode = "1257L",
            payFrequency = "Weekly",
            umbrellaMargin = 18m,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var breakdown = await Read<BreakdownResponse>(response);
        Assert.Equal(914.43m, breakdown.NetPay);
        Assert.Contains("illustrative", breakdown.TaxYear, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Calculator_rejects_unsupported_tax_code()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/payroll/calculate", new
        {
            standardHours = 10m,
            overtimeHours = 0m,
            hourlyRate = 20m,
            taxCode = "XYZ",
            payFrequency = "Monthly",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Paging_parameters_are_validated()
    {
        var response = await _client.GetAsync("/api/v1/contractors?page=0&pageSize=500");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
