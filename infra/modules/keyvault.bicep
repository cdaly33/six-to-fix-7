param location string
param environment string
param appName string
param tenantId string
@secure()
param secrets object = {}

var isProd = environment == 'prod'
var nameSuffix = '${appName}-${environment}'

resource keyVault 'Microsoft.KeyVault/vaults@2024-04-01-preview' = {
  name: 'kv-${nameSuffix}'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: isProd ? 90 : 7
    enablePurgeProtection: isProd
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

resource vaultSecrets 'Microsoft.KeyVault/vaults/secrets@2024-04-01-preview' = [for secret in items(secrets): {
  parent: keyVault
  name: secret.key
  properties: {
    value: string(secret.value)
  }
}]

output id string = keyVault.id
output name string = keyVault.name
output uri string = keyVault.properties.vaultUri
