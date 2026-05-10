@description('Azure region')
param location string

@description('Resource prefix')
param resourcePrefix string

@description('Whether this is production')
param isProd bool

resource searchService 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: 'srch-${resourcePrefix}'
  location: location
  sku: {
    // Prod: Standard S1 for semantic ranking + replicas per environment-contract.md §4
    name: isProd ? 'standard' : 'free'
  }
  properties: {
    replicaCount: isProd ? 2 : 1
    partitionCount: 1
    publicNetworkAccess: 'enabled'
  }
}

// App Service gets Search Index Data Contributor (app writes to index — confirmed per ADR Q9)
// RBAC assignment happens in rbac.bicep

output searchServiceName string = searchService.name
output searchServiceId string = searchService.id
