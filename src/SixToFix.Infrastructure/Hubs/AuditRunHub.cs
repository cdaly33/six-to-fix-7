using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SixToFix.Application.Hubs;

namespace SixToFix.Infrastructure.Hubs;

[Authorize]
public sealed class AuditRunHub : Hub<IAuditRunHubClient>
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
