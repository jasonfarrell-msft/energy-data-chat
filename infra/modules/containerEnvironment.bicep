@description('Location for the Container App Environment resource.')
param location string

@description('Short location code used in naming.')
param shortLocation string

@description('Suffix for resource naming.')
@minLength(4)
@maxLength(4)
param suffix string

@description('Tags to apply to the resource.')
param tags object

@description('The resource ID of the Log Analytics workspace.')
param logAnalyticsWorkspaceId string

var containerEnvironmentName = 'cae-pseg-energychat-${shortLocation}-${suffix}'

resource containerEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerEnvironmentName
  location: location
  tags: tags
  properties: {
    zoneRedundant: false
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: last(split(logAnalyticsWorkspaceId, '/'))!
}

@description('The resource ID of the Container App Environment.')
output id string = containerEnvironment.id

@description('The name of the Container App Environment.')
output name string = containerEnvironment.name
