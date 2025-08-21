targetScope = 'subscription'

// The main bicep module to provision Azure resources.
// For a more complete walkthrough to understand how this file works with azd,
// see https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/make-azd-compatible?pivots=azd-create

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

// Optional parameters to override the default azd resource naming conventions.
// Add the following to main.parameters.json to provide values:
// "resourceGroupName": {
//      "value": "myGroupName"
// }
param resourceGroupName string = ''
param appServiceName string = ''
param appServicePlanName string = ''

// AI Foundry parameters
param aiHubName string = 'aihub'
param aiHubFriendlyName string = 'AI Hub for Agent'
param aiHubDescription string = 'A hub resource required for the AI agent setup.'
param aiProjectName string = 'aiproject'
param aiProjectFriendlyName string = 'AI Project for Agent'
param aiProjectDescription string = 'A project resource required for the AI agent setup.'
param aiModelName string = 'gpt-4o-mini'
param aiModelFormat string = 'OpenAI'
param aiModelVersion string = '2024-07-18'
param aiModelSkuName string = 'GlobalStandard'
param aiModelCapacity int = 50
param aiModelLocation string = ''
param aiServiceKind string = 'AIServices'

var abbrs = loadJsonContent('./abbreviations.json')

// tags that should be applied to all resources.
var tags = {
  // Tag all resources with the environment name.
  'azd-env-name': environmentName
}

// Generate a unique token to be used in naming resources.
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

// Generate resource names for AI Foundry
var aiStorageAccountName = 'aistg${take(replace(resourceToken, '-', ''), 16)}'
var aiServicesAccountName = 'aisvc-${resourceToken}'
var aiKeyVaultName = 'aikv-${resourceToken}'

// Name of the service defined in azure.yaml
// A tag named azd-service-name with this value should be applied to the service host resource, such as:
//   Microsoft.Web/sites for appservice, function
// Example usage:
//   tags: union(tags, { 'azd-service-name': apiServiceName })

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

// Add resources to be provisioned below.

// Create an App Service Plan to group applications under the same payment plan and SKU
module appServicePlan './core/host/appserviceplan.bicep' = {
  name: 'appserviceplan'
  scope: rg
  params: {
    name: !empty(appServicePlanName) ? appServicePlanName : '${abbrs.webServerFarms}${resourceToken}'
    location: location
    tags: tags
    sku: {
      name: 'P0v3'
    }
  }
}

// The application App
module web './core/host/appservice.bicep' = {
  name: 'web'
  scope: rg
  params: {
    name: !empty(appServiceName) ? appServiceName : '${abbrs.webSitesAppService}web-${resourceToken}'
    location: location
    appServicePlanId: appServicePlan.outputs.id
    runtimeName: 'dotnetcore'
    runtimeVersion: '9.0'
    tags: union(tags, { 'azd-service-name': 'web' })
  }
}

// Azure AI Developer role definition ID
var aiDeveloperRoleId = '64702f94-c441-49e6-a78b-ef80e0188fee'

// Create a unique ID for the role assignment that doesn't depend on runtime values
var roleAssignmentName = guid(subscription().id, environmentName, aiDeveloperRoleId)

// Assign Azure AI Developer role to the web app's managed identity
resource azureAIDeveloperRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: roleAssignmentName
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', aiDeveloperRoleId)
    principalId: web.outputs.identityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Deploy the AI Foundry resources using the module
module aifoundry './core/ai/aifoundry.bicep' = {
  name: 'aifoundry'
  scope: rg
  params: {
    aiHubName: aiHubName
    aiHubFriendlyName: aiHubFriendlyName
    aiHubDescription: aiHubDescription
    aiProjectName: aiProjectName
    aiProjectFriendlyName: aiProjectFriendlyName
    aiProjectDescription: aiProjectDescription
    location: location
    tags: tags
    modelName: aiModelName
    modelFormat: aiModelFormat
    modelVersion: aiModelVersion
    modelSkuName: aiModelSkuName
    modelCapacity: aiModelCapacity
    modelLocation: aiModelLocation
    aiServiceKind: aiServiceKind
    storageAccountName: aiStorageAccountName
    aiServicesName: aiServicesAccountName
    keyVaultName: aiKeyVaultName
    resourceToken: resourceToken
    subscriptionId: subscription().subscriptionId
    rgName: rg.name
    webAppPrincipalId: web.outputs.identityPrincipalId
  }
}

// Add outputs from the deployment here, if needed.
//
// This allows the outputs to be referenced by other bicep deployments in the deployment pipeline,
// or by the local machine as a way to reference created resources in Azure for local development.
// Secrets should not be added here.
//
// Outputs are automatically saved in the local azd environment .env file.
// To see these outputs, run `azd env get-values`,  or `azd env get-values --output json` for json output.
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = rg.name

// AI Foundry outputs
output AI_PROJECT_CONNECTION_STRING string = aifoundry.outputs.projectConnectionString
output AI_HUB_NAME string = aifoundry.outputs.aiHubName
output AI_PROJECT_NAME string = aifoundry.outputs.aiProjectName
output AI_SERVICES_ENDPOINT string = aifoundry.outputs.aiServicesEndpoint

// App Service outputs
output SERVICE_WEB_IDENTITY_PRINCIPAL_ID string = web.outputs.identityPrincipalId
output SERVICE_WEB_NAME string = web.outputs.name
output SERVICE_WEB_URI string = web.outputs.uri
