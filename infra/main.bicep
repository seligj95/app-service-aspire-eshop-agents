targetScope = 'resourceGroup'

@description('Primary location for all resources')
param location string = resourceGroup().location

@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string

@description('Optional App Service name override')
param appServiceName string = ''

@description('Optional App Service Plan name override')
param appServicePlanName string = ''

@description('Model name for deployment')
param modelName string = 'gpt-4o-mini'

@description('Model format for deployment')
param modelFormat string = 'OpenAI'

@description('Model version for deployment')
param modelVersion string = '2024-07-18'

@description('Model deployment SKU name')
param modelSkuName string = 'GlobalStandard'

@description('Model deployment capacity')
param modelCapacity int = 50

// Load abbreviations
var abbrs = loadJsonContent('./abbreviations.json')

// VARIABLES
var tags = {
  'azd-env-name': environmentName
}

var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

// Resource names
var actualAppServicePlanName = !empty(appServicePlanName) ? appServicePlanName : '${abbrs.webServerFarms}${resourceToken}'
var actualAppServiceName = !empty(appServiceName) ? appServiceName : '${abbrs.webSitesAppService}web-${resourceToken}'
var aiServiceName = 'ai-${resourceToken}'
var deploymentName = modelName

// RESOURCES

// Create an App Service Plan
module appServicePlan './core/host/appserviceplan.bicep' = {
  name: 'appserviceplan'
  params: {
    name: actualAppServicePlanName
    location: location
    tags: tags
    sku: {
      name: 'P0v3'
    }
  }
}

// Create the AI Foundry resource (CognitiveServices account)
resource aiFoundryResource 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: aiServiceName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: toLower(aiServiceName)
    publicNetworkAccess: 'Enabled'
    allowProjectManagement: true
  }
}

// Create an AI Project as a child resource
resource aiProject 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' = {
  parent: aiFoundryResource
  name: 'proj-${resourceToken}'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: 'AI Project for .NET Agent'
  }
}

// Create model deployment
resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = {
  parent: aiFoundryResource
  name: deploymentName
  sku: {
    name: modelSkuName
    capacity: modelCapacity
  }
  properties: {
    model: {
      format: modelFormat
      name: modelName
      version: modelVersion
    }
  }
}

// The application App - using the working module approach
module web './core/host/appservice.bicep' = {
  name: 'web'
  params: {
    name: actualAppServiceName
    location: location
    appServicePlanId: appServicePlan.outputs.id
    runtimeName: 'dotnetcore'
    runtimeVersion: '9.0'
    tags: union(tags, { 'azd-service-name': 'web' })
    appSettings: {
      AI_PROJECT_ENDPOINT: 'https://${aiServiceName}.services.ai.azure.com/api/projects/${aiProject.name}'
      AI_SERVICES_ENDPOINT: aiFoundryResource.properties.endpoint
      AI_MODEL_DEPLOYMENT_NAME: deploymentName
    }
  }
}

// Role assignments for the app service to access AI services
resource cognitiveServicesContributorRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: '25fbc0a9-bd7c-42a3-aa1a-3b75d497ee68'
  scope: subscription()
}

resource cognitiveServicesContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiFoundryResource.id, cognitiveServicesContributorRole.id, actualAppServiceName)
  properties: {
    principalId: web.outputs.identityPrincipalId
    roleDefinitionId: cognitiveServicesContributorRole.id
    principalType: 'ServicePrincipal'
  }
}

resource cognitiveServicesOpenAIUserRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
  scope: subscription()
}

resource cognitiveServicesOpenAIUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiFoundryResource.id, cognitiveServicesOpenAIUserRole.id, actualAppServiceName)
  properties: {
    principalId: web.outputs.identityPrincipalId
    roleDefinitionId: cognitiveServicesOpenAIUserRole.id
    principalType: 'ServicePrincipal'
  }
}

resource cognitiveServicesUserRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: 'a97b65f3-24c7-4388-baec-2e87135dc908'
  scope: subscription()
}

resource cognitiveServicesUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiFoundryResource.id, cognitiveServicesUserRole.id, actualAppServiceName)
  properties: {
    principalId: web.outputs.identityPrincipalId
    roleDefinitionId: cognitiveServicesUserRole.id
    principalType: 'ServicePrincipal'
  }
}

// OUTPUTS
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = resourceGroup().name
output AI_PROJECT_ENDPOINT string = 'https://${aiServiceName}.services.ai.azure.com/api/projects/${aiProject.name}'
output AI_SERVICES_ENDPOINT string = aiFoundryResource.properties.endpoint
output AI_MODEL_DEPLOYMENT_NAME string = deploymentName
output SERVICE_WEB_IDENTITY_PRINCIPAL_ID string = web.outputs.identityPrincipalId
output SERVICE_WEB_NAME string = web.outputs.name
output SERVICE_WEB_URI string = web.outputs.uri
