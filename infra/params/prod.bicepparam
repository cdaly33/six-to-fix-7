using '../main.bicep'

param environment = 'prod'
param appName = 'six-to-fix'
param tenantId = '0cb15a3f-7b9f-4e7d-8c3f-baac8907a1ed'

// Set env vars before running (never put secrets in this file):
//   $env:POSTGRES_ADMIN_PASSWORD = '<password>'
//   $env:SF_APP_PASSWORD = '<password>'
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
param sfAppPassword = readEnvironmentVariable('SF_APP_PASSWORD')
