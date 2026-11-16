using Payroll.Domain.Common;

namespace Payroll.Domain.Pay;

public sealed record PayInput(
    decimal StandardHours,
    decimal OvertimeHours,
    decimal HourlyRate,
    decimal UmbrellaMargin,
    TaxCode TaxCode,
    PayFrequency Frequency);

public sealed record PayBreakdown(
    decimal StandardHours,
    decimal OvertimeHours,
    decimal HourlyRate,
    decimal OvertimeRate,
    decimal AssignmentIncome,
    decimal UmbrellaMargin,
    decimal EmployerNationalInsurance,
    decimal GrossPay,
    decimal TaxablePay,
    decimal IncomeTax,
    decimal EmployeeNationalInsurance,
    decimal NetPay,
    string TaxCode,
    PayFrequency Frequency,
    string TaxYear);

/// <summary>
/// Orchestrates the full timesheet → payslip pipeline:
/// <c>hours × rate → assignment income → − margin − employer NI → gross → − PAYE − employee NI → net</c>.
/// </summary>
public static class PayCalculator
{
    public const decimal OvertimeMultiplier = 1.5m;

    public static PayBreakdown Calculate(PayInput input, TaxYearRules rules)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);

        if (input.StandardHours < 0m || input.OvertimeHours < 0m)
        {
            throw new DomainException("pay.negative_hours", "Hours cannot be negative.");
        }

        if (input.HourlyRate <= 0m)
        {
            throw new DomainException("pay.invalid_rate", "Hourly rate must be greater than zero.");
        }

        var overtimeRate = Money.RoundToPence(input.HourlyRate * OvertimeMultiplier);
        var assignmentIncome = Money.RoundToPence(input.StandardHours * input.HourlyRate + input.OvertimeHours * overtimeRate);

        var funds = UmbrellaFundsCalculator.Calculate(assignmentIncome, input.UmbrellaMargin, input.Frequency, rules);
        var tax = IncomeTaxCalculator.Calculate(funds.GrossPay, input.TaxCode, input.Frequency, rules);
        var employeeNi = NationalInsuranceCalculator.Employee(funds.GrossPay, input.Frequency, rules);

        return new PayBreakdown(
            StandardHours: input.StandardHours,
            OvertimeHours: input.OvertimeHours,
            HourlyRate: input.HourlyRate,
            OvertimeRate: overtimeRate,
            AssignmentIncome: assignmentIncome,
            UmbrellaMargin: Math.Min(input.UmbrellaMargin, assignmentIncome),
            EmployerNationalInsurance: funds.EmployerNationalInsurance,
            GrossPay: funds.GrossPay,
            TaxablePay: tax.TaxablePay,
            IncomeTax: tax.Tax,
            EmployeeNationalInsurance: employeeNi,
            NetPay: funds.GrossPay - tax.Tax - employeeNi,
            TaxCode: input.TaxCode.Code,
            Frequency: input.Frequency,
            TaxYear: rules.TaxYear);
    }
}
