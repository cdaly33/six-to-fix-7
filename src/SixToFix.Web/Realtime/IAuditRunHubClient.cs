namespace SixToFix.Web.Realtime;

public interface IAuditRunHubClient : IAsyncDisposable
{
    void OnReceiveEvent(Func<string, object?, Task> handler);
    Task StartAsync(CancellationToken ct = default);
    Task JoinAuditRunAsync(string auditRunId, CancellationToken ct = default);
    Task LeaveAuditRunAsync(string auditRunId, CancellationToken ct = default);
}

public interface IAuditRunHubClientFactory
{
    IAuditRunHubClient Create();
}
