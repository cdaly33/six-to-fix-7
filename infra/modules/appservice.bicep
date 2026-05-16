param location string
param environment string
param appName string
param appInsightsConnectionString string
param keyVaultName string
param keyVaultUri string
param blobEndpoint string
param searchEndpoint string

var isProd = environment == 'prod'
var nameSuffix = '${appName}-${environment}'

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'asp-${nameSuffix}'
  location: location
  kind: 'app,linux'
  sku: {
    name: isProd ? 'P2v3' : 'B2'
    tier: isProd ? 'PremiumV3' : 'Basic'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: 'app-${nameSuffix}'
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      webSocketsEnabled: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
    }
  }
}

resource appSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: webApp
  name: 'appsettings'
  properties: {
    ASPNETCORE_ENVIRONMENT: isProd ? 'Production' : 'Development'
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
    KeyVault__Uri: keyVaultUri
    Search__Endpoint: searchEndpoint
    Storage__BlobEndpoint: blobEndpoint
    ConnectionStrings__DefaultConnection: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=ConnectionStrings--DefaultConnection)'
    ConnectionStrings__AdminConnection: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=ConnectionStrings--AdminConnection)'
    Jwt__SigningKey: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=Jwt--SigningKey)'
    HubSpot__PrivateAppToken: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=HubSpot--PrivateAppToken)'
    AzureOpenAI__ApiKey: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=AzureOpenAI--ApiKey)'
    WEBSITE_RUN_FROM_PACKAGE: '1'
  }
}

output hostName string = webApp.properties.defaultHostName
output id string = webApp.id
output name string = webApp.name
output principalId string = webApp.identity.principalId
