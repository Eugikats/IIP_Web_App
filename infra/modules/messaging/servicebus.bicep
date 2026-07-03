targetScope = 'resourceGroup'

@description('The namespace name.')
param namespaceName string

@description('The deployment location.')
param location string

@description('The Service Bus SKU.')
param skuName string = 'Basic'

@description('List of queues to create.')
param queueNames array

@description('Whether to disable local authentication (use Entra ID).')
param disableLocalAuth bool = true

@description('Whether public network access is disabled.')
param publicNetworkAccessDisabled bool = false

@description('Resource tags.')
param tags object = {}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    disableLocalAuth: disableLocalAuth
    publicNetworkAccess: publicNetworkAccessDisabled ? 'Disabled' : 'Enabled'
  }
}

resource serviceBusQueues 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = [for queueName in queueNames: {
  parent: serviceBusNamespace
  name: queueName
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 10
  }
}]

output namespaceId string = serviceBusNamespace.id
output namespaceEndpoint string = '${serviceBusNamespace.name}.servicebus.windows.net'
output namespaceName string = serviceBusNamespace.name
