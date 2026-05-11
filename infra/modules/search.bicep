param location string
param environment string
param appName string

var isProd = environment == 'prod'
var nameSuffix = '${appName}-${environment}'

resource searchService 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: 'srch-${nameSuffix}'
  location: location
  sku: {
    name: isProd ? 'standard' : 'basic'
  }
  properties: {
    replicaCount: isProd ? 2 : 1
    partitionCount: 1
    publicNetworkAccess: 'enabled'
  }
}

output endpoint string = 'https://${searchService.name}.search.windows.net'
output id string = searchService.id
output name string = searchService.name
