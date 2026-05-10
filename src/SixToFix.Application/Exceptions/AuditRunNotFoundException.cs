namespace SixToFix.Application.Exceptions;

public sealed class AuditRunNotFoundException : Exception
{
    public Guid AuditRunId { get; }

    public AuditRunNotFoundException(Guid auditRunId)
        : base($"AuditRun '{auditRunId}' was not found.")
    {
        AuditRunId = auditRunId;
    }
}
