@description('Azure region')
param location string

@description('Resource prefix (e.g., strategicglue-dev)')
param resourcePrefix string

@description('Whether this is a production deployment')
param isProd bool

@description('Key Vault URI for secret references')
param keyVaultUri string

@description('Application Insights connection string')
param applicationInsightsConnectionString string

// SKUs per environment-contract.md §4: dev B2 Linux, prod P2v3 Linux
var planSku = isProd
  ? { name: 'P2v3', tier: 'PremiumV3', capacity: 1 }
  : { name: 'B2', tier: 'Basic', capacity: 1 }

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'asp-${resourcePrefix}'
  location: location
  kind: 'app,linux'
  sku: planSku
  properties: {
    reserved: true  // Linux
  }
}

resource appService 'Microsoft.Web/sites@2024-04-01' = {
  name: 'app-${resourcePrefix}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    // ARR Affinity REQUIRED for SignalR Blazor Server circuits — per environment-contract.md §6
    clientAffinityEnabled: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      // Prod: TLS 1.3 per environment-contract.md §4
      minTlsVersion: isProd ? '1.3' : '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: isProd ? 'Production' : 'Development'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        // Key Vault URI — app reads all secrets from KV at startup via AddAzureKeyVault
        {
          name: 'Azure__KeyVaultUri'
          value: keyVaultUri
        }
        // Secrets via Key Vault references (managed identity auth)
        // See environment-contract.md §1.2 for Key Vault reference syntax
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/sf-pgbouncer-connstr/)'
        }
        {
          name: 'Jwt__SigningKey'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/sf-jwt-signing-key/)'
        }
        {
          name: 'HubSpot__PrivateAppToken'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/sf-hubspot-private-app-token/)'
        }
        {
          name: 'HubSpot__WebhookSecret'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/sf-hubspot-webhook-secret/)'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

output principalId string = appService.identity.principalId
output hostName string = appService.properties.defaultHostName
output appServiceName string = appService.name
