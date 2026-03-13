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

@description('The FQDN of the frontend App Service (for CORS).')
param frontendHostname string

@description('The AI Foundry project endpoint.')
param aiFoundryProjectEndpoint string

@description('The name of the Foundry agent to invoke.')
param aiAgentName string = 'chat-agent'

@description('The Application Insights connection string.')
param appInsightsConnectionString string

var containerAppName = 'aca-energy-chat-api-${shortLocation}-${suffix}'

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerEnvironmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        corsPolicy: {
          allowedOrigins: [
            'http://localhost:5173'
            'https://${frontendHostname}'
          ]
          allowedMethods: [
            'GET'
            'POST'
            'OPTIONS'
          ]
          allowedHeaders: [
            'Content-Type'
          ]
        }
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
          name: 'energy-chat-api'
          image: '${containerRegistryLoginServer}/${imageName}'
          env: [
            {
              name: 'AzureAI__Endpoint'
              value: aiFoundryProjectEndpoint
            }
            {
              name: 'AzureAI__AgentName'
              value: aiAgentName
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsightsConnectionString
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
        maxReplicas: 5
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

@description('The principal ID of the Container App system-assigned identity.')
output systemIdentityPrincipalId string = containerApp.identity.principalId
