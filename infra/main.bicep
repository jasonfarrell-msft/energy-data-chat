targetScope = 'resourceGroup'

// ──────────────────────────────────────────────
// Parameters
// ──────────────────────────────────────────────

@description('Four-character suffix for resource naming.')
@minLength(4)
@maxLength(4)
param suffix string = 'mx01'

@description('SQL Server administrator login username.')
param sqlAdminLogin string

@secure()
@description('SQL Server administrator login password.')
param sqlAdminPassword string

@description('Container image name and tag for the energy chat API (e.g. energy-chat-api:v1).')
param containerImageName string

// ──────────────────────────────────────────────
// Variables
// ──────────────────────────────────────────────

var location = resourceGroup().location

var locationShortMap = {
  eastus2: 'eus2'
  westus: 'wus'
  eastus: 'eus'
  centralus: 'cus'
}

var shortLocation = locationShortMap[location]

var commonTags = {
  SecurityControl: 'Ignore'
}

// ──────────────────────────────────────────────
// Modules
// ──────────────────────────────────────────────

module sqlServer 'modules/sqlServer.bicep' = {
  name: 'deploy-sqlServer'
  params: {
    location: location
    shortLocation: shortLocation
    suffix: suffix
    tags: commonTags
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
  }
}

module sqlDatabase 'modules/sqlDatabase.bicep' = {
  name: 'deploy-sqlDatabase'
  params: {
    sqlServerName: sqlServer.outputs.name
    location: location
    suffix: suffix
    tags: commonTags
  }
}

module containerEnvironment 'modules/containerEnvironment.bicep' = {
  name: 'deploy-containerEnvironment'
  params: {
    location: location
    shortLocation: shortLocation
    suffix: suffix
    tags: commonTags
  }
}

module containerRegistry 'modules/containerRegistry.bicep' = {
  name: 'deploy-containerRegistry'
  params: {
    location: location
    shortLocation: shortLocation
    suffix: suffix
    tags: commonTags
  }
}

module userAssignedIdentity 'modules/userAssignedIdentity.bicep' = {
  name: 'deploy-userAssignedIdentity'
  params: {
    location: location
    shortLocation: shortLocation
    suffix: suffix
    tags: commonTags
  }
}

module acrPullRoleAssignment 'modules/acrPullRoleAssignment.bicep' = {
  name: 'deploy-acrPullRoleAssignment'
  params: {
    containerRegistryName: containerRegistry.outputs.name
    principalId: userAssignedIdentity.outputs.principalId
  }
}

module appServicePlan 'modules/appServicePlan.bicep' = {
  name: 'deploy-appServicePlan'
  params: {
    location: location
    shortLocation: shortLocation
    suffix: suffix
    tags: commonTags
  }
}

module appService 'modules/appService.bicep' = {
  name: 'deploy-appService'
  params: {
    location: location
    shortLocation: shortLocation
    suffix: suffix
    tags: commonTags
    appServicePlanId: appServicePlan.outputs.id
    containerAppFqdn: containerApp.outputs.fqdn
  }
}

module containerApp 'modules/containerApp.bicep' = {
  name: 'deploy-containerApp'
  dependsOn: [
    acrPullRoleAssignment
  ]
  params: {
    location: location
    shortLocation: shortLocation
    suffix: suffix
    tags: commonTags
    containerEnvironmentId: containerEnvironment.outputs.id
    containerRegistryLoginServer: containerRegistry.outputs.loginServer
    userAssignedIdentityId: userAssignedIdentity.outputs.id
    imageName: containerImageName
    frontendHostname: 'app-pseg-energychat-${shortLocation}-${suffix}.azurewebsites.net'
  }
}

// ──────────────────────────────────────────────
// Outputs
// ──────────────────────────────────────────────

@description('The name of the deployed SQL Server.')
output sqlServerName string = sqlServer.outputs.name

@description('The name of the deployed SQL Database.')
output sqlDatabaseName string = sqlDatabase.outputs.name

@description('The name of the deployed Container App Environment.')
output containerEnvironmentName string = containerEnvironment.outputs.name

@description('The name of the deployed Container Registry.')
output containerRegistryName string = containerRegistry.outputs.name

@description('The login server of the deployed Container Registry.')
output containerRegistryLoginServer string = containerRegistry.outputs.loginServer

@description('The name of the deployed App Service.')
output appServiceName string = appService.outputs.name

@description('The default hostname of the deployed App Service.')
output appServiceHostname string = appService.outputs.defaultHostname

@description('The name of the deployed User Assigned Identity.')
output userAssignedIdentityName string = userAssignedIdentity.outputs.name

@description('The name of the deployed Container App.')
output containerAppName string = containerApp.outputs.name

@description('The FQDN of the deployed Container App.')
output containerAppFqdn string = containerApp.outputs.fqdn
