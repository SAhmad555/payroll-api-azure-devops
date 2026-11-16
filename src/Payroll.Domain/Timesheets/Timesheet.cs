using Payroll.Domain.Common;

namespace Payroll.Domain.Timesheets;

public enum TimesheetStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Processed = 4,
}

public enum HoursType
{
    Standard = 0,
    Overtime = 1,
}

public sealed class TimesheetEntry
{
    private TimesheetEntry() { } // EF Core

    internal TimesheetEntry(Guid id, DateOnly workDate, decimal hours, HoursType type)
    {
        Id = id;
        WorkDate = workDate;
        Hours = hours;
        Type = type;
    }

    public Guid Id { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public decimal Hours { get; private set; }
    public HoursType Type { get; private set; }
}

/// <summary>
/// Aggregate root for a contractor's hours in one pay period.
/// Lifecycle: <c>Draft → Submitted → Approved → Processed</c> (or <c>Submitted → Rejected</c>).
/// </summary>
public sealed class Timesheet
{
    public const int MaxPeriodDays = 31;
    public const decimal MaxHoursPerDay = 24m;

    private readonly List<TimesheetEntry> _entries = [];

    private Timesheet() { } // EF Core

    public Timesheet(Guid id, Guid contractorId, DateOnly periodStart, DateOnly periodEnd, DateTimeOffset createdAtUtc)
    {
        if (periodEnd < periodStart)
        {
            throw new DomainException("timesheet.invalid_period", "Period end must be on or after period start.");
        }

        if (periodEnd.DayNumber - periodStart.DayNumber + 1 > MaxPeriodDays)
        {
            throw new DomainException("timesheet.period_too_long", $"A timesheet period cannot exceed {MaxPeriodDays} days.");
        }

        Id = id;
        ContractorId = contractorId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Status = TimesheetStatus.Draft;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ContractorId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public TimesheetStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public IReadOnlyCollection<TimesheetEntry> Entries => _entries.AsReadOnly();

    public decimal StandardHours => _entries.Where(e => e.Type == HoursType.Standard).Sum(e => e.Hours);
    public decimal OvertimeHours => _entries.Where(e => e.Type == HoursType.Overtime).Sum(e => e.Hours);
    public decimal TotalHours => _entries.Sum(e => e.Hours);

    public void AddEntry(DateOnly workDate, decimal hours, HoursType type)
    {
        EnsureStatus(TimesheetStatus.Draft, "timesheet.not_editable", "Only draft timesheets can be edited.");

        if (workDate < PeriodStart || workDate > PeriodEnd)
        {
            throw new DomainException("timesheet.date_outside_period", $"{workDate:yyyy-MM-dd} is outside the timesheet period.");
        }

        if (hours <= 0m || hours > MaxHoursPerDay)
        {
            throw new DomainException("timesheet.invalid_hours", $"Hours must be greater than 0 and at most {MaxHoursPerDay}.");
        }

        var alreadyLogged = _entries.Where(e => e.WorkDate == workDate).Sum(e => e.Hours);
        if (alreadyLogged + hours > MaxHoursPerDay)
        {
            throw new DomainException("timesheet.day_overbooked", $"More than {MaxHoursPerDay} hours logged on {workDate:yyyy-MM-dd}.");
        }

        _entries.Add(new TimesheetEntry(Guid.NewGuid(), workDate, hours, type));
    }

    public void Submit(DateTimeOffset nowUtc)
    {
        EnsureStatus(TimesheetStatus.Draft, "timesheet.not_draft", "Only draft timesheets can be submitted.");

        if (_entries.Count == 0)
        {
            throw new DomainException("timesheet.empty", "A timesheet must contain at least one entry before submission.");
        }

        Status = TimesheetStatus.Submitted;
        SubmittedAtUtc = nowUtc;
    }

    public void Approve(DateTimeOffset nowUtc)
    {
        EnsureStatus(TimesheetStatus.Submitted, "timesheet.not_submitted", "Only submitted timesheets can be approved.");
        Status = TimesheetStatus.Approved;
        ApprovedAtUtc = nowUtc;
    }

    public void Reject(string reason)
    {
        EnsureStatus(TimesheetStatus.Submitted, "timesheet.not_submitted", "Only submitted timesheets can be rejected.");

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("timesheet.reason_required", "A rejection reason is required.");
        }

        Status = TimesheetStatus.Rejected;
        RejectionReason = reason.Trim();
    }

    public void MarkProcessed()
    {
        EnsureStatus(TimesheetStatus.Approved, "timesheet.not_approved", "Only approved timesheets can be processed for payroll.");
        Status = TimesheetStatus.Processed;
    }

    private void EnsureStatus(TimesheetStatus expected, string code, string message)
    {
        if (Status != expected)
        {
            throw new DomainException(code, $"{message} Current status: {Status}.");
        }
    }
}
