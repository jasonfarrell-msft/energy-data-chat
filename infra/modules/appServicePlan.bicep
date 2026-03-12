@description('Location for the App Service Plan.')
param location string

@description('Short location code used in naming.')
param shortLocation string

@description('Suffix for resource naming.')
@minLength(4)
@maxLength(4)
param suffix string

@description('Tags to apply to the resource.')
param tags object

var appServicePlanName = 'asp-pseg-energychat-${shortLocation}-${suffix}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

@description('The resource ID of the App Service Plan.')
output id string = appServicePlan.id

@description('The name of the App Service Plan.')
output name string = appServicePlan.name
