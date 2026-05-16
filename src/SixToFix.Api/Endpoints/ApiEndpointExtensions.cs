using System.Security.Claims;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SixToFix.Api.Models;
using SixToFix.Application.Auth;
using SixToFix.Application.Exceptions;
using SixToFix.Application.Models;
using SixToFix.Application.Services;

namespace SixToFix.Api.Endpoints;

public static class ApiEndpointExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        // Health check
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
            .AllowAnonymous()
            .WithName("HealthCheck");

        MapAuthEndpoints(app);
        MapAuditRunEndpoints(app);
        MapReviewerEndpoints(app);
        MapPublishingEndpoints(app);
        MapCalibrationEndpoints(app);
        MapWebhookEndpoints(app);

        return app;
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    private static void MapAuthEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            IAuthService authService,
            ILogger<LoginRequest> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.Problem("Email and password are required.", statusCode: 400);

            var result = await authService.LoginAsync(request.Email, request.Password, ct);
            if (result is null)
            {
                logger.LogWarning("Failed login attempt for email hash {EmailHash}", request.Email.GetHashCode());
                return Results.Problem("Invalid credentials.", statusCode: 401);
            }

            return Results.Ok(new LoginResponse(result.AccessToken, result.Email, result.UserId, result.TenantId, result.Roles));
        })
        .AllowAnonymous()
        .WithName("Login");

        app.MapPost("/api/auth/refresh", async (
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Results.Problem("Invalid token.", statusCode: 401);

            var result = await authService.ReissueTokenAsync(userId, ct);
            if (result is null)
                return Results.Problem("User not found or inactive.", statusCode: 401);

            return Results.Ok(new LoginResponse(result.AccessToken, result.Email, result.UserId, result.TenantId, result.Roles));
        })
        .RequireAuthorization()
        .WithName("RefreshToken");
    }

    // ── Audit Runs ────────────────────────────────────────────────────────────

    private static void MapAuditRunEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/audit-runs", async (
            CreateAuditRunRequest request,
            HttpContext httpContext,
            IAuditOrchestrator orchestrator,
            ILogger<IAuditOrchestrator> logger,
            CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            if (userId == Guid.Empty)
                return Results.Problem("User identity missing.", statusCode: 401);

            if (!VerifyTenantOwnership(httpContext, null))
                return Results.Problem("Access denied.", statusCode: 403);

            try
            {
                var run = await orchestrator.CreateAuditRunAsync(request.ClientId, userId, ct);
                return Results.Created($"/api/audit-runs/{run.Id}", run);
            }
            catch (ClientNotFoundException ex)
            {
                logger.LogWarning("Client {ClientId} not found when creating audit run", ex.ClientId);
                return Results.Problem($"Client '{ex.ClientId}' not found.", statusCode: 404);
            }
            catch (AuditRunConflictException ex)
            {
                logger.LogWarning("Audit run conflict for client {ClientId}", ex.ClientId);
                return Results.Problem($"An active audit run already exists for client '{ex.ClientId}'.", statusCode: 409);
            }
        })
        .RequireAuthorization("TenantAdmin")
        .WithName("CreateAuditRun");

        app.MapPost("/api/audit-runs/{id:guid}/start", async (
            Guid id,
            HttpContext httpContext,
            IAuditOrchestrator orchestrator,
            ILogger<IAuditOrchestrator> logger,
            CancellationToken ct) =>
        {
            try
            {
                await orchestrator.StartAuditRunAsync(id, ct);
                return Results.Ok(new { auditRunId = id, status = "started" });
            }
            catch (AuditRunNotFoundException ex)
            {
                logger.LogWarning("AuditRun {AuditRunId} not found on start", ex.AuditRunId);
                return Results.Problem($"AuditRun '{ex.AuditRunId}' not found.", statusCode: 404);
            }
            catch (InvalidAuditRunStateException ex)
            {
                logger.LogWarning("AuditRun {AuditRunId} in invalid state: {State}", id, ex.CurrentState);
                return Results.Problem(ex.Message, statusCode: 422);
            }
        })
        .RequireAuthorization("TenantAdmin")
        .WithName("StartAuditRun");

        app.MapGet("/api/audit-runs/{id:guid}", async (
            Guid id,
            IAuditOrchestrator orchestrator,
            ILogger<IAuditOrchestrator> logger,
            CancellationToken ct) =>
        {
            try
            {
                var run = await orchestrator.GetAuditRunAsync(id, ct);
                return Results.Ok(run);
            }
            catch (AuditRunNotFoundException ex)
            {
                logger.LogWarning("AuditRun {AuditRunId} not found", ex.AuditRunId);
                return Results.Problem($"AuditRun '{ex.AuditRunId}' not found.", statusCode: 404);
            }
        })
        .RequireAuthorization()
        .WithName("GetAuditRun");

        app.MapGet("/api/audit-runs/{id:guid}/status", async (
            Guid id,
            IAuditOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var status = await orchestrator.GetAuditRunStatusAsync(id, ct);
            return status is null ? Results.NotFound() : Results.Ok(status);
        })
        .RequireAuthorization()
        .WithName("GetAuditRunStatus");

        app.MapGet("/api/audit-runs", async (
            Guid clientId,
            IAuditOrchestrator orchestrator,
            ILogger<IAuditOrchestrator> logger,
            CancellationToken ct) =>
        {
            try
            {
                var runs = await orchestrator.GetAuditRunsForClientAsync(clientId, ct);
                return Results.Ok(runs);
            }
            catch (ClientNotFoundException ex)
            {
                logger.LogWarning("Client {ClientId} not found when listing audit runs", ex.ClientId);
                return Results.Problem($"Client '{ex.ClientId}' not found.", statusCode: 404);
            }
        })
        .RequireAuthorization()
        .WithName("GetAuditRunsForClient");
    }

    // ── Reviewer Workflow ──────────────────────────────────────────────────────

    private static void MapReviewerEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/audit-runs/{auditRunId:guid}/categories/{categoryId:guid}/approve", async (
            Guid auditRunId,
            Guid categoryId,
            HttpContext httpContext,
            IReviewerWorkflow workflow,
            ILogger<IReviewerWorkflow> logger,
            CancellationToken ct) =>
        {
            var reviewerId = GetUserId(httpContext);
            if (reviewerId == Guid.Empty)
                return Results.Problem("User identity missing.", statusCode: 401);

            try
            {
                await workflow.ApproveAsync(auditRunId, categoryId, reviewerId, ct);
                return Results.Ok(new { auditRunId, categoryId, status = "approved" });
            }
            catch (AuditRunNotFoundException ex)
            {
                logger.LogWarning("AuditRun {AuditRunId} not found on approve", ex.AuditRunId);
                return Results.Problem($"AuditRun '{ex.AuditRunId}' not found.", statusCode: 404);
            }
            catch (InvalidAuditRunStateException ex)
            {
                logger.LogWarning("Invalid state on approve for AuditRun {AuditRunId}: {State}", auditRunId, ex.CurrentState);
                return Results.Problem(ex.Message, statusCode: 422);
            }
        })
        .RequireAuthorization("Reviewer")
        .WithName("ApproveCategory");

        app.MapPost("/api/audit-runs/{auditRunId:guid}/categories/{categoryId:guid}/reject", async (
            Guid auditRunId,
            Guid categoryId,
            RejectRequest request,
            HttpContext httpContext,
            IReviewerWorkflow workflow,
            ILogger<IReviewerWorkflow> logger,
            CancellationToken ct) =>
        {
            var reviewerId = GetUserId(httpContext);
            if (reviewerId == Guid.Empty)
                return Results.Problem("User identity missing.", statusCode: 401);

            try
            {
                await workflow.RejectAsync(auditRunId, categoryId, reviewerId, request.Reason, ct);
                return Results.Ok(new { auditRunId, categoryId, status = "rejected" });
            }
            catch (ReviewerLockoutException ex)
            {
                logger.LogWarning(
                    "Reviewer lockout triggered for AuditRun {AuditRunId}, Category {CategoryId}",
                    auditRunId, categoryId);
                return Results.Json(
                    new { code = "REVIEWER_REJECTION_LOCKOUT", lockoutExpiresAt = ex.LockoutStatus.LockoutExpiresAt },
                    statusCode: 409);
            }
            catch (AuditRunNotFoundException ex)
            {
                logger.LogWarning("AuditRun {AuditRunId} not found on reject", ex.AuditRunId);
                return Results.Problem($"AuditRun '{ex.AuditRunId}' not found.", statusCode: 404);
            }
            catch (InvalidAuditRunStateException ex)
            {
                logger.LogWarning("Invalid state on reject for AuditRun {AuditRunId}: {State}", auditRunId, ex.CurrentState);
                return Results.Problem(ex.Message, statusCode: 422);
            }
        })
        .RequireAuthorization("Reviewer")
        .WithName("RejectCategory");
    }

    // ── Publishing ────────────────────────────────────────────────────────────

    private static void MapPublishingEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/audit-runs/{auditRunId:guid}/publish", async (
            Guid auditRunId,
            HttpContext httpContext,
            IPublisher publisher,
            ILogger<IPublisher> logger,
            CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            if (userId == Guid.Empty)
                return Results.Problem("User identity missing.", statusCode: 401);

            try
            {
                var result = await publisher.PublishAuditAsync(auditRunId, userId, ct);
                return Results.Ok(result);
            }
            catch (AuditRunNotFoundException ex)
            {
                logger.LogWarning("AuditRun {AuditRunId} not found on publish", ex.AuditRunId);
                return Results.Problem($"AuditRun '{ex.AuditRunId}' not found.", statusCode: 404);
            }
            catch (InvalidAuditRunStateException ex)
            {
                logger.LogWarning("Invalid state on publish for AuditRun {AuditRunId}: {State}", auditRunId, ex.CurrentState);
                return Results.Problem(ex.Message, statusCode: 422);
            }
            catch (NotAllCategoriesApprovedException ex)
            {
                logger.LogWarning(
                    "Publish precondition failed: {Approved}/{Required} categories approved for AuditRun {AuditRunId}",
                    ex.ApprovedCount, ex.RequiredCount, auditRunId);
                return Results.Problem(ex.Message, statusCode: 422);
            }
            catch (AuditAlreadyPublishedException)
            {
                logger.LogWarning("AuditRun {AuditRunId} is already published", auditRunId);
                return Results.Problem("This audit has already been published.", statusCode: 409);
            }
        })
        .RequireAuthorization("TenantAdmin")
        .WithName("PublishAuditRun");

        app.MapGet("/api/published/{auditRunId:guid}", async (
            Guid auditRunId,
            IPublisher publisher,
            ILogger<IPublisher> logger,
            CancellationToken ct) =>
        {
            try
            {
                var summary = await publisher.GetPublishedAuditByRunIdAsync(auditRunId, ct);
                return Results.Ok(summary);
            }
            catch (AuditRunNotFoundException ex)
            {
                logger.LogWarning("Published AuditRun {AuditRunId} not found", ex.AuditRunId);
                return Results.Problem($"AuditRun '{ex.AuditRunId}' not found or not yet published.", statusCode: 404);
            }
        })
        .RequireAuthorization()
        .WithName("GetPublishedAudit");
    }

    // ── Calibration ───────────────────────────────────────────────────────────

    private static void MapCalibrationEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/clients/{clientId:guid}/calibration", async (
            Guid clientId,
            ICalibrationTracker calibrationTracker,
            ILogger<ICalibrationTracker> logger,
            CancellationToken ct) =>
        {
            try
            {
                var history = await calibrationTracker.GetCalibrationHistoryAsync(clientId, ct);
                return Results.Ok(history);
            }
            catch (ClientNotFoundException ex)
            {
                logger.LogWarning("Client {ClientId} not found on calibration history request", ex.ClientId);
                return Results.Problem($"Client '{ex.ClientId}' not found.", statusCode: 404);
            }
        })
        .RequireAuthorization()
        .WithName("GetCalibrationHistory");
    }

    // ── HubSpot Webhook ───────────────────────────────────────────────────────

    private static void MapWebhookEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/hubspot", async (
            HttpContext httpContext,
            IHubSpotClient hubSpotClient,
            Channel<HubSpotEvent> channel,
            ILogger<IHubSpotClient> logger,
            CancellationToken ct) =>
        {
            httpContext.Request.EnableBuffering();

            string body;
            using (var reader = new System.IO.StreamReader(
                httpContext.Request.Body,
                leaveOpen: true))
            {
                body = await reader.ReadToEndAsync(ct);
                httpContext.Request.Body.Position = 0;
            }

            var signature = httpContext.Request.Headers["X-HubSpot-Signature"].FirstOrDefault() ?? string.Empty;

            var valid = await hubSpotClient.ValidateWebhookSignatureAsync(signature, body, ct);
            if (!valid)
            {
                logger.LogWarning("HubSpot webhook HMAC validation failed");
                return Results.Problem("Invalid webhook signature.", statusCode: 401);
            }

            HubSpotWebhookPayload? payload;
            try
            {
                payload = System.Text.Json.JsonSerializer.Deserialize<HubSpotWebhookPayload>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (System.Text.Json.JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize HubSpot webhook payload");
                return Results.Problem("Invalid webhook payload.", statusCode: 400);
            }

            if (payload is null)
                return Results.Problem("Empty webhook payload.", statusCode: 400);

            var evt = payload.PublishedAt.HasValue
                ? new HubSpotEvent(payload.AuditRunId, payload.ClientSlug, payload.Tier, payload.CompositeScore, payload.PublishedAt.Value)
                : new HubSpotEvent(payload.AuditRunId, payload.ClientSlug, payload.Tier, payload.CompositeScore);

            await channel.Writer.WriteAsync(evt, ct);

            logger.LogInformation("HubSpot webhook received and enqueued for AuditRun {AuditRunId}", payload.AuditRunId);
            return Results.Accepted();
        })
        .AllowAnonymous()
        .WithName("HubSpotWebhook");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Guid GetUserId(HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Returns true when the caller's tenant_id claim matches the expected tenantId,
    /// or when tenantId is null (no resource-level check required at call site).
    /// SuperAdmin can access any tenant.
    /// </summary>
    private static bool VerifyTenantOwnership(HttpContext httpContext, Guid? tenantId)
    {
        if (httpContext.User.IsInRole("SuperAdmin"))
            return true;

        if (tenantId is null)
            return true;

        var tenantClaim = httpContext.User.FindFirstValue("tenant_id");
        return Guid.TryParse(tenantClaim, out var callerTenant) && callerTenant == tenantId.Value;
    }
}

