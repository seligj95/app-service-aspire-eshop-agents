// AI Foundry Module for Azure resources
// This module creates all AI Foundry resources in a resource group

@description('Name for the AI resource and used to derive names of dependent resources.')
param aiHubName string = 'basic-hub'

@description('Friendly name for your Azure AI resource')
param aiHubFriendlyName string = 'Agents basic hub resource'

@description('Description of your Azure AI resource displayed in AI studio')
param aiHubDescription string = 'A basic hub resource required for the agent setup.'

@description('Name for the project')
param aiProjectName string = 'basic-project'

@description('Friendly name for your Azure AI project resource')
param aiProjectFriendlyName string = 'Agents basic project resource'

@description('Description of your Azure AI project resource displayed in AI studio')
param aiProjectDescription string = 'A basic project resource required for the agent setup.'

@description('Azure region used for the deployment of all resources.')
param location string

@description('Set of tags to apply to all resources.')
param tags object = {}

@description('Model name for deployment')
param modelName string = 'gpt-4o-mini'

@description('Model format for deployment')
param modelFormat string = 'OpenAI'

@description('Model version for deployment')
param modelVersion string = '2024-07-18'

@description('Model deployment SKU name, prefer GlobalStandard for production workloads')
param modelSkuName string = 'GlobalStandard'

@description('Model deployment capacity')
param modelCapacity int = 50

@description('Model deployment location. If blank, uses the same location as other resources.')
param modelLocation string = ''

@description('AI Service Account kind: either OpenAI or AIServices')
param aiServiceKind string = 'AIServices'

@description('Storage account name (must be globally unique)')
param storageAccountName string

@description('Azure AI Services name')
param aiServicesName string

@description('Key Vault name (must be globally unique)')
param keyVaultName string

@description('Resource token for unique resource naming')
param resourceToken string

@description('Subscription ID for project connection string')
param subscriptionId string 

@description('Resource Group name for project connection string')
param rgName string

@description('Service Principal ID to grant access to resources')
param webAppPrincipalId string = ''

// VARIABLES
var effectiveModelLocation = !empty(modelLocation) ? modelLocation : location
var hubNameClean = toLower(aiHubName)
var projectNameClean = toLower(aiProjectName)
var aiHubResourceName = 'ai-${hubNameClean}-${resourceToken}'
var aiProjectResourceName = 'ai-${projectNameClean}-${resourceToken}'

// Regions without Zone-Redundant Storage support
var noZRSRegions = ['southindia', 'westus']
var storageSku = contains(noZRSRegions, location) ? { name: 'Standard_GRS' } : { name: 'Standard_ZRS' }

// RESOURCES

// 1. AI SERVICES (Cognitive Services)
resource aiServices 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: aiServicesName
  location: effectiveModelLocation
  sku: {
    name: 'S0'
  }
  kind: aiServiceKind
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: toLower(aiServicesName)
    publicNetworkAccess: 'Enabled'
  }
}

// 2. MODEL DEPLOYMENT
resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: aiServices
  name: modelName
  sku: {
    capacity: modelCapacity
    name: modelSkuName
  }
  properties: {
    model: {
      name: modelName
      format: modelFormat
      version: modelVersion
    }
  }
}

// 3. STORAGE ACCOUNT
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: storageSku
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
      virtualNetworkRules: []
    }
    allowSharedKeyAccess: false
  }
}

// 4. KEY VAULT
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableSoftDelete: true
    enabledForTemplateDeployment: true
    accessPolicies: []
  }
}

// 5. AI HUB (Machine Learning workspace of kind 'hub')
resource aiHub 'Microsoft.MachineLearningServices/workspaces@2024-10-01-preview' = {
  name: aiHubResourceName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    // organization
    friendlyName: aiHubFriendlyName
    description: aiHubDescription

    // dependent resources
    keyVault: keyVault.id
    storageAccount: storage.id
    systemDatastoresAuthMode: 'identity'
  }
  kind: 'hub'

  // AI Services Connection as a child resource
  resource aiServicesConnection 'connections@2024-10-01-preview' = {
    name: '${aiHubResourceName}-connection-AIServices'
    properties: {
      category: aiServiceKind
      target: aiServices.properties.endpoint
      authType: 'AAD'
      isSharedToAll: true
      metadata: {
        ApiType: 'Azure'
        ResourceId: aiServices.id
        Location: aiServices.location
      }
    }
  }
}

// 6. AI PROJECT (Machine Learning workspace of kind 'project')
var projectConnectionString = '${location}.api.azureml.ms;${subscriptionId};${rgName};${aiProjectResourceName}'

resource aiProject 'Microsoft.MachineLearningServices/workspaces@2024-10-01-preview' = {
  name: aiProjectResourceName
  location: location
  tags: union(tags, {
    ProjectConnectionString: projectConnectionString
  })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    // organization
    friendlyName: aiProjectFriendlyName
    description: aiProjectDescription
    systemDatastoresAuthMode: 'identity'

    // dependent resources
    hubResourceId: aiHub.id
  }
  kind: 'project'
}

// 7. ROLE ASSIGNMENTS
resource cognitiveServicesContributorRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: '25fbc0a9-bd7c-42a3-aa1a-3b75d497ee68'
  scope: subscription()
}

resource cognitiveServicesContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiServices.id, cognitiveServicesContributorRole.id, aiProject.id)
  properties: {  
    principalId: aiProject.identity.principalId
    roleDefinitionId: cognitiveServicesContributorRole.id
    principalType: 'ServicePrincipal'
  }
}

resource cognitiveServicesOpenAIUserRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
  scope: subscription()
}

resource cognitiveServicesOpenAIUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiProject.id, cognitiveServicesOpenAIUserRole.id, aiServices.id)
  properties: {
    principalId: aiProject.identity.principalId
    roleDefinitionId: cognitiveServicesOpenAIUserRole.id
    principalType: 'ServicePrincipal'
  }
}

resource cognitiveServicesUserRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: 'a97b65f3-24c7-4388-baec-2e87135dc908'
  scope: subscription()
}

resource cognitiveServicesUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiProject.id, cognitiveServicesUserRole.id, aiServices.id)
  properties: {
    principalId: aiProject.identity.principalId
    roleDefinitionId: cognitiveServicesUserRole.id
    principalType: 'ServicePrincipal'
  }
}

// 8. Web App access to AI Project (optional)
// resource contributorRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = if (!empty(webAppPrincipalId)) {
//   name: 'b24988ac-6180-42a0-ab88-20f7382dd24c'  // Contributor
//   scope: subscription()
// }

// resource webAppAIProjectRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(webAppPrincipalId)) {
//   name: guid(subscription().id, aiProject.id, webAppPrincipalId, 'contributor')
//   properties: {
//     principalId: webAppPrincipalId
//     roleDefinitionId: contributorRoleDefinition.id
//     principalType: 'ServicePrincipal'
//   }
// }

// OUTPUTS
output projectConnectionString string = projectConnectionString
output aiHubId string = aiHub.id
output aiHubName string = aiHub.name
output aiProjectId string = aiProject.id
output aiProjectName string = aiProject.name
output aiServicesId string = aiServices.id
output aiServicesEndpoint string = aiServices.properties.endpoint
output storageAccountId string = storage.id
output keyVaultId string = keyVault.id
