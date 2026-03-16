@description('Name of the parent AI Foundry account.')
param aiFoundryName string

@description('Name for the model deployment.')
param deploymentName string = 'gpt-5.2-chat-deployment'

@description('Model name to deploy.')
param modelName string = 'gpt-5.2-chat'

@description('Model version.')
param modelVersion string = '2025-12-11'

@description('Deployment SKU name.')
param skuName string = 'GlobalStandard'

@description('Deployment capacity (thousands of tokens per minute).')
param skuCapacity int = 50

resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: aiFoundryName
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: aiFoundry
  name: deploymentName
  sku: {
    name: skuName
    capacity: skuCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
  }
}

@description('The name of the model deployment.')
output name string = modelDeployment.name
