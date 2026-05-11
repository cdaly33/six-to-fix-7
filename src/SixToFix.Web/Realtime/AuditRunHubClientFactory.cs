using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace SixToFix.Web.Realtime;

public sealed class AuditRunHubClientFactory(NavigationManager navigationManager) : IAuditRunHubClientFactory
{
    public IAuditRunHubClient Create()
    {
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(navigationManager.ToAbsoluteUri("/hubs/audit-run"))
            .WithAutomaticReconnect()
            .Build();

        return new AuditRunHubClient(hubConnection);
    }

    private sealed class AuditRunHubClient(HubConnection hubConnection) : IAuditRunHubClient
    {
        public void OnReceiveEvent(Func<string, object?, Task> handler)
        {
            hubConnection.On<string, object?>("ReceiveEvent", handler);
        }

        public Task StartAsync(CancellationToken ct = default) => hubConnection.StartAsync(ct);

        public Task JoinAuditRunAsync(string auditRunId, CancellationToken ct = default) =>
            hubConnection.InvokeAsync("JoinAuditRun", auditRunId, ct);

        public Task LeaveAuditRunAsync(string auditRunId, CancellationToken ct = default) =>
            hubConnection.InvokeAsync("LeaveAuditRun", auditRunId, ct);

        public ValueTask DisposeAsync() => hubConnection.DisposeAsync();
    }
}
