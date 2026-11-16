using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Timesheets;

namespace Payroll.Application.Timesheets;

public sealed record TimesheetEntryDto(DateOnly WorkDate, decimal Hours, HoursType Type);

public sealed record TimesheetDto(
    Guid Id,
    Guid ContractorId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    TimesheetStatus Status,
    decimal StandardHours,
    decimal OvertimeHours,
    decimal TotalHours,
    string? RejectionReason,
    IReadOnlyList<TimesheetEntryDto> Entries)
{
    public static TimesheetDto From(Timesheet t) => new(
        t.Id, t.ContractorId, t.PeriodStart, t.PeriodEnd, t.Status,
        t.StandardHours, t.OvertimeHours, t.TotalHours, t.RejectionReason,
        t.Entries.OrderBy(e => e.WorkDate).ThenBy(e => e.Type)
            .Select(e => new TimesheetEntryDto(e.WorkDate, e.Hours, e.Type)).ToList());
}

// ---------------------------------------------------------------- Create

public sealed record CreateTimesheetCommand(
    Guid ContractorId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<TimesheetEntryDto> Entries) : ICommand<TimesheetDto>;

public sealed class CreateTimesheetValidator : AbstractValidator<CreateTimesheetCommand>
{
    public CreateTimesheetValidator()
    {
        RuleFor(x => x.ContractorId).NotEmpty();
        RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart)
            .WithMessage("Period end must be on or after period start.");
        RuleFor(x => x.Entries).NotNull();
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Hours).GreaterThan(0).LessThanOrEqualTo(Timesheet.MaxHoursPerDay);
            entry.RuleFor(e => e.Type).IsInEnum();
        });
    }
}

public sealed class CreateTimesheetHandler(IPayrollDbContext db, TimeProvider clock)
    : ICommandHandler<CreateTimesheetCommand, TimesheetDto>
{
    public async Task<TimesheetDto> HandleAsync(CreateTimesheetCommand command, CancellationToken cancellationToken)
    {
        if (!await db.Contractors.AnyAsync(c => c.Id == command.ContractorId, cancellationToken))
        {
            throw new NotFoundException("Contractor", command.ContractorId);
        }

        var overlaps = await db.Timesheets.AnyAsync(
            t => t.ContractorId == command.ContractorId
                 && t.Status != TimesheetStatus.Rejected
                 && t.PeriodStart <= command.PeriodEnd
                 && t.PeriodEnd >= command.PeriodStart,
            cancellationToken);

        if (overlaps)
        {
            throw new ConflictException("The contractor already has a timesheet that overlaps this period.");
        }

        // Domain enforces the invariants (period length, hours per day, dates inside period).
        var timesheet = new Timesheet(Guid.NewGuid(), command.ContractorId, command.PeriodStart, command.PeriodEnd, clock.GetUtcNow());
        foreach (var entry in command.Entries ?? [])
        {
            timesheet.AddEntry(entry.WorkDate, entry.Hours, entry.Type);
        }

        db.Timesheets.Add(timesheet);
        await db.SaveChangesAsync(cancellationToken);

        return TimesheetDto.From(timesheet);
    }
}

// ---------------------------------------------------------------- State transitions

public enum TimesheetTransition
{
    Submit,
    Approve,
    Reject,
}

public sealed record TransitionTimesheetCommand(Guid Id, TimesheetTransition Transition, string? Reason = null) : ICommand<TimesheetDto>;

public sealed class TransitionTimesheetHandler(IPayrollDbContext db, TimeProvider clock)
    : ICommandHandler<TransitionTimesheetCommand, TimesheetDto>
{
    public async Task<TimesheetDto> HandleAsync(TransitionTimesheetCommand command, CancellationToken cancellationToken)
    {
        var timesheet = await db.Timesheets.Include(t => t.Entries).FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Timesheet", command.Id);

        switch (command.Transition)
        {
            case TimesheetTransition.Submit:
                timesheet.Submit(clock.GetUtcNow());
                break;
            case TimesheetTransition.Approve:
                timesheet.Approve(clock.GetUtcNow());
                break;
            case TimesheetTransition.Reject:
                timesheet.Reject(command.Reason ?? string.Empty);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command.Transition, "Unknown transition.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return TimesheetDto.From(timesheet);
    }
}

// ---------------------------------------------------------------- Queries

public sealed record GetTimesheetQuery(Guid Id) : IQuery<TimesheetDto>;

public sealed class GetTimesheetHandler(IPayrollDbContext db) : IQueryHandler<GetTimesheetQuery, TimesheetDto>
{
    public async Task<TimesheetDto> HandleAsync(GetTimesheetQuery query, CancellationToken cancellationToken)
    {
        var timesheet = await db.Timesheets.AsNoTracking().Include(t => t.Entries)
            .FirstOrDefaultAsync(t => t.Id == query.Id, cancellationToken)
            ?? throw new NotFoundException("Timesheet", query.Id);

        return TimesheetDto.From(timesheet);
    }
}

public sealed record ListTimesheetsQuery(Guid? ContractorId = null, TimesheetStatus? Status = null) : IQuery<IReadOnlyList<TimesheetDto>>;

public sealed class ListTimesheetsHandler(IPayrollDbContext db) : IQueryHandler<ListTimesheetsQuery, IReadOnlyList<TimesheetDto>>
{
    public async Task<IReadOnlyList<TimesheetDto>> HandleAsync(ListTimesheetsQuery query, CancellationToken cancellationToken)
    {
        var source = db.Timesheets.AsNoTracking().Include(t => t.Entries).AsQueryable();

        if (query.ContractorId is { } contractorId)
        {
            source = source.Where(t => t.ContractorId == contractorId);
        }

        if (query.Status is { } status)
        {
            source = source.Where(t => t.Status == status);
        }

        var items = await source.OrderByDescending(t => t.PeriodStart).Take(200).ToListAsync(cancellationToken);
        return items.ConvertAll(TimesheetDto.From);
    }
}
