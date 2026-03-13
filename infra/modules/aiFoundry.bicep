@description('Azure region for the resource.')
param location string

@description('Short location code (e.g. eus2).')
param shortLocation string

@description('Four-character suffix for resource naming.')
param suffix string

@description('Resource tags.')
param tags object

var foundryName = 'foundry-pseg-main-${shortLocation}-${suffix}'

resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: foundryName
  location: location
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: foundryName
    publicNetworkAccess: 'Enabled'
    allowProjectManagement: true
  }
  tags: tags
}

@description('The resource ID of the AI Foundry account.')
output id string = aiFoundry.id

@description('The name of the AI Foundry account.')
output name string = aiFoundry.name

@description('The endpoint of the AI Foundry account.')
output endpoint string = aiFoundry.properties.endpoint
