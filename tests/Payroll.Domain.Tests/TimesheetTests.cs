using Payroll.Domain.Common;
using Payroll.Domain.Timesheets;

namespace Payroll.Domain.Tests;

public class TimesheetTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    private static Timesheet NewWeek() => new(Guid.NewGuid(), Guid.NewGuid(), Monday, Monday.AddDays(6), Now);

    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Code;

    [Fact]
    public void Happy_path_draft_to_processed()
    {
        var sheet = NewWeek();
        sheet.AddEntry(Monday, 7.5m, HoursType.Standard);
        sheet.AddEntry(Monday, 2m, HoursType.Overtime);

        sheet.Submit(Now);
        sheet.Approve(Now);
        sheet.MarkProcessed();

        Assert.Equal(TimesheetStatus.Processed, sheet.Status);
        Assert.Equal(7.5m, sheet.StandardHours);
        Assert.Equal(2m, sheet.OvertimeHours);
        Assert.Equal(9.5m, sheet.TotalHours);
    }

    [Fact]
    public void Period_end_before_start_is_rejected() =>
        Assert.Equal("timesheet.invalid_period", CodeOf(() => _ = new Timesheet(Guid.NewGuid(), Guid.NewGuid(), Monday, Monday.AddDays(-1), Now)));

    [Fact]
    public void Period_longer_than_31_days_is_rejected() =>
        Assert.Equal("timesheet.period_too_long", CodeOf(() => _ = new Timesheet(Guid.NewGuid(), Guid.NewGuid(), Monday, Monday.AddDays(31), Now)));

    [Fact]
    public void Single_day_period_is_allowed()
    {
        var sheet = new Timesheet(Guid.NewGuid(), Guid.NewGuid(), Monday, Monday, Now);
        sheet.AddEntry(Monday, 8m, HoursType.Standard);
        Assert.Single(sheet.Entries);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void Entry_outside_period_is_rejected(int offsetDays) =>
        Assert.Equal("timesheet.date_outside_period", CodeOf(() => NewWeek().AddEntry(Monday.AddDays(offsetDays), 1m, HoursType.Standard)));

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(24.01)]
    public void Invalid_hours_are_rejected(decimal hours) =>
        Assert.Equal("timesheet.invalid_hours", CodeOf(() => NewWeek().AddEntry(Monday, hours, HoursType.Standard)));

    [Fact]
    public void Exactly_24_hours_in_a_day_is_allowed_but_not_more()
    {
        var sheet = NewWeek();
        sheet.AddEntry(Monday, 16m, HoursType.Standard);
        sheet.AddEntry(Monday, 8m, HoursType.Overtime);

        Assert.Equal("timesheet.day_overbooked", CodeOf(() => sheet.AddEntry(Monday, 0.25m, HoursType.Overtime)));
    }

    [Fact]
    public void Empty_timesheet_cannot_be_submitted() =>
        Assert.Equal("timesheet.empty", CodeOf(() => NewWeek().Submit(Now)));

    [Fact]
    public void Submitted_timesheet_cannot_be_edited()
    {
        var sheet = NewWeek();
        sheet.AddEntry(Monday, 8m, HoursType.Standard);
        sheet.Submit(Now);

        Assert.Equal("timesheet.not_editable", CodeOf(() => sheet.AddEntry(Monday.AddDays(1), 8m, HoursType.Standard)));
    }

    [Fact]
    public void Draft_cannot_be_approved_or_processed()
    {
        var sheet = NewWeek();
        Assert.Equal("timesheet.not_submitted", CodeOf(() => sheet.Approve(Now)));
        Assert.Equal("timesheet.not_approved", CodeOf(sheet.MarkProcessed));
    }

    [Fact]
    public void Reject_requires_a_reason_and_records_it()
    {
        var sheet = NewWeek();
        sheet.AddEntry(Monday, 8m, HoursType.Standard);
        sheet.Submit(Now);

        Assert.Equal("timesheet.reason_required", CodeOf(() => sheet.Reject("  ")));

        sheet.Reject("  Hours do not match the client's sign-off  ");
        Assert.Equal(TimesheetStatus.Rejected, sheet.Status);
        Assert.Equal("Hours do not match the client's sign-off", sheet.RejectionReason);
    }

    [Fact]
    public void Processed_timesheet_cannot_be_processed_twice()
    {
        var sheet = NewWeek();
        sheet.AddEntry(Monday, 8m, HoursType.Standard);
        sheet.Submit(Now);
        sheet.Approve(Now);
        sheet.MarkProcessed();

        Assert.Equal("timesheet.not_approved", CodeOf(sheet.MarkProcessed));
    }
}
