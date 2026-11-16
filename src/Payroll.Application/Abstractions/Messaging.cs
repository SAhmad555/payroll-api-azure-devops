namespace Payroll.Application.Abstractions;

/// <summary>Marker for state-changing requests.</summary>
public interface ICommand<TResult>;

/// <summary>Marker for read-only requests.</summary>
public interface IQuery<TResult>;

/// <summary>
/// CQRS-lite: one handler per use case, resolved straight from DI. No mediator library needed —
/// cross-cutting concerns (validation, errors, auth) live in the API pipeline.
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
