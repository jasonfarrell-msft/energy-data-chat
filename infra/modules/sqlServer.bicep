@description('Location for the SQL Server resource.')
param location string

@description('Short location code used in naming.')
param shortLocation string

@description('Suffix for resource naming.')
@minLength(4)
@maxLength(4)
param suffix string

@description('Tags to apply to the resource.')
param tags object

@description('SQL Server administrator login username.')
param administratorLogin string

@secure()
@description('SQL Server administrator login password.')
param administratorLoginPassword string

var sqlServerName = 'sqlsvr-pseg-${shortLocation}-${suffix}'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
  }
}

@description('The resource ID of the SQL Server.')
output id string = sqlServer.id

@description('The name of the SQL Server.')
output name string = sqlServer.name
