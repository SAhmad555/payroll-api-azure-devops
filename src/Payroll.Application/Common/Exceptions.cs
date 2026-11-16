namespace Payroll.Application.Common;

/// <summary>Requested resource does not exist → HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, object key)
        : base($"{resource} '{key}' was not found.") { }

    public NotFoundException() { }

    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Request conflicts with current state (duplicates, overlaps) → HTTP 409.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException() { }

    public ConflictException(string message) : base(message) { }

    public ConflictException(string message, Exception innerException) : base(message, innerException) { }
}
