namespace SixToFix.Application.Services;

public interface IRealtimeNotifier
{
    Task SendToGroupAsync(string group, string method, object payload, CancellationToken ct = default);
}
