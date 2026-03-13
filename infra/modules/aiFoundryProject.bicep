@description('Azure region for the resource.')
param location string

@description('Name of the Foundry project.')
param projectName string

@description('Name of the parent AI Foundry account.')
param aiFoundryName string

@description('Resource tags.')
param tags object

resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: aiFoundryName
}

resource aiFoundryProject 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: aiFoundry
  name: projectName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
  tags: tags
}

@description('The resource ID of the Foundry project.')
output id string = aiFoundryProject.id

@description('The name of the Foundry project.')
output name string = aiFoundryProject.name
