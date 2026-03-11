@description('Name of the SQL Server to host the database.')
param sqlServerName string

@description('Location for the SQL Database resource.')
param location string

@description('Suffix for resource naming.')
@minLength(4)
@maxLength(4)
param suffix string

@description('Tags to apply to the resource.')
param tags object

@description('The max vCores for serverless auto-pause. Default: 1')
param maxCapacity int = 1

@description('The min vCores for serverless auto-pause. Default: 0.5')
param minCapacity string = '0.5'

@description('Auto-pause delay in minutes. -1 to disable. Default: 30')
param autoPauseDelay int = 30

@description('Max size in bytes for the database. Default: 2147483648 (2 GB)')
param maxSizeBytes int = 2147483648

var sqlDatabaseName = 'sqldb-pseg-energrydata-${suffix}'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' existing = {
  name: sqlServerName
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: maxCapacity
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: maxSizeBytes
    autoPauseDelay: autoPauseDelay
    minCapacity: json(minCapacity)
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}

@description('The resource ID of the SQL Database.')
output id string = sqlDatabase.id

@description('The name of the SQL Database.')
output name string = sqlDatabase.name
