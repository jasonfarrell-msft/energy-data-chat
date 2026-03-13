@description('Name of the AI Foundry account to assign the role on.')
param aiFoundryName string

@description('Principal ID of the identity to grant the role to.')
param principalId string

// Cognitive Services User – allows calling AI services endpoints
var cognitiveServicesUserRoleId = 'a97b65f3-24c7-4388-baec-2e87135dc908'

resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: aiFoundryName
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiFoundry.id, principalId, cognitiveServicesUserRoleId)
  scope: aiFoundry
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesUserRoleId)
    principalType: 'ServicePrincipal'
  }
}
