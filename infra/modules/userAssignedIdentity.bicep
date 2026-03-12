@description('Location for the User Assigned Identity resource.')
param location string

@description('Short location code used in naming.')
param shortLocation string

@description('Suffix for resource naming.')
@minLength(4)
@maxLength(4)
param suffix string

@description('Tags to apply to the resource.')
param tags object

var identityName = 'uai-pseg-energychat-core-${shortLocation}-${suffix}'

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: tags
}

@description('The resource ID of the User Assigned Identity.')
output id string = userAssignedIdentity.id

@description('The name of the User Assigned Identity.')
output name string = userAssignedIdentity.name

@description('The principal ID of the User Assigned Identity.')
output principalId string = userAssignedIdentity.properties.principalId

@description('The client ID of the User Assigned Identity.')
output clientId string = userAssignedIdentity.properties.clientId
