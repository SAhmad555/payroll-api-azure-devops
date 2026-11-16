namespace Payroll.Domain.Pay;

/// <summary>An immutable record of one pay run for one approved timesheet.</summary>
public sealed class Payslip
{
    private Payslip() { } // EF Core

    public Payslip(Guid id, Guid contractorId, Guid timesheetId, DateOnly periodStart, DateOnly periodEnd, PayBreakdown breakdown, DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(breakdown);

        Id = id;
        ContractorId = contractorId;
        TimesheetId = timesheetId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        TaxYear = breakdown.TaxYear;
        TaxCode = breakdown.TaxCode;
        PayFrequency = breakdown.Frequency;
        StandardHours = breakdown.StandardHours;
        OvertimeHours = breakdown.OvertimeHours;
        HourlyRate = breakdown.HourlyRate;
        OvertimeRate = breakdown.OvertimeRate;
        AssignmentIncome = breakdown.AssignmentIncome;
        UmbrellaMargin = breakdown.UmbrellaMargin;
        EmployerNationalInsurance = breakdown.EmployerNationalInsurance;
        GrossPay = breakdown.GrossPay;
        TaxablePay = breakdown.TaxablePay;
        IncomeTax = breakdown.IncomeTax;
        EmployeeNationalInsurance = breakdown.EmployeeNationalInsurance;
        NetPay = breakdown.NetPay;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ContractorId { get; private set; }
    public Guid TimesheetId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public string TaxYear { get; private set; } = string.Empty;
    public string TaxCode { get; private set; } = string.Empty;
    public PayFrequency PayFrequency { get; private set; }
    public decimal StandardHours { get; private set; }
    public decimal OvertimeHours { get; private set; }
    public decimal HourlyRate { get; private set; }
    public decimal OvertimeRate { get; private set; }
    public decimal AssignmentIncome { get; private set; }
    public decimal UmbrellaMargin { get; private set; }
    public decimal EmployerNationalInsurance { get; private set; }
    public decimal GrossPay { get; private set; }
    public decimal TaxablePay { get; private set; }
    public decimal IncomeTax { get; private set; }
    public decimal EmployeeNationalInsurance { get; private set; }
    public decimal NetPay { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
