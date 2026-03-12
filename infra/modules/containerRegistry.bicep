@description('Location for the Container Registry resource.')
param location string

@description('Short location code used in naming.')
param shortLocation string

@description('Suffix for resource naming.')
@minLength(4)
@maxLength(4)
param suffix string

@description('Tags to apply to the resource.')
param tags object

var containerRegistryName = 'crpsengchat${shortLocation}${suffix}'

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

@description('The resource ID of the Container Registry.')
output id string = containerRegistry.id

@description('The name of the Container Registry.')
output name string = containerRegistry.name

@description('The login server of the Container Registry.')
output loginServer string = containerRegistry.properties.loginServer
