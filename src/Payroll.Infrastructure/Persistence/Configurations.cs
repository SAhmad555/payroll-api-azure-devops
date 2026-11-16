using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payroll.Domain.Contractors;
using Payroll.Domain.Pay;
using Payroll.Domain.Timesheets;

namespace Payroll.Infrastructure.Persistence;

internal sealed class ContractorConfiguration : IEntityTypeConfiguration<Contractor>
{
    public void Configure(EntityTypeBuilder<Contractor> builder)
    {
        builder.ToTable("Contractors");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.EmployeeNumber).HasMaxLength(20).IsRequired();
        builder.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.LastName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256).IsRequired();
        builder.Property(c => c.NationalInsuranceNumber).HasMaxLength(9).IsRequired();
        builder.Property(c => c.TaxCode).HasMaxLength(10).IsRequired();
        builder.Property(c => c.HourlyRate).HasPrecision(9, 2);
        builder.Ignore(c => c.FullName);

        builder.HasIndex(c => c.EmployeeNumber).IsUnique();
        builder.HasIndex(c => c.NationalInsuranceNumber).IsUnique();
    }
}

internal sealed class TimesheetConfiguration : IEntityTypeConfiguration<Timesheet>
{
    public void Configure(EntityTypeBuilder<Timesheet> builder)
    {
        builder.ToTable("Timesheets");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.RejectionReason).HasMaxLength(500);
        builder.Ignore(t => t.StandardHours);
        builder.Ignore(t => t.OvertimeHours);
        builder.Ignore(t => t.TotalHours);

        builder.HasOne<Contractor>().WithMany().HasForeignKey(t => t.ContractorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.ContractorId, t.PeriodStart });

        builder.HasMany(t => t.Entries).WithOne().HasForeignKey("TimesheetId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TimesheetEntryConfiguration : IEntityTypeConfiguration<TimesheetEntry>
{
    public void Configure(EntityTypeBuilder<TimesheetEntry> builder)
    {
        builder.ToTable("TimesheetEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Hours).HasPrecision(5, 2);
    }
}

internal sealed class PayslipConfiguration : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> builder)
    {
        builder.ToTable("Payslips");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.TaxYear).HasMaxLength(32).IsRequired();
        builder.Property(p => p.TaxCode).HasMaxLength(10).IsRequired();
        builder.Property(p => p.StandardHours).HasPrecision(7, 2);
        builder.Property(p => p.OvertimeHours).HasPrecision(7, 2);

        builder.HasOne<Contractor>().WithMany().HasForeignKey(p => p.ContractorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Timesheet>().WithMany().HasForeignKey(p => p.TimesheetId).OnDelete(DeleteBehavior.Restrict);

        // One payslip per timesheet — enforced by the database as well as the handler.
        builder.HasIndex(p => p.TimesheetId).IsUnique();
        builder.HasIndex(p => new { p.ContractorId, p.PeriodStart });
    }
}
