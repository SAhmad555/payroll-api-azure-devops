using Payroll.Domain.Common;
using Payroll.Domain.Pay;
using ParsedTaxCode = Payroll.Domain.Pay.TaxCode;

namespace Payroll.Domain.Contractors;

/// <summary>
/// A contractor employed through the (fictional) umbrella company. All sample data in this
/// repository is invented; NI numbers use HMRC's reserved example prefix "QQ".
/// </summary>
public sealed class Contractor
{
    private Contractor() { } // EF Core

    public Contractor(
        Guid id,
        string employeeNumber,
        string firstName,
        string lastName,
        string email,
        string nationalInsuranceNumber,
        string taxCode,
        decimal hourlyRate,
        PayFrequency payFrequency,
        DateTimeOffset createdAtUtc)
    {
        if (hourlyRate <= 0)
        {
            throw new DomainException("contractor.invalid_rate", "Hourly rate must be greater than zero.");
        }

        // Fail fast on unsupported tax codes rather than at payroll time.
        _ = ParsedTaxCode.Parse(taxCode);

        Id = id;
        EmployeeNumber = employeeNumber;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        NationalInsuranceNumber = nationalInsuranceNumber.ToUpperInvariant();
        TaxCode = taxCode.ToUpperInvariant();
        HourlyRate = hourlyRate;
        PayFrequency = payFrequency;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string EmployeeNumber { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string NationalInsuranceNumber { get; private set; } = string.Empty;
    public string TaxCode { get; private set; } = string.Empty;
    public decimal HourlyRate { get; private set; }
    public PayFrequency PayFrequency { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public void ChangeTaxCode(string newTaxCode)
    {
        _ = ParsedTaxCode.Parse(newTaxCode);
        TaxCode = newTaxCode.ToUpperInvariant();
    }
}
