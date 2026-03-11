targetScope = 'resourceGroup'

// ──────────────────────────────────────────────
// Parameters
// ──────────────────────────────────────────────

@description('Four-character suffix for resource naming.')
@minLength(4)
@maxLength(4)
param suffix string = 'mx01'

@description('SQL Server administrator login username.')
param sqlAdminLogin string

@secure()
@description('SQL Server administrator login password.')
param sqlAdminPassword string

// ──────────────────────────────────────────────
// Variables
// ──────────────────────────────────────────────

var location = resourceGroup().location

var locationShortMap = {
  eastus2: 'eus2'
  westus: 'wus'
  eastus: 'eus'
  centralus: 'cus'
}

var shortLocation = locationShortMap[location]

var commonTags = {
  SecurityControl: 'Ignore'
}

// ──────────────────────────────────────────────
// Modules
// ──────────────────────────────────────────────

module sqlServer 'modules/sqlServer.bicep' = {
  name: 'deploy-sqlServer'
  params: {
    location: location
    shortLocation: shortLocation
    suffix: suffix
    tags: commonTags
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
  }
}

module sqlDatabase 'modules/sqlDatabase.bicep' = {
  name: 'deploy-sqlDatabase'
  params: {
    sqlServerName: sqlServer.outputs.name
    location: location
    suffix: suffix
    tags: commonTags
  }
}

// ──────────────────────────────────────────────
// Outputs
// ──────────────────────────────────────────────

@description('The name of the deployed SQL Server.')
output sqlServerName string = sqlServer.outputs.name

@description('The name of the deployed SQL Database.')
output sqlDatabaseName string = sqlDatabase.outputs.name
