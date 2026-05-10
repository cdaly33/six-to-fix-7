using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SixToFix.Web.Hubs;

/// <summary>
/// Real-time hub for audit run progress events.
/// Clients join group by auditRunId.
/// Events: skill_started, skill_completed, skill_failed, council_started,
///         council_completed, audit_completed, audit_failed.
/// ARR Affinity required (see ADR-001).
/// </summary>
[Authorize]
public sealed class AuditRunHub : Hub
{
    public async Task JoinAuditRun(string auditRunId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, auditRunId);
    }

    public async Task LeaveAuditRun(string auditRunId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, auditRunId);
    }
}
