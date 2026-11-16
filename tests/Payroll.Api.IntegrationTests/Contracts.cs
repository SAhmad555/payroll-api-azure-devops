namespace Payroll.Api.IntegrationTests;

// Minimal client-side shapes — deliberately independent of the API's own DTOs so the tests
// verify the public JSON contract rather than sharing types with the server.

public sealed record ContractorResponse(Guid Id, string EmployeeNumber, string FirstName, string LastName, string TaxCode, decimal HourlyRate, string PayFrequency);

public sealed record PagedResponse<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record TimesheetResponse(Guid Id, Guid ContractorId, string Status, decimal StandardHours, decimal OvertimeHours, decimal TotalHours);

public sealed record PayslipResponse(
    Guid Id,
    Guid TimesheetId,
    decimal AssignmentIncome,
    decimal UmbrellaMargin,
    decimal EmployerNationalInsurance,
    decimal GrossPay,
    decimal IncomeTax,
    decimal EmployeeNationalInsurance,
    decimal NetPay,
    string Disclaimer);

public sealed record BreakdownResponse(decimal AssignmentIncome, decimal GrossPay, decimal IncomeTax, decimal EmployeeNationalInsurance, decimal NetPay, string TaxYear);

public sealed record ProblemResponse(int Status, string Title, string? Detail, string? Code, Dictionary<string, string[]>? Errors);

public sealed record HealthResponse(string Status, List<HealthCheckEntry> Checks);

public sealed record HealthCheckEntry(string Name, string Status);
