param location string
param environment string
param appName string

var isProd = environment == 'prod'
var nameSuffix = '${appName}-${environment}'

// SKU note: prod uses Standard (2 replicas for HA + semantic search on six-to-fix-evidence).
// Only one index remains (six-to-fix-evidence); six-to-fix-skill-outputs and
// six-to-fix-calibration were removed (data lives in PostgreSQL).
// Downgrade to Basic is possible IF: (a) replicaCount drops to 1 and (b) semantic search
// is confirmed available on Basic in the target region. Validate before changing the default.
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
