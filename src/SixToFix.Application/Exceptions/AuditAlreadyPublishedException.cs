namespace SixToFix.Application.Exceptions;

public sealed class AuditAlreadyPublishedException : Exception
{
    public Guid AuditRunId { get; }

    public AuditAlreadyPublishedException(Guid auditRunId)
        : base($"AuditRun '{auditRunId}' has already been published.")
    {
        AuditRunId = auditRunId;
    }
}
