using Microsoft.AspNetCore.Http.HttpResults;
using Payroll.Api.Infrastructure;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Application.Contractors;
using Payroll.Application.Payslips;
using Payroll.Application.Timesheets;
using Payroll.Domain.Pay;
using Payroll.Domain.Timesheets;

namespace Payroll.Api.Endpoints;

public sealed record RejectTimesheetRequest(string Reason);

internal static class PayrollEndpoints
{
    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder v1)
    {
        MapContractors(v1.MapGroup("/contractors").WithTags("Contractors"));
        MapTimesheets(v1.MapGroup("/timesheets").WithTags("Timesheets"));
        MapPayslips(v1.MapGroup("/payslips").WithTags("Payslips"));
        MapCalculator(v1.MapGroup("/payroll").WithTags("Payroll calculator"));
        return v1;
    }

    private static void MapContractors(RouteGroupBuilder group)
    {
        group.RequireAuthorization(Policies.PayrollRead);

        group.MapGet("/", async Task<Ok<PagedResult<ContractorDto>>> (
                [AsParameters] ListContractorsQuery query,
                IQueryHandler<ListContractorsQuery, PagedResult<ContractorDto>> handler,
                CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(query, ct)))
            .Validate<ListContractorsQuery>()
            .WithName("ListContractors")
            .WithSummary("List contractors (paged, optional name/number search).");

        group.MapGet("/{id:guid}", async Task<Ok<ContractorDto>> (
                Guid id,
                IQueryHandler<GetContractorQuery, ContractorDto> handler,
                CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(new GetContractorQuery(id), ct)))
            .WithName("GetContractor")
            .WithSummary("Get a contractor by id.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async Task<Created<ContractorDto>> (
                CreateContractorCommand command,
                ICommandHandler<CreateContractorCommand, ContractorDto> handler,
                CancellationToken ct) =>
            {
                var created = await handler.HandleAsync(command, ct);
                return TypedResults.Created($"/api/v1/contractors/{created.Id}", created);
            })
            .Validate<CreateContractorCommand>()
            .RequireAuthorization(Policies.PayrollAdmin)
            .WithName("CreateContractor")
            .WithSummary("Onboard a new contractor.")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id:guid}/payslips", async Task<Ok<IReadOnlyList<PayslipDto>>> (
                Guid id,
                IQueryHandler<ListPayslipsQuery, IReadOnlyList<PayslipDto>> handler,
                CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(new ListPayslipsQuery(id), ct)))
            .WithName("ListContractorPayslips")
            .WithSummary("List a contractor's payslips, newest first.")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapTimesheets(RouteGroupBuilder group)
    {
        group.RequireAuthorization(Policies.PayrollRead);

        group.MapGet("/", async Task<Ok<IReadOnlyList<TimesheetDto>>> (
                Guid? contractorId,
                TimesheetStatus? status,
                IQueryHandler<ListTimesheetsQuery, IReadOnlyList<TimesheetDto>> handler,
                CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(new ListTimesheetsQuery(contractorId, status), ct)))
            .WithName("ListTimesheets")
            .WithSummary("List timesheets, optionally filtered by contractor and status.");

        group.MapGet("/{id:guid}", async Task<Ok<TimesheetDto>> (
                Guid id,
                IQueryHandler<GetTimesheetQuery, TimesheetDto> handler,
                CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(new GetTimesheetQuery(id), ct)))
            .WithName("GetTimesheet")
            .WithSummary("Get a timesheet with its daily entries.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async Task<Created<TimesheetDto>> (
                CreateTimesheetCommand command,
                ICommandHandler<CreateTimesheetCommand, TimesheetDto> handler,
                CancellationToken ct) =>
            {
                var created = await handler.HandleAsync(command, ct);
                return TypedResults.Created($"/api/v1/timesheets/{created.Id}", created);
            })
            .Validate<CreateTimesheetCommand>()
            .WithName("CreateTimesheet")
            .WithSummary("Create a draft timesheet with daily entries.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/submit", (Guid id, ICommandHandler<TransitionTimesheetCommand, TimesheetDto> handler, CancellationToken ct) =>
                Transition(handler, new TransitionTimesheetCommand(id, TimesheetTransition.Submit), ct))
            .WithName("SubmitTimesheet")
            .WithSummary("Submit a draft timesheet for approval.")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/approve", (Guid id, ICommandHandler<TransitionTimesheetCommand, TimesheetDto> handler, CancellationToken ct) =>
                Transition(handler, new TransitionTimesheetCommand(id, TimesheetTransition.Approve), ct))
            .RequireAuthorization(Policies.PayrollAdmin)
            .WithName("ApproveTimesheet")
            .WithSummary("Approve a submitted timesheet.")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/reject", (Guid id, RejectTimesheetRequest request, ICommandHandler<TransitionTimesheetCommand, TimesheetDto> handler, CancellationToken ct) =>
                Transition(handler, new TransitionTimesheetCommand(id, TimesheetTransition.Reject, request.Reason), ct))
            .RequireAuthorization(Policies.PayrollAdmin)
            .WithName("RejectTimesheet")
            .WithSummary("Reject a submitted timesheet with a reason.")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/payslip", async Task<Created<PayslipDto>> (
                Guid id,
                ICommandHandler<RunPayrollCommand, PayslipDto> handler,
                CancellationToken ct) =>
            {
                var payslip = await handler.HandleAsync(new RunPayrollCommand(id), ct);
                return TypedResults.Created($"/api/v1/payslips/{payslip.Id}", payslip);
            })
            .RequireAuthorization(Policies.PayrollAdmin)
            .WithName("RunPayroll")
            .WithSummary("Run payroll for an approved timesheet and issue a payslip.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapPayslips(RouteGroupBuilder group)
    {
        group.RequireAuthorization(Policies.PayrollRead);

        group.MapGet("/{id:guid}", async Task<Ok<PayslipDto>> (
                Guid id,
                IQueryHandler<GetPayslipQuery, PayslipDto> handler,
                CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(new GetPayslipQuery(id), ct)))
            .WithName("GetPayslip")
            .WithSummary("Get a payslip with its full gross-to-net breakdown.")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapCalculator(RouteGroupBuilder group)
    {
        group.MapPost("/calculate", async Task<Ok<PayBreakdown>> (
                CalculatePayQuery query,
                IQueryHandler<CalculatePayQuery, PayBreakdown> handler,
                CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(query, ct)))
            .Validate<CalculatePayQuery>()
            .AllowAnonymous()
            .WithName("CalculatePay")
            .WithSummary("Stateless gross-to-net calculator (illustrative rates, not tax advice).");
    }

    private static async Task<Ok<TimesheetDto>> Transition(
        ICommandHandler<TransitionTimesheetCommand, TimesheetDto> handler,
        TransitionTimesheetCommand command,
        CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(command, ct));
}
