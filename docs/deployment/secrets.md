# Deployment Secrets

## Required GitHub Secrets

| Secret | Purpose | Where to find it |
| --- | --- | --- |
| `AZURE_CLIENT_ID` | OIDC-enabled app registration / service principal client ID used by GitHub Actions. | Azure portal → Microsoft Entra ID → App registrations → the deployment app → **Application (client) ID**. |
| `AZURE_TENANT_ID` | Tenant ID for the StrategicGlue Azure directory. | Azure portal → Microsoft Entra ID → **Tenant ID**. |
| `AZURE_SUBSCRIPTION_ID` | Subscription that hosts `rg-StrategicGlue-CommandCenter`. | Azure portal → Subscriptions → target subscription → **Subscription ID**. |
| `AZURE_WEBAPP_NAME_DEV` | Dev App Service name for `deploy-app.yml`. | Azure portal → App Services → dev app → **Name**. |
| `AZURE_WEBAPP_NAME_PROD` | Prod App Service name for `deploy-app.yml`. | Azure portal → App Services → prod app → **Name**. |
| `PLAYWRIGHT_BASE_URL` | Base URL used by main-branch Playwright runs. | The deployed dev site URL, usually `https://<dev-app-name>.azurewebsites.net`. |
| `E2E_USERNAME` | Seeded E2E login for Playwright. | Seeded test user created in the target environment. |
| `E2E_PASSWORD` | Password for the seeded E2E login. | Secret chosen when seeding the E2E user. |

## How to configure OIDC federation

1. Create or reuse an Azure app registration dedicated to GitHub Actions deployments.
2. In **Certificates & secrets** do **not** create a client secret; GitHub uses OpenID Connect instead.
3. Open **Federated credentials** and add one credential per workflow trust boundary:
   - `repo:cdaly33/six-to-fix-7:ref:refs/heads/main` for automatic dev deployments.
   - `repo:cdaly33/six-to-fix-7:environment:prod` for manually approved prod deployments.
4. Grant the app registration enough Azure RBAC on `rg-StrategicGlue-CommandCenter` to deploy Bicep and App Service content:
   - `Contributor` on the resource group.
   - `User Access Administrator` only if the workflow must create RBAC assignments.
5. Add the GitHub secrets in **Repository settings → Secrets and variables → Actions**.
6. Run `deploy-infra.yml` manually once for `dev` to validate the trust relationship before relying on automatic pushes.

## Notes

- `deploy-infra.yml` and `deploy-app.yml` both require `permissions: id-token: write` so the OIDC token can be exchanged by `azure/login@v2`.
- Keep environment-specific names in GitHub secrets rather than hard-coding them in workflows.
- Rotate seeded E2E credentials when the shared dev environment changes hands.
