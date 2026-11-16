using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Contractors;
using Payroll.Domain.Pay;

namespace Payroll.Application.Contractors;

public sealed record ContractorDto(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string NationalInsuranceNumber,
    string TaxCode,
    decimal HourlyRate,
    PayFrequency PayFrequency)
{
    public static ContractorDto From(Contractor c) => new(
        c.Id, c.EmployeeNumber, c.FirstName, c.LastName, c.Email,
        c.NationalInsuranceNumber, c.TaxCode, c.HourlyRate, c.PayFrequency);
}

// ---------------------------------------------------------------- Create

public sealed record CreateContractorCommand(
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string NationalInsuranceNumber,
    string TaxCode,
    decimal HourlyRate,
    PayFrequency PayFrequency) : ICommand<ContractorDto>;

public sealed class CreateContractorValidator : AbstractValidator<CreateContractorCommand>
{
    // Format check only (two-letter prefix, six digits, suffix A-D); HMRC's full prefix rules are not enforced.
    // "QQ" is HMRC's reserved *example* prefix — it is accepted explicitly so that demo data is never a real NINO.
    private const string NinoPattern = "^(QQ|[A-CEGHJ-PR-TW-Z][A-CEGHJ-NPR-TW-Z])[0-9]{6}[A-D]$";

    public CreateContractorValidator()
    {
        RuleFor(x => x.EmployeeNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.NationalInsuranceNumber)
            .NotEmpty()
            .Must(n => System.Text.RegularExpressions.Regex.IsMatch(n?.Replace(" ", string.Empty).ToUpperInvariant() ?? string.Empty, NinoPattern))
            .WithMessage("National Insurance number must look like 'QQ123456C'.");
        RuleFor(x => x.TaxCode)
            .NotEmpty()
            .Must(code => Domain.Pay.TaxCode.TryParse(code, out _))
            .WithMessage("Unsupported tax code. Examples: 1257L, 0T, K100, BR, D0, D1, NT.");
        RuleFor(x => x.HourlyRate).GreaterThan(0).LessThanOrEqualTo(1_000);
        RuleFor(x => x.PayFrequency).IsInEnum();
    }
}

public sealed class CreateContractorHandler(IPayrollDbContext db, TimeProvider clock)
    : ICommandHandler<CreateContractorCommand, ContractorDto>
{
    public async Task<ContractorDto> HandleAsync(CreateContractorCommand command, CancellationToken cancellationToken)
    {
        var nino = command.NationalInsuranceNumber.Replace(" ", string.Empty).ToUpperInvariant();

        var duplicate = await db.Contractors.AnyAsync(
            c => c.EmployeeNumber == command.EmployeeNumber || c.NationalInsuranceNumber == nino,
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException("A contractor with the same employee number or NI number already exists.");
        }

        var contractor = new Contractor(
            Guid.NewGuid(),
            command.EmployeeNumber.Trim(),
            command.FirstName.Trim(),
            command.LastName.Trim(),
            command.Email.Trim(),
            nino,
            command.TaxCode,
            command.HourlyRate,
            command.PayFrequency,
            clock.GetUtcNow());

        db.Contractors.Add(contractor);
        await db.SaveChangesAsync(cancellationToken);

        return ContractorDto.From(contractor);
    }
}

// ---------------------------------------------------------------- Get by id

public sealed record GetContractorQuery(Guid Id) : IQuery<ContractorDto>;

public sealed class GetContractorHandler(IPayrollDbContext db) : IQueryHandler<GetContractorQuery, ContractorDto>
{
    public async Task<ContractorDto> HandleAsync(GetContractorQuery query, CancellationToken cancellationToken)
    {
        var contractor = await db.Contractors.AsNoTracking().FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken)
            ?? throw new NotFoundException("Contractor", query.Id);

        return ContractorDto.From(contractor);
    }
}

// ---------------------------------------------------------------- List (paged + search)

public sealed record ListContractorsQuery(int Page = 1, int PageSize = 20, string? Search = null) : IQuery<PagedResult<ContractorDto>>;

public sealed class ListContractorsValidator : AbstractValidator<ListContractorsQuery>
{
    public ListContractorsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public sealed class ListContractorsHandler(IPayrollDbContext db) : IQueryHandler<ListContractorsQuery, PagedResult<ContractorDto>>
{
    public async Task<PagedResult<ContractorDto>> HandleAsync(ListContractorsQuery query, CancellationToken cancellationToken)
    {
        var source = db.Contractors.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(c =>
                c.FirstName.Contains(term) || c.LastName.Contains(term) || c.EmployeeNumber.Contains(term));
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderBy(c => c.EmployeeNumber)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ContractorDto>(items.ConvertAll(ContractorDto.From), query.Page, query.PageSize, total);
    }
}
