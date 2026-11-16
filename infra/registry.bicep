// One-time, shared container registry (build once → promote the same image to dev and prod).
//
//   az group create -n rg-payroll-shared -l uksouth
//   az deployment group create -g rg-payroll-shared -f infra/registry.bicep -p registryName=<globally-unique-name>

@description('Globally unique registry name (5-50 alphanumerics).')
@minLength(5)
@maxLength(50)
param registryName string

param location string = resourceGroup().location

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false // pulls use managed identity, pushes use the pipeline's Entra identity
    publicNetworkAccess: 'Enabled'
  }
  tags: {
    application: 'umbrella-payroll-api'
    'managed-by': 'bicep'
  }
}

output loginServer string = registry.properties.loginServer
