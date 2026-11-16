using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Pay;
using Payroll.Domain.Timesheets;

namespace Payroll.Application.Payslips;

public sealed record PayslipDto(
    Guid Id,
    Guid ContractorId,
    Guid TimesheetId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string TaxYear,
    string TaxCode,
    PayFrequency PayFrequency,
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
    string Disclaimer)
{
    public const string IllustrativeDisclaimer =
        "Illustrative calculation using simplified, hard-coded sample rates. Not tax advice and not for real payroll use.";

    public static PayslipDto From(Payslip p) => new(
        p.Id, p.ContractorId, p.TimesheetId, p.PeriodStart, p.PeriodEnd, p.TaxYear, p.TaxCode, p.PayFrequency,
        p.StandardHours, p.OvertimeHours, p.HourlyRate, p.OvertimeRate, p.AssignmentIncome, p.UmbrellaMargin,
        p.EmployerNationalInsurance, p.GrossPay, p.TaxablePay, p.IncomeTax, p.EmployeeNationalInsurance, p.NetPay,
        IllustrativeDisclaimer);
}

// ---------------------------------------------------------------- Run payroll for one approved timesheet

public sealed record RunPayrollCommand(Guid TimesheetId) : ICommand<PayslipDto>;

public sealed class RunPayrollHandler(IPayrollDbContext db, TimeProvider clock, IOptions<PayrollOptions> options)
    : ICommandHandler<RunPayrollCommand, PayslipDto>
{
    public async Task<PayslipDto> HandleAsync(RunPayrollCommand command, CancellationToken cancellationToken)
    {
        var timesheet = await db.Timesheets.Include(t => t.Entries)
            .FirstOrDefaultAsync(t => t.Id == command.TimesheetId, cancellationToken)
            ?? throw new NotFoundException("Timesheet", command.TimesheetId);

        if (await db.Payslips.AnyAsync(p => p.TimesheetId == timesheet.Id, cancellationToken))
        {
            throw new ConflictException("A payslip has already been produced for this timesheet.");
        }

        var contractor = await db.Contractors.FirstOrDefaultAsync(c => c.Id == timesheet.ContractorId, cancellationToken)
            ?? throw new NotFoundException("Contractor", timesheet.ContractorId);

        // Throws DomainException (→ 422) unless the timesheet is Approved.
        timesheet.MarkProcessed();

        var breakdown = PayCalculator.Calculate(
            new PayInput(
                StandardHours: timesheet.StandardHours,
                OvertimeHours: timesheet.OvertimeHours,
                HourlyRate: contractor.HourlyRate,
                UmbrellaMargin: options.Value.MarginFor(contractor.PayFrequency),
                TaxCode: TaxCode.Parse(contractor.TaxCode),
                Frequency: contractor.PayFrequency),
            TaxYearRules.Illustrative);

        var payslip = new Payslip(Guid.NewGuid(), contractor.Id, timesheet.Id, timesheet.PeriodStart, timesheet.PeriodEnd, breakdown, clock.GetUtcNow());

        db.Payslips.Add(payslip);
        await db.SaveChangesAsync(cancellationToken);

        return PayslipDto.From(payslip);
    }
}

// ---------------------------------------------------------------- Stateless "what-if" calculator

public sealed record CalculatePayQuery(
    decimal StandardHours,
    decimal OvertimeHours,
    decimal HourlyRate,
    string TaxCode,
    PayFrequency PayFrequency,
    decimal? UmbrellaMargin = null) : IQuery<PayBreakdown>;

public sealed class CalculatePayValidator : AbstractValidator<CalculatePayQuery>
{
    public CalculatePayValidator()
    {
        RuleFor(x => x.StandardHours).InclusiveBetween(0, 744);
        RuleFor(x => x.OvertimeHours).InclusiveBetween(0, 744);
        RuleFor(x => x.HourlyRate).GreaterThan(0).LessThanOrEqualTo(1_000);
        RuleFor(x => x.PayFrequency).IsInEnum();
        RuleFor(x => x.UmbrellaMargin).GreaterThanOrEqualTo(0).When(x => x.UmbrellaMargin.HasValue);
        RuleFor(x => x.TaxCode)
            .NotEmpty()
            .Must(code => Domain.Pay.TaxCode.TryParse(code, out _))
            .WithMessage("Unsupported tax code. Examples: 1257L, 0T, K100, BR, D0, D1, NT.");
    }
}

public sealed class CalculatePayHandler(IOptions<PayrollOptions> options) : IQueryHandler<CalculatePayQuery, PayBreakdown>
{
    public Task<PayBreakdown> HandleAsync(CalculatePayQuery query, CancellationToken cancellationToken)
    {
        var breakdown = PayCalculator.Calculate(
            new PayInput(
                query.StandardHours,
                query.OvertimeHours,
                query.HourlyRate,
                query.UmbrellaMargin ?? options.Value.MarginFor(query.PayFrequency),
                TaxCode.Parse(query.TaxCode),
                query.PayFrequency),
            TaxYearRules.Illustrative);

        return Task.FromResult(breakdown);
    }
}

// ---------------------------------------------------------------- Queries

public sealed record GetPayslipQuery(Guid Id) : IQuery<PayslipDto>;

public sealed class GetPayslipHandler(IPayrollDbContext db) : IQueryHandler<GetPayslipQuery, PayslipDto>
{
    public async Task<PayslipDto> HandleAsync(GetPayslipQuery query, CancellationToken cancellationToken)
    {
        var payslip = await db.Payslips.AsNoTracking().FirstOrDefaultAsync(p => p.Id == query.Id, cancellationToken)
            ?? throw new NotFoundException("Payslip", query.Id);

        return PayslipDto.From(payslip);
    }
}

public sealed record ListPayslipsQuery(Guid ContractorId) : IQuery<IReadOnlyList<PayslipDto>>;

public sealed class ListPayslipsHandler(IPayrollDbContext db) : IQueryHandler<ListPayslipsQuery, IReadOnlyList<PayslipDto>>
{
    public async Task<IReadOnlyList<PayslipDto>> HandleAsync(ListPayslipsQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Contractors.AnyAsync(c => c.Id == query.ContractorId, cancellationToken))
        {
            throw new NotFoundException("Contractor", query.ContractorId);
        }

        var items = await db.Payslips.AsNoTracking()
            .Where(p => p.ContractorId == query.ContractorId)
            .OrderByDescending(p => p.PeriodStart)
            .ToListAsync(cancellationToken);

        return items.ConvertAll(PayslipDto.From);
    }
}
