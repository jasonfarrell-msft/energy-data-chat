@description('Location for the App Service.')
param location string

@description('Short location code used in naming.')
param shortLocation string

@description('Suffix for resource naming.')
@minLength(4)
@maxLength(4)
param suffix string

@description('Tags to apply to the resource.')
param tags object

@description('The resource ID of the App Service Plan.')
param appServicePlanId string

@description('The FQDN of the backend Container App (used for API proxy config).')
param containerAppFqdn string

@description('The Application Insights connection string.')
param appInsightsConnectionString string

var appServiceName = 'app-pseg-energychat-${shortLocation}-${suffix}'

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: appServiceName
  location: location
  tags: tags
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'NODE|20-lts'
      appCommandLine: 'pm2 serve /home/site/wwwroot --no-daemon --spa --port 8080'
      appSettings: [
        {
          name: 'API_BASE_URL'
          value: 'https://${containerAppFqdn}'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
      ]
    }
  }
}

@description('The resource ID of the App Service.')
output id string = appService.id

@description('The name of the App Service.')
output name string = appService.name

@description('The default hostname of the App Service.')
output defaultHostname string = appService.properties.defaultHostName
