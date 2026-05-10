using Microsoft.AspNetCore.SignalR;
using SixToFix.Application.Hubs;
using SixToFix.Application.Services;

namespace SixToFix.Infrastructure.Hubs;

/// <summary>
/// IRealtimeNotifier implementation that delegates to the SignalR AuditRunHub.
/// Allows Infrastructure services (SkillRunner, CouncilRunner) to send events
/// without a direct dependency on IHubContext.
/// </summary>
internal sealed class AuditRunHubNotifier : IRealtimeNotifier
{
    private readonly IHubContext<AuditRunHub, IAuditRunHubClient> _hubContext;

    public AuditRunHubNotifier(IHubContext<AuditRunHub, IAuditRunHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendToGroupAsync(string group, string method, object payload, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await _hubContext.Clients.Group(group).ReceiveEvent(method, payload);
    }
}
