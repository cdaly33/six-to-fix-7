using Microsoft.AspNetCore.SignalR;
using SixToFix.Application.Hubs;

namespace SixToFix.Infrastructure.Hubs;

public sealed class AuditRunHub : Hub<IAuditRunHubClient>
{
}
