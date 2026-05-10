namespace SixToFix.Application.Exceptions;

public sealed class TelemetryAlreadyInitializedException : Exception
{
    public Guid AuditRunId { get; }

    public TelemetryAlreadyInitializedException(Guid auditRunId)
        : base($"Telemetry for AuditRun '{auditRunId}' has already been initialized.")
    {
        AuditRunId = auditRunId;
    }
}
