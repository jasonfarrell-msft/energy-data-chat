@description('Azure region for the resource.')
param location string

@description('Short location code used in naming.')
param shortLocation string

@description('Suffix for resource naming.')
@minLength(4)
@maxLength(4)
param suffix string

@description('Resource tags.')
param tags object

var logAnalyticsName = 'log-pseg-energychat-${shortLocation}-${suffix}'
var appInsightsName = 'appi-pseg-energychat-${shortLocation}-${suffix}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

@description('The resource ID of the Application Insights instance.')
output id string = appInsights.id

@description('The name of the Application Insights instance.')
output name string = appInsights.name

@description('The instrumentation key for Application Insights.')
output instrumentationKey string = appInsights.properties.InstrumentationKey

@description('The connection string for Application Insights.')
output connectionString string = appInsights.properties.ConnectionString

@description('The resource ID of the Log Analytics workspace.')
output logAnalyticsWorkspaceId string = logAnalytics.id
