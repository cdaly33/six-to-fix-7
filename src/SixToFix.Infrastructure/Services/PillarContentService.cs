using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

/// <summary>
/// EF Core implementation of IPillarContentService.
///
/// Tenant isolation: the SixToFixDbContext global query filter already restricts
/// PillarContent rows to the resolved tenant. The explicit WHERE e.TenantId == tenantId
/// clauses in every query provide defence-in-depth and make the contract clear at the call site,
/// matching the pattern used by ClientService.
///
/// Default content seeding: GetAllForTenantAsync/GetForTenantAsync lazily ensure all 6
/// Pillar rows exist and contain meaningful starter content. This keeps left-nav pillar
/// routes useful for new tenants immediately after login.
/// </summary>
public sealed class PillarContentService : IPillarContentService
{
    private static readonly Pillar[] AllPillars =
        (Pillar[])Enum.GetValues(typeof(Pillar));

    private static readonly IReadOnlyDictionary<Pillar, SeedContent> SeedCatalog = new Dictionary<Pillar, SeedContent>
    {
        [Pillar.Brand] = new(
            "Brand Strategy",
            "Build a powerful brand identity and positioning",
            """{"strategy":[{"title":"Define your positioning","points":["Clarify your ideal customer profile and why they choose you","Document your differentiators against top alternatives","Align voice and visual identity to your value proposition"]}],"execution":["Run a brand perception survey with recent clients","Publish a one-page positioning statement for internal teams","Update website headline and service pages to match positioning"],"templates":["Brand Messaging Matrix","Positioning Statement Canvas"],"examples":["Before/after homepage messaging revision","Competitor differentiation table"],"metrics":[["Branded search growth","Month-over-month %"],["Win rate vs. top competitor","Quarterly %"]]}"""),
        [Pillar.Customer] = new(
            "Customer Strategy",
            "Understand audience segments, acquisition priorities, and retention motion",
            """{"strategy":[{"title":"Segment for focus","points":["Group accounts by value, fit, and growth potential","Prioritize the top segments for acquisition and expansion","Define clear customer outcomes per segment"]}],"execution":["Build segment-specific personas with buying triggers","Map lifecycle stages from lead to advocate","Set retention playbooks for top-value cohorts"],"templates":["Customer Segment Prioritization Grid","Lifecycle Journey Map"],"examples":["Segment-level churn review","Persona-driven campaign brief"],"metrics":[["Net revenue retention","Quarterly %"],["Time to first value","Days"]]}"""),
        [Pillar.Offering] = new(
            "Offering Strategy",
            "Package services for clarity, recurring revenue, and easier buying decisions",
            """{"strategy":[{"title":"Productize core services","points":["Package services into clear tiers with outcomes","Create upgrade paths tied to client maturity","Standardize delivery for predictable quality"]}],"execution":["Inventory all services and map to tiered bundles","Define scope boundaries and pricing guardrails","Create SOPs for consistent fulfillment"],"templates":["Service Packaging Worksheet","Scope and Pricing Guardrail Sheet"],"examples":["Tier migration plan","Recurring revenue offer redesign"],"metrics":[["Average revenue per account","Monthly $"],["Recurring revenue mix","% of total revenue"]]}"""),
        [Pillar.Communication] = new(
            "Communication Strategy",
            "Deliver the right message at the right time across all channels",
            """{"strategy":[{"title":"Coordinate the message system","points":["Define core narrative themes for each funnel stage","Ensure channel-specific content supports one unified story","Create feedback loops to improve message-market fit"]}],"execution":["Create a quarterly editorial calendar by funnel stage","Map campaign assets to channel and owner","Run monthly performance review and message iteration"],"templates":["Editorial Calendar Template","Campaign Messaging Brief"],"examples":["Multi-channel launch timeline","Nurture sequence optimization"],"metrics":[["Content-to-meeting conversion","%"],["Email engagement rate","Open/click %"]]}"""),
        [Pillar.Sales] = new(
            "Sales Strategy",
            "Turn demand into predictable revenue with clear process and follow-through",
            """{"strategy":[{"title":"Systematize pipeline movement","points":["Define non-negotiable qualification criteria","Standardize opportunity stages with exit criteria","Align sales and marketing handoff expectations"]}],"execution":["Implement stage definitions and required fields in CRM","Create SLA for lead response and follow-up","Run weekly pipeline inspection with coaching notes"],"templates":["Opportunity Qualification Checklist","Pipeline Review Agenda"],"examples":["Discovery-to-proposal conversion analysis","Lead handoff SLA dashboard"],"metrics":[["Pipeline coverage","x of quota"],["Stage conversion rate","% by stage"]]}"""),
        [Pillar.Management] = new(
            "Management Strategy",
            "Operationalize the framework with ownership, KPIs, and execution rhythm",
            """{"strategy":[{"title":"Create accountable operating cadence","points":["Assign pillar owners with explicit outcomes","Track KPIs that ladder to business goals","Use recurring reviews to remove blockers quickly"]}],"execution":["Define owner scorecards per pillar","Publish a weekly KPI dashboard with clear thresholds","Run monthly strategy reviews with decision logs"],"templates":["Pillar Ownership Scorecard","Monthly Strategy Review Deck"],"examples":["Cross-functional KPI review","Quarterly objective reset"],"metrics":[["Initiative completion rate","% on-time"],["KPI health score","Green/Amber/Red mix"]]}""")
    };

    private readonly SixToFixDbContext _db;
    private readonly ILogger<PillarContentService> _logger;

    public PillarContentService(SixToFixDbContext db, ILogger<PillarContentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PillarContent?> GetForTenantAsync(
        Guid tenantId, Pillar pillar, CancellationToken ct = default)
    {
        await EnsureSeedRowsAsync(tenantId, ct);
        return await _db.PillarContents
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Pillar == pillar, ct);
    }

    /// <summary>
    /// Returns all 6 pillar content rows for the tenant.
    /// Any missing pillars are seeded on-demand with placeholder content before returning.
    /// </summary>
    public async Task<IReadOnlyList<PillarContent>> GetAllForTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        await EnsureSeedRowsAsync(tenantId, ct);
        return await _db.PillarContents
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Pillar)
            .ToListAsync(ct);
    }

    public async Task<PillarContent> UpsertAsync(
        Guid tenantId, Pillar pillar, string bodyJson, Guid updatedByUserId, CancellationToken ct = default)
    {
        var existing = await _db.PillarContents
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Pillar == pillar, ct);

        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.BodyJson = bodyJson;
            existing.UpdatedAt = now;
            existing.UpdatedByUserId = updatedByUserId;
        }
        else
        {
            existing = new PillarContent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Pillar = pillar,
                Title = SeedCatalog[pillar].Title,
                Subtitle = SeedCatalog[pillar].Subtitle,
                BodyJson = bodyJson,
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedByUserId = updatedByUserId
            };
            _db.PillarContents.Add(existing);
        }

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    private async Task EnsureSeedRowsAsync(Guid tenantId, CancellationToken ct)
    {
        var existing = await _db.PillarContents
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.Pillar)
            .ToListAsync(ct);

        var missingPillars = AllPillars.Except(existing).ToList();
        if (missingPillars.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var pillar in missingPillars)
        {
            var seed = SeedCatalog[pillar];
            _db.PillarContents.Add(new PillarContent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Pillar = pillar,
                Title = seed.Title,
                Subtitle = seed.Subtitle,
                BodyJson = seed.BodyJson,
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedByUserId = null
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seeded {Count} default pillar content row(s) for tenant {TenantId}",
            missingPillars.Count, tenantId);
    }

    private sealed record SeedContent(string Title, string Subtitle, string BodyJson);
}
