namespace SixToFix.Application.Hubs;

public interface IAuditRunHubClient
{
    Task ReceiveEvent(string eventType, object payload);
}
