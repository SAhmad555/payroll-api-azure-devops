namespace Payroll.Domain.Common;

/// <summary>
/// Raised when a business rule is violated (e.g. approving a timesheet that was never submitted).
/// The API layer maps this to an HTTP 422 ProblemDetails response.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message) : base(message) => Code = code;

    public DomainException() : this("domain.error", "A business rule was violated.") { }

    public DomainException(string message) : this("domain.error", message) { }

    public DomainException(string message, Exception innerException) : base(message, innerException) => Code = "domain.error";

    /// <summary>Stable, machine-readable error code (e.g. <c>timesheet.not_submitted</c>).</summary>
    public string Code { get; }
}
