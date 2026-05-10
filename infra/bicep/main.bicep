@description('Deployment environment')
@allowed(['dev', 'prod'])
param environment string = 'dev'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Application name prefix for resource naming')
param appName string = 'strategicglue'

var resourcePrefix = '${appName}-${environment}'
var isProd = environment == 'prod'

// Application Insights + Log Analytics
module monitoring 'modules/appinsights.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    resourcePrefix: resourcePrefix
    retentionDays: isProd ? 90 : 30
  }
}

// Key Vault
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    resourcePrefix: resourcePrefix
    isProd: isProd
  }
}

// Storage
module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    resourcePrefix: resourcePrefix
    isProd: isProd
  }
}

// AI Search
module search 'modules/search.bicep' = {
  name: 'search'
  params: {
    location: location
    resourcePrefix: resourcePrefix
    isProd: isProd
  }
}

// Azure OpenAI
module openai 'modules/openai.bicep' = {
  name: 'openai'
  params: {
    location: location
    resourcePrefix: resourcePrefix
  }
}

// PostgreSQL Flexible Server
module postgresql 'modules/postgresql.bicep' = {
  name: 'postgresql'
  params: {
    location: location
    resourcePrefix: resourcePrefix
    isProd: isProd
  }
}

// App Service (must come last — needs other resource URIs for KV references)
module appService 'modules/appservice.bicep' = {
  name: 'appService'
  params: {
    location: location
    resourcePrefix: resourcePrefix
    isProd: isProd
    keyVaultUri: keyVault.outputs.keyVaultUri
    applicationInsightsConnectionString: monitoring.outputs.connectionString
  }
}

// RBAC: Grant App Service managed identity access to each service
module appServiceRbac 'modules/rbac.bicep' = {
  name: 'appServiceRbac'
  params: {
    appServicePrincipalId: appService.outputs.principalId
    keyVaultName: keyVault.outputs.keyVaultName
    storageAccountName: storage.outputs.storageAccountName
    searchServiceName: search.outputs.searchServiceName
    openAiAccountName: openai.outputs.openAiAccountName
  }
}

output appServiceHostName string = appService.outputs.hostName
output keyVaultUri string = keyVault.outputs.keyVaultUri
