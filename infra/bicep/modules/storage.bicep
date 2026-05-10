@description('Azure region')
param location string

@description('Resource prefix')
param resourcePrefix string

@description('Whether this is production')
param isProd bool

// Storage account name must be globally unique, 3-24 lowercase alphanumeric
var storageAccountName = 'st${replace(resourcePrefix, '-', '')}001'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    // ZRS for prod resilience, LRS for dev per environment-contract.md §4
    name: isProd ? 'Standard_ZRS' : 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

// Container for skill run prompt/response blobs
resource skillRunsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageAccount.name}/default/skill-runs'
  properties: {
    publicAccess: 'None'
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
