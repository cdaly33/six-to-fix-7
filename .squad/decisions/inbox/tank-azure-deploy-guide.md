# Tank — Azure Deployment Guide

## Context
Chris requested a comprehensive Azure deployment guide with two parallel paths:

1. Primary path: Azure Portal web UI with extremely detailed click-by-click instructions.
2. Secondary path: Bicep CLI with explicit parameter guidance.

The deployment flow must remain manual. GitHub Actions must not be presented as the deployment mechanism for Azure infrastructure or app publish.

## Decisions / Notes

- Treat the Azure Portal walkthrough as the primary onboarding path for non-expert Azure users.
- Treat Bicep as the alternative path for repeatable infra creation via `az deployment group create`.
- App deployment guidance should use ZIP deploy only (`az webapp deploy` or portal ZIP/package deployment).
- Keep manual post-Bicep steps explicit: missing Key Vault secrets, `sf_app` user creation, EF migrations, ZIP deploy, and verification.
- Document the actual repo behavior for migrations: EF tooling currently reads `DESIGN_TIME_CONNECTION_STRING` at design time.
- Call out the secret-name mapping clearly: App Service settings use `__`, Key Vault uses `--`.
- Call out that `HubSpot--WebhookSecret` does not need its own App Service setting when `KeyVault__Uri` is present because the app loads the full vault through `AddAzureKeyVault`.
