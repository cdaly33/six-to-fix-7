# Azure OIDC (Workload Identity Federation) Setup Guide — Six-to-Fix

This guide explains how to configure **Azure OIDC (Workload Identity Federation)** so that the GitHub Actions workflow in `deploy-app.yml` can authenticate to Azure **without storing secrets** in the repository or GitHub.

Instead of using long-lived service principal keys, this approach uses **federated credentials** to create a trust relationship between your GitHub repository and an Azure App Registration. GitHub Actions uses OpenID Connect (OIDC) tokens to prove its identity to Azure.

## Prerequisites

- Access to the Azure Portal with permissions to:
  - Create App Registrations in Azure AD / Entra ID
  - Assign roles on resource groups
- Access to GitHub repository settings for `cdaly33/six-to-fix-7`
- The following subscription and resource details:
  - **Azure Subscription ID**: `b1096bba-5e3d-4878-aeed-abdccbe745ef`
  - **Resource Group**: `rg-sixtofix-prod`
  - **App Service Name (prod)**: `app-sixtofix-prod`
  - **App Service Name (dev)**: (to be confirmed; assumed same as prod or existing dev app service)
  - **GitHub Organization**: `cdaly33`
  - **GitHub Repository**: `six-to-fix-7`

## Part 1: Create an App Registration in Azure AD

This App Registration represents your GitHub Actions workflow as an identity in Azure.

### Step 1.1: Navigate to App Registrations

1. Open the **Azure Portal**: https://portal.azure.com
2. In the search bar at the top, type **"App registrations"** and click **App registrations** (under **Services**)
   - Alternatively: **Azure AD** (or **Microsoft Entra ID** in newer portals) → **App registrations**

### Step 1.2: Create a New Registration

3. Click the **New registration** button (blue button at the top)
4. In the **Register an application** form, fill in:
   - **Name**: `github-sixtofix-deploy` (or similar; this identifies the app registration purpose)
   - **Supported account types**: Select **Accounts in this organizational directory only** (default)
   - **Redirect URI**: Leave blank (not needed for OIDC federated credentials)
5. Click **Register**

You will be taken to the app registration's overview page.

### Step 1.3: Note the Client ID and Tenant ID

6. On the **Overview** page, note down these two values — **you will need them later**:
   - **Application (client) ID** — copy this value, paste it somewhere safe
   - **Directory (tenant) ID** — copy this value, paste it somewhere safe

Example (values shown are placeholders):
```
Application (client) ID: 12345678-1234-1234-1234-123456789012
Directory (tenant) ID):   87654321-4321-4321-4321-210987654321
```

## Part 2: Add Federated Credentials (OIDC)

Federated credentials establish trust between GitHub and Azure. You will create **two** separate credentials:
1. **One for the prod environment** (manual `workflow_dispatch` deploy)
2. **One for the main branch** (automatic deploy on push to main)

### Step 2.1: Navigate to Federated Credentials

7. In the left sidebar of the app registration, click **Certificates & secrets**
8. Select the **Federated credentials** tab (third tab; may be labeled "Federated credentials" or "Credentials")
9. Click the **Add credential** button (blue button)

### Step 2.2: Create the Prod Environment Federated Credential

A federated credential for the **prod** GitHub environment (requires manual `workflow_dispatch` trigger):

10. Fill in the **Add a credential** form:
    - **Federated credential scenario**: Select **GitHub Actions deploying Azure resources**
    - **Organization name**: `cdaly33`
    - **Repository name**: `six-to-fix-7`
    - **Entity type**: Select **Environment**
    - **GitHub environment name**: `prod`
    - **Name**: `github-sixtofix-prod-env` (optional; helps you identify this credential later)

11. Click **Add**

The credential will be created. You should see it listed under "Federated credentials."

### Step 2.3: Create the Main Branch Federated Credential

Another federated credential for the **main branch** (automatic deploy on push):

12. Click **Add credential** again
13. Fill in the second credential:
    - **Federated credential scenario**: Select **GitHub Actions deploying Azure resources**
    - **Organization name**: `cdaly33`
    - **Repository name**: `six-to-fix-7`
    - **Entity type**: Select **Branch**
    - **Branch name**: `main`
    - **Name**: `github-sixtofix-main-branch` (optional; helps you identify this credential)

14. Click **Add**

You should now see **two federated credentials** listed on the **Federated credentials** tab.

**Gotcha:** The entity type and values must match **exactly** what GitHub Actions sends. If you mistype the branch name, organization, or environment name, the OIDC token will not be accepted.

## Part 3: Grant the App Registration Permissions on Azure

The App Registration needs permission to deploy to the resource group and App Service.

### Step 3.1: Navigate to the Resource Group

15. In the Azure Portal search bar, type **`rg-sixtofix-prod`** and click on the resource group that appears
    - Alternatively: Home → Resource groups → `rg-sixtofix-prod`

### Step 3.2: Add a Role Assignment

16. In the left sidebar, click **Access control (IAM)**
17. Click the **Add** button and select **Add role assignment**
18. In the **Add role assignment** panel:
    - **Role**: Search for and select **Contributor**
      - (Contributor allows the workflow to deploy and update App Services; if you prefer least-privilege, you can use "Website Contributor" instead, but Contributor is simpler)
    - Click **Next**

### Step 3.3: Assign the Role to Your App Registration

19. On the **Members** tab, under **Assign access to**, select **User, group, or service principal**
20. Click **Select members**
21. In the search box that appears, type `github-sixtofix-deploy` (the name of the app registration you created)
22. Click on the app registration name when it appears in the results
23. Click the **Select** button to confirm
24. Click **Next** (then **Review + assign** if prompted)
25. Click **Create** or **Assign** to finalize

The app registration is now granted **Contributor** permissions on the resource group.

## Part 4: Add GitHub Repository Secrets

These secrets store the values needed by the workflow to authenticate via OIDC.

### Step 4.1: Navigate to GitHub Repository Secrets

26. Go to your GitHub repository: https://github.com/cdaly33/six-to-fix-7
27. Click **Settings** (top navigation bar)
28. In the left sidebar, click **Secrets and variables** → **Actions**

### Step 4.2: Add the AZURE_CLIENT_ID Secret

29. Click the **New repository secret** button
30. **Name**: `AZURE_CLIENT_ID`
31. **Value**: Paste the **Application (client) ID** from Part 1.3
32. Click **Add secret**

### Step 4.3: Add the AZURE_TENANT_ID Secret

33. Click **New repository secret** again
34. **Name**: `AZURE_TENANT_ID`
35. **Value**: Paste the **Directory (tenant) ID** from Part 1.3
36. Click **Add secret**

### Step 4.4: Add the AZURE_SUBSCRIPTION_ID Secret

37. Click **New repository secret** again
38. **Name**: `AZURE_SUBSCRIPTION_ID`
39. **Value**: `b1096bba-5e3d-4878-aeed-abdccbe745ef` (copy exactly)
40. Click **Add secret**

### Step 4.5: Add the AZURE_WEBAPP_NAME_PROD Secret

41. Click **New repository secret** again
42. **Name**: `AZURE_WEBAPP_NAME_PROD`
43. **Value**: `app-sixtofix-prod` (copy exactly)
44. Click **Add secret**

### Step 4.6: Add the AZURE_WEBAPP_NAME_DEV Secret

45. Click **New repository secret** again
46. **Name**: `AZURE_WEBAPP_NAME_DEV`
47. **Value**: (ask your team or check if you have a dev app service name; if not, use `app-sixtofix-dev` as a placeholder or same as prod)
48. Click **Add secret**

You should now have **5 secrets** visible on the Actions secrets page:
- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_WEBAPP_NAME_PROD`
- `AZURE_WEBAPP_NAME_DEV`

## Part 5: Create GitHub Environments

Environments provide isolation and protection rules for different deployment targets (prod vs. dev).

### Step 5.1: Create the Prod Environment

49. In the GitHub repository, go to **Settings** → **Environments** (left sidebar)
50. Click **New environment**
51. **Environment name**: `prod`
52. Click **Configure environment**

### Step 5.2: Add Prod Protection Rules

53. Under **Deployment branches and tags**, select **Protected branches**
    - Add a restriction so only the `main` branch can deploy to prod (optional; enforces best practices)
54. Under **Required reviewers**, check the box and add yourself (or your team member who should approve prod deploys)
    - This ensures **every prod deployment requires manual approval**
55. Click **Save protection rules**

### Step 5.3: Create the Dev Environment

56. Click **New environment** again
57. **Environment name**: `dev`
58. Click **Configure environment**
59. Leave all protection rules **unchecked** (dev deploys automatically on push to main)
60. Click **Save protection rules** (or the environment will be created with no rules)

You should now have **two environments** visible: `prod` and `dev`.

## Part 6: Verify the Workflow Configuration

The `deploy-app.yml` workflow should already use these secrets and environments. Here's what to look for:

### Expected Workflow Structure

The workflow should have two jobs:

1. **Deploy to Dev** — triggered on push to `main`, runs automatically:
   ```yaml
   deploy-dev:
     runs-on: ubuntu-latest
     environment: dev
     permissions:
       id-token: write
       contents: read
   ```

2. **Deploy to Prod** — triggered by manual `workflow_dispatch`, runs on approval:
   ```yaml
   deploy-prod:
     runs-on: ubuntu-latest
     environment: prod
     permissions:
       id-token: write
       contents: read
   ```

Both should use the `azure/login` action with OIDC:
```yaml
- uses: azure/login@v1
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

If your workflow differs, update it to match this structure.

## Part 7: Test the Workflow

### Step 7.1: Test Dev Deploy (Automatic)

61. Create a test branch and push a small change to trigger the dev deploy:
    ```powershell
    git checkout -b test/oidc-setup
    echo "# OIDC Test" >> README.md
    git add README.md
    git commit -m "test: oidc setup"
    git push -u origin test/oidc-setup
    ```

62. Create a pull request to merge into `main`
63. Once approved and merged to `main`, the workflow should trigger automatically
64. Go to **Actions** in GitHub and monitor the **Deploy Application** (or similar) workflow
    - It should run the `deploy-dev` job
    - If successful, the action shows ✅ green checkmark
    - If it fails, check the logs for error messages (likely OIDC token or permissions issues)

### Step 7.2: Test Prod Deploy (Manual)

65. Go to **Actions** in GitHub
66. Click on the **Deploy Application** (or similar) workflow name
67. Click **Run workflow** (blue button on the right)
68. Select **Branch: main** and **Environment: prod** (if prompted)
69. Click **Run workflow**
70. A notification will appear asking for approval (because of the required reviewer rule)
71. Click the notification or go back to **Actions** and approve the pending deployment
72. The workflow should run and deploy to prod
    - If successful, ✅ green checkmark
    - If it fails, check logs

### Step 7.3: Verify the Deployment

73. In the Azure Portal, go to the **app-sixtofix-prod** App Service
    - Click **Overview**
    - Look at the **Deployments** section to confirm the latest deployment timestamp matches when you ran the workflow
    - Or use **SSH** to connect to the app and verify code changes

## Troubleshooting

### "AADSTS700016: Application not found in directory" error

- **Cause**: The federated credential subject does not match what GitHub is sending.
- **Fix**: Double-check the federated credential settings (organization, repository, entity type, branch/environment name). Re-check against Part 2.

### "Insufficient privileges to complete the operation" error

- **Cause**: The App Registration does not have Contributor role on the resource group.
- **Fix**: Follow Part 3 again to add the role assignment. Wait a few minutes for Azure to propagate the role.

### "Resource group or app service not found" error

- **Cause**: The secrets store incorrect subscription ID, resource group name, or app service name.
- **Fix**: Verify all secrets match the values in Part 4 and Part 1 (Azure Portal). Check spelling and exact values.

### Workflow runs but shows "No deployments to Azure"

- **Cause**: The App Service deployment command is missing or incorrect in the workflow.
- **Fix**: Ensure the workflow includes an Azure App Service Deploy step with correct app service name and artifact path.

### "Permission denied" when GitHub tries to authenticate

- **Cause**: OIDC token validation failed; federated credential configuration is incorrect.
- **Fix**: Go back to Part 2 and verify each federated credential line-by-line. Test with a manual curl to validate the token (advanced debugging).

## Next Steps

- Monitor the workflow runs and address any errors in logs.
- Once confirmed working, consider updating documentation and removing any old secrets from the repository.
- For future deployments, use the **Actions → Deploy Application → Run workflow** method or merge to `main` to trigger automatic dev deploy.

---

**Last Updated**: May 2026  
**Related Documentation**: See [AZURE-DEPLOYMENT-GUIDE.md](./AZURE-DEPLOYMENT-GUIDE.md) for initial infrastructure setup.
