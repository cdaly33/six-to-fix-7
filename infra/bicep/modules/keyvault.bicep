@description('Azure region')
param location string

@description('Resource prefix')
param resourcePrefix string

@description('Whether this is production (enables purge protection)')
param isProd bool

resource keyVault 'Microsoft.KeyVault/vaults@2024-04-01-preview' = {
  name: 'kv-${resourcePrefix}'
  location: location
  properties: {
    sku: {
      family: 'A'
      // Prod uses Premium for HSM-backed keys per environment-contract.md §4
      name: isProd ? 'premium' : 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true    // RBAC model (not access policies) — per managed-identity-wiring.md
    enableSoftDelete: true
    softDeleteRetentionInDays: isProd ? 90 : 7
    enablePurgeProtection: isProd    // Purge protection for prod only
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Secrets expected in Key Vault (per environment-contract.md §2):
// sf-pgbouncer-connstr         — runtime DB connection (sf_app, port 6432, No Reset On Close=true)
// sf-jwt-signing-key           — JWT HMAC signing key (≥32 chars)
// sf-openai-api-key            — Azure OpenAI key (fallback if managed identity fails)
// sf-hubspot-private-app-token — HubSpot Private App token (Bearer auth)
// sf-hubspot-webhook-secret    — HubSpot inbound webhook HMAC-SHA256 secret
// sf-blob-storage-connstr      — Azure Blob Storage connection string (fallback / local-dev override)

output keyVaultUri string = keyVault.properties.vaultUri
output keyVaultName string = keyVault.name
