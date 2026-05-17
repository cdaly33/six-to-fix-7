using '../main.bicep'

param environment = 'dev'
param appName = 'six-to-fix'
param tenantId = '0cb15a3f-7b9f-4e7d-8c3f-baac8907a1ed'

// Match prod region to avoid eastus2 capacity restrictions for PostgreSQL and AI Search.
param location = 'centralus'
