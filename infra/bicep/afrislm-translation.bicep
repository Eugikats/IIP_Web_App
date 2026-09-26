// The local-language translation engine: the AfriSLM model file served by llama.cpp in its own
// container, so the API can translate WhatsApp messages between English, Luganda and Kiswahili.
//
// Why a separate container app and not the API's App Service plan: the model needs a few GB of RAM
// and CPU that would otherwise starve the API, and it must be able to restart, scale or be replaced
// without touching the API.
//
// This records what was created in `dev` on 2026-09-27 (by CLI, not by deploying this file). It
// reuses the existing storage account and adds only a Container Apps environment, one container app
// and one file share. The model file itself is never in git: it is uploaded once to the share and
// its SHA-256 is recorded in AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md.
//
// Deploy (only into a resource group that already has the storage account and environment):
//   az deployment group create -g rg-iip-dev -f infra/bicep/afrislm-translation.bicep \
//     -p apiKey=<value from Key Vault secret Translation--ApiKey>

@description('Existing Container Apps environment. Create it first with the Log Analytics workspace already in the resource group.')
param environmentName string = 'cae-iip-dev'

@description('Existing storage account that holds the model file share.')
param storageAccountName string = 'stiiph5girbnzrayqe'

param shareName string = 'afrislm-models'

param containerAppName string = 'ca-afrislm-dev'

param location string = resourceGroup().location

@description('The bearer key the API sends. It is the only thing keeping a public address private to the API, so it comes from Key Vault, never from git.')
@secure()
param apiKey string

@description('Pinned by digest, not by tag: the moving "server" tag would change the engine under a running deployment. The model file uses the qwen35 architecture, so the build must be recent enough to load it.')
param image string = 'ghcr.io/ggml-org/llama.cpp@sha256:6257697a7f5d034b8fb499ddb07af3e250506352f94102054252a23f3b85e0af'

param modelFile string = 'afrislm-0.8b-q4_k_m.gguf'

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: environmentName
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource share 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  name: '${storageAccountName}/default/${shareName}'
  properties: {
    shareQuota: 5
  }
}

// Read-only: the container can never alter the model it serves.
resource environmentStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: environment
  name: 'models'
  properties: {
    azureFile: {
      accountName: storageAccountName
      accountKey: storage.listKeys().keys[0].value
      shareName: shareName
      accessMode: 'ReadOnly'
    }
  }
  dependsOn: [
    share
  ]
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      secrets: [
        {
          name: 'apikey'
          value: apiKey
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'afrislm'
          image: image
          // One request at a time and no streaming: the model is small and CPU bound, and a
          // translation is a short, complete answer. --no-mmap loads it fully into memory instead
          // of paging it over the file share.
          args: [
            '-m', '/models/${modelFile}'
            '--host', '0.0.0.0'
            '--port', '8080'
            '-c', '2048'
            '-t', '2'
            '-np', '1'
            '--no-mmap'
            '--no-webui'
          ]
          env: [
            {
              name: 'LLAMA_API_KEY'
              secretRef: 'apikey'
            }
          ]
          resources: {
            cpu: json('2.0')
            memory: '4Gi'
          }
          volumeMounts: [
            {
              volumeName: 'models'
              mountPath: '/models'
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 40
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              periodSeconds: 30
              timeoutSeconds: 10
              failureThreshold: 5
            }
          ]
        }
      ]
      // One replica, always on: loading the model takes tens of seconds, so scaling to zero would
      // make the first customer message after a quiet period wait for it.
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      volumes: [
        {
          name: 'models'
          storageType: 'AzureFile'
          storageName: environmentStorage.name
        }
      ]
    }
  }
}

output endpoint string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
