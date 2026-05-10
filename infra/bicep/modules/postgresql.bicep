@description('Azure region')
param location string

@description('Resource prefix')
param resourcePrefix string

@description('Whether this is production')
param isProd bool

// Credentials passed as parameters at deploy time (from GitHub secrets, not stored in Bicep)
@secure()
param adminUsername string = 'sf_admin_deploy'

@secure()
param adminPassword string

// SKUs per environment-contract.md §4:
// Dev: Standard_B2ms / Burstable | Prod: Standard_D4s_v3 / GeneralPurpose
var skuName = isProd ? 'Standard_D4s_v3' : 'Standard_B2ms'
var tier = isProd ? 'GeneralPurpose' : 'Burstable'

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: 'psql-${resourcePrefix}'
  location: location
  sku: {
    name: skuName
    tier: tier
  }
  properties: {
    version: '16'
    administratorLogin: adminUsername
    administratorLoginPassword: adminPassword
    storage: {
      storageSizeGB: isProd ? 64 : 32
    }
    backup: {
      backupRetentionDays: isProd ? 35 : 7
      geoRedundantBackup: isProd ? 'Enabled' : 'Disabled'
    }
    highAvailability: {
      // Prod: SameZone HA per environment-contract.md §4
      mode: isProd ? 'SameZone' : 'Disabled'
    }
  }
}

// Enable pgBouncer connection pooler
resource pgBouncerConfig 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: postgresServer
  name: 'pgbouncer.enabled'
  properties: {
    value: 'true'
    source: 'user-override'
  }
}

// Main application database
resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: 'sixtofix'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// Allow Azure services (0.0.0.0 → 0.0.0.0 is the Azure services sentinel)
resource firewallRuleAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgresServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output serverName string = postgresServer.name
output fqdn string = postgresServer.properties.fullyQualifiedDomainName
// pgBouncer port 6432 is the runtime connection port (sf_app user)
// Port 5432 is for admin/migration connections (sf_admin user via GitHub Actions secret)
output pgBouncerPort int = 6432
