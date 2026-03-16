@description('Location for the Container App resource.')
param location string

@description('Short location code used in naming.')
param shortLocation string

@description('Suffix for resource naming.')
@minLength(4)
@maxLength(4)
param suffix string

@description('Tags to apply to the resource.')
param tags object

@description('The resource ID of the Container App Environment.')
param containerEnvironmentId string

@description('The login server of the Container Registry.')
param containerRegistryLoginServer string

@description('The resource ID of the User Assigned Identity.')
param userAssignedIdentityId string

@description('The container image name and tag.')
param imageName string

@description('The SQL Server connection string for the energy database.')
@secure()
param sqlConnectionString string

@description('The AI Foundry project endpoint.')
param aiFoundryProjectEndpoint string

var containerAppName = 'aca-energy-data-mcp-${shortLocation}-${suffix}'

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerEnvironmentId
    configuration: {
      secrets: [
        {
          name: 'sql-connection-string'
          value: sqlConnectionString
        }
      ]
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: containerRegistryLoginServer
          identity: userAssignedIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'energy-data-mcp'
          image: '${containerRegistryLoginServer}/${imageName}'
          env: [
            {
              name: 'ConnectionStrings__EnergyDb'
              secretRef: 'sql-connection-string'
            }
            {
              name: 'AzureAI__Endpoint'
              value: aiFoundryProjectEndpoint
            }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

@description('The resource ID of the Container App.')
output id string = containerApp.id

@description('The name of the Container App.')
output name string = containerApp.name

@description('The FQDN of the Container App.')
output fqdn string = containerApp.properties.configuration.ingress.fqdn
