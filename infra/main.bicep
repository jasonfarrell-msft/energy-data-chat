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

@description('Name of the AI Foundry project.')
param foundryProjectName string

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

module applicationInsights 'modules/applicationInsights.bicep' = {
  name: 'deploy-applicationInsights'
  params: {
    location: location
    shortLocation: shortLocation
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
    logAnalyticsWorkspaceId: applicationInsights.outputs.logAnalyticsWorkspaceId
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
    appInsightsConnectionString: applicationInsights.outputs.connectionString
  }
}

module aiFoundry 'modules/aiFoundry.bicep' = {
  name: 'deploy-aiFoundry'
  params: {
    location: location
    shortLocation: shortLocation
    suffix: suffix
    tags: commonTags
  }
}

module aiFoundryProject 'modules/aiFoundryProject.bicep' = {
  name: 'deploy-aiFoundryProject'
  params: {
    location: location
    projectName: foundryProjectName
    aiFoundryName: aiFoundry.outputs.name
    tags: commonTags
  }
}

module aiModelDeployment 'modules/aiModelDeployment.bicep' = {
  name: 'deploy-aiModelDeployment'
  dependsOn: [
    aiFoundryProject
  ]
  params: {
    aiFoundryName: aiFoundry.outputs.name
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
    aiFoundryProjectEndpoint: '${aiFoundry.outputs.endpoint}/api/projects/${foundryProjectName}'
    appInsightsConnectionString: applicationInsights.outputs.connectionString
  }
}

module aiFoundryRbac 'modules/aiFoundryRbac.bicep' = {
  name: 'deploy-aiFoundryRbac'
  params: {
    aiFoundryName: aiFoundry.outputs.name
    principalId: containerApp.outputs.systemIdentityPrincipalId
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

@description('The name of the deployed Application Insights instance.')
output applicationInsightsName string = applicationInsights.outputs.name

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

@description('The name of the AI Foundry account.')
output aiFoundryName string = aiFoundry.outputs.name

@description('The endpoint of the AI Foundry account.')
output aiFoundryEndpoint string = aiFoundry.outputs.endpoint

@description('The name of the AI Foundry project.')
output aiFoundryProjectName string = aiFoundryProject.outputs.name

@description('The system-assigned identity principal ID of the Container App.')
output containerAppSystemIdentityPrincipalId string = containerApp.outputs.systemIdentityPrincipalId

@description('The name of the model deployment.')
output aiModelDeploymentName string = aiModelDeployment.outputs.name
