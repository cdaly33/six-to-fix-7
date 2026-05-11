namespace SixToFix.Web.Tests.Fakes;

public sealed class FakeAuditRunHubClient : IAuditRunHubClient, IAuditRunHubClientFactory
{
    private Func<string, object?, Task>? _handler;

    public bool Started { get; private set; }
    public string? JoinedAuditRunId { get; private set; }
    public string? LeftAuditRunId { get; private set; }

    public IAuditRunHubClient Create() => this;

    public void OnReceiveEvent(Func<string, object?, Task> handler) => _handler = handler;

    public Task StartAsync(CancellationToken ct = default)
    {
        Started = true;
        return Task.CompletedTask;
    }

    public Task JoinAuditRunAsync(string auditRunId, CancellationToken ct = default)
    {
        JoinedAuditRunId = auditRunId;
        return Task.CompletedTask;
    }

    public Task LeaveAuditRunAsync(string auditRunId, CancellationToken ct = default)
    {
        LeftAuditRunId = auditRunId;
        return Task.CompletedTask;
    }

    public Task TriggerAsync(string eventType, object? payload = null) => _handler?.Invoke(eventType, payload) ?? Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
