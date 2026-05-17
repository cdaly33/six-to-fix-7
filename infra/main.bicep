targetScope = 'resourceGroup'

@allowed([
  'dev'
  'prod'
])
param environment string

param location string = resourceGroup().location
param appName string = 'six-to-fix'
param tenantId string
param postgresAdminLogin string = 'sfadmin'
@secure()
param postgresAdminPassword string
@secure()
param sfAppPassword string
param openAiAccountName string = ''

var databaseName = 'sixtofix'
var normalizedAppName = toLower(replace(appName, '-', ''))

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring-${environment}'
  params: {
    appName: normalizedAppName
    environment: environment
    location: location
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage-${environment}'
  params: {
    appName: normalizedAppName
    environment: environment
    location: location
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres-${environment}'
  params: {
    appName: normalizedAppName
    environment: environment
    location: location
    adminLogin: postgresAdminLogin
    adminPassword: postgresAdminPassword
    databaseName: databaseName
  }
}

module search 'modules/search.bicep' = {
  name: 'search-${environment}'
  params: {
    appName: normalizedAppName
    environment: environment
    location: location
  }
}

var bootstrapSecrets = {
  // Runtime app user (least-privilege) — sf_app must be created in PostgreSQL before the app starts.
  'ConnectionStrings--DefaultConnection': 'Host=${postgres.outputs.fqdn};Port=6432;Database=${databaseName};Username=sf_app;Password=${sfAppPassword};No Reset On Close=true;Ssl Mode=Require'
  // DDL admin — used only for migrations, never by the runtime app.
  'ConnectionStrings--AdminConnection': 'Host=${postgres.outputs.fqdn};Port=5432;Database=${databaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};Ssl Mode=Require;Trust Server Certificate=false'
  // Jwt--SigningKey, HubSpot--PrivateAppToken, HubSpot--WebhookSecret, AzureOpenAI--ApiKey
  // must be set manually after deploy — see docs/deployment/NEXT-STEPS-FOR-CHRIS.md Step 2.
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault-${environment}'
  params: {
    appName: normalizedAppName
    environment: environment
    location: location
    tenantId: tenantId
    secrets: bootstrapSecrets
  }
}

module appservice 'modules/appservice.bicep' = {
  name: 'appservice-${environment}'
  params: {
    appName: normalizedAppName
    environment: environment
    location: location
    appInsightsConnectionString: monitoring.outputs.connectionString
    keyVaultName: keyvault.outputs.name
    keyVaultUri: keyvault.outputs.uri
    blobEndpoint: storage.outputs.blobEndpoint
    searchEndpoint: search.outputs.endpoint
  }
}

module identity 'modules/identity.bicep' = {
  name: 'identity-${environment}'
  params: {
    principalId: appservice.outputs.principalId
    keyVaultName: keyvault.outputs.name
    storageAccountName: storage.outputs.name
    searchServiceName: search.outputs.name
    openAiAccountName: openAiAccountName
  }
}

resource http5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-${normalizedAppName}-${environment}-http5xx'
  location: 'global'
  properties: {
    description: 'Alert when the web app emits more than 10 HTTP 5xx responses per minute.'
    severity: 2
    enabled: true
    scopes: [
      appservice.outputs.id
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT1M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'http5xx-threshold'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          operator: 'GreaterThan'
          timeAggregation: 'Total'
          threshold: 10
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    autoMitigate: true
  }
}

resource dependencyFailuresAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-${normalizedAppName}-${environment}-dependency-failures'
  location: 'global'
  properties: {
    description: 'Alert when Application Insights dependency failures exceed 5 per minute.'
    severity: 2
    enabled: true
    scopes: [
      monitoring.outputs.appInsightsId
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT1M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'dependency-failures-threshold'
          metricNamespace: 'microsoft.insights/components'
          metricName: 'dependencies/failed'
          operator: 'GreaterThan'
          timeAggregation: 'Count'
          threshold: 5
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    autoMitigate: true
  }
}

output appServiceName string = appservice.outputs.name
output appServiceHostName string = appservice.outputs.hostName
output keyVaultUri string = keyvault.outputs.uri
output postgresServerName string = postgres.outputs.name
output searchServiceName string = search.outputs.name