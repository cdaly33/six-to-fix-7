using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using SixToFix.Application.Models;
using SixToFix.Application.Services;

namespace SixToFix.Infrastructure.BackgroundServices;

public sealed class HubSpotWorker : BackgroundService
{
    private readonly Channel<HubSpotEvent> _channel;
    private readonly IHubSpotClient _hubSpotClient;
    private readonly ILogger<HubSpotWorker> _logger;

    public HubSpotWorker(
        Channel<HubSpotEvent> channel,
        IHubSpotClient hubSpotClient,
        ILogger<HubSpotWorker> logger)
    {
        _channel = channel;
        _hubSpotClient = hubSpotClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var hubSpotEvent in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Use the stored HubSpot company ID when available (audit-publish path);
                // fall back to ClientSlug for inbound webhook echo events.
                var companyId = hubSpotEvent.HubSpotCompanyId ?? hubSpotEvent.ClientSlug;

                await _hubSpotClient.UpdateAuditResultAsync(
                    companyId,
                    hubSpotEvent.Tier,
                    hubSpotEvent.CompositeScore,
                    hubSpotEvent.Scores,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HubSpot sync failed for audit run {AuditRunId}", hubSpotEvent.AuditRunId);
            }
        }
    }
}
