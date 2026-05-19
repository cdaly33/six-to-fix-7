param location string
param environment string
param appName string
param adminLogin string
@secure()
param adminPassword string
param databaseName string = 'sixtofix'

var isProd = environment == 'prod'
var nameSuffix = '${appName}-${environment}'

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: 'psql-${nameSuffix}'
  location: location
  sku: {
    // Prod downgraded from Standard_D4s_v3/GeneralPurpose to B2ms/Burstable for cost.
    // Workload: 2-5 concurrent users. Burstable tier is appropriate.
    name: isProd ? 'Standard_B2ms' : 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    storage: {
      storageSizeGB: isProd ? 128 : 32
    }
    backup: {
      // Prod: 35-day retention gives a full month of recovery window at minimal extra cost.
      // geoRedundantBackup disabled for both envs — Burstable tier requires LRS; geo-redundant
      // backups are only available on General Purpose and Memory Optimized tiers.
      backupRetentionDays: isProd ? 35 : 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      // Burstable tier does NOT support HA — must be Disabled for all envs.
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource pgBouncerConfig 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: postgresServer
  name: 'pgbouncer.enabled'
  properties: {
    value: 'True'
    source: 'user-override'
  }
}

resource sslConfig 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: postgresServer
  name: 'require_secure_transport'
  properties: {
    value: 'on'
    source: 'user-override'
  }
}

resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgresServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output id string = postgresServer.id
output name string = postgresServer.name
output fqdn string = postgresServer.properties.fullyQualifiedDomainName
output adminLogin string = adminLogin
output databaseName string = database.name
