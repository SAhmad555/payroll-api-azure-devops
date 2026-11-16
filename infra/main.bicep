// =====================================================================================
// Umbrella Payroll API — per-environment infrastructure (dev / prod)
//
//   App Service (Linux, container) ── user-assigned managed identity ──┬─ AcrPull on shared registry
//                                                                      ├─ Key Vault Secrets User
//                                                                      └─ Entra ID login to Azure SQL
//   Application Insights (workspace-based) + Log Analytics + diagnostic settings
//
// Deploy:
//   az deployment group create -g rg-payroll-dev -f infra/main.bicep -p infra/main.dev.bicepparam \
//      -p containerImage=<acr>.azurecr.io/payroll-api:<sha>
// =====================================================================================

targetScope = 'resourceGroup'

@description('Short application name used in resource names.')
@minLength(3)
@maxLength(12)
param appName string = 'payroll'

@description('Deployment environment.')
@allowed([
  'dev'
  'prod'
])
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Full container image reference, e.g. myregistry.azurecr.io/payroll-api:1a2b3c4.')
param containerImage string

@description('Name of the (shared) Azure Container Registry that holds the image.')
param containerRegistryName string

@description('Resource group of the shared container registry (see infra/registry.bicep).')
param containerRegistryResourceGroup string

@description('Display name of the Microsoft Entra group that administers the SQL server.')
param sqlAdminGroupName string

@description('Object id of the Microsoft Entra group that administers the SQL server.')
param sqlAdminGroupObjectId string

@description('OIDC authority for JWT validation, e.g. https://login.microsoftonline.com/<tenant-id>/v2.0')
param apiAuthority string

@description('Expected audience (App ID URI or client id) of access tokens for this API.')
param apiAudience string

@description('Seed fictional demo data on startup. Keep false in production.')
param seedSampleData bool = false

@description('Tags applied to every resource.')
param tags object = {}

// ------------------------------------------------------------------ naming & sizing
var suffix = take(uniqueString(resourceGroup().id, appName, environmentName), 6)
var baseName = '${appName}-${environmentName}'
var isProd = environmentName == 'prod'
var allTags = union(tags, {
  application: 'umbrella-payroll-api'
  environment: environmentName
  'managed-by': 'bicep'
})

var appServiceSku = isProd ? { name: 'P0v3', tier: 'PremiumV3' } : { name: 'B1', tier: 'Basic' }
var sqlSku = isProd ? { name: 'S1', tier: 'Standard' } : { name: 'Basic', tier: 'Basic' }

// Built-in role definition ids
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ------------------------------------------------------------------ identity
resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${baseName}'
  location: location
  tags: allTags
}

// ------------------------------------------------------------------ observability
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${baseName}-${suffix}'
  location: location
  tags: allTags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: isProd ? 90 : 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${baseName}-${suffix}'
  location: location
  kind: 'web'
  tags: allTags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    DisableLocalAuth: false
  }
}

// ------------------------------------------------------------------ secrets
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${take(appName, 8)}-${environmentName}-${suffix}'
  location: location
  tags: allTags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: isProd ? 90 : 7
    enablePurgeProtection: isProd ? true : null
    publicNetworkAccess: 'Enabled'
  }
}

resource appInsightsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ApplicationInsightsConnectionString'
  properties: {
    value: appInsights.properties.ConnectionString
    contentType: 'text/plain'
  }
}

resource kvSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appIdentity.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
  }
}

// ------------------------------------------------------------------ container registry access (shared RG)
module acrPull 'modules/acr-pull-role.bicep' = {
  name: 'acrPull-${baseName}'
  scope: resourceGroup(containerRegistryResourceGroup)
  params: {
    registryName: containerRegistryName
    principalId: appIdentity.properties.principalId
  }
}

// ------------------------------------------------------------------ data
resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: 'sql-${baseName}-${suffix}'
  location: location
  tags: allTags
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true // no SQL logins / passwords at all
      login: sqlAdminGroupName
      sid: sqlAdminGroupObjectId
      principalType: 'Group'
      tenantId: subscription().tenantId
    }
  }
}

// Allows traffic from Azure services (App Service outbound). Production hardening would use
// VNet integration + a private endpoint and set publicNetworkAccess to 'Disabled'.
resource sqlAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: 'sqldb-${appName}'
  location: location
  tags: allTags
  sku: sqlSku
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
    requestedBackupStorageRedundancy: isProd ? 'Geo' : 'Local'
  }
}

// ------------------------------------------------------------------ compute
resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'asp-${baseName}'
  location: location
  tags: allTags
  kind: 'linux'
  sku: appServiceSku
  properties: {
    reserved: true // Linux
  }
}

var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabase.name};Authentication=Active Directory Managed Identity;User Id=${appIdentity.properties.clientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: 'app-${baseName}-${suffix}'
  location: location
  tags: allTags
  kind: 'app,linux,container'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: appIdentity.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerImage}'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: appIdentity.properties.clientId
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: '/health/ready'
      appSettings: [
        { name: 'WEBSITES_PORT', value: '8080' }
        { name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE', value: 'false' }
        { name: 'ASPNETCORE_ENVIRONMENT', value: isProd ? 'Production' : 'Staging' }
        { name: 'Database__Provider', value: 'SqlServer' }
        { name: 'Database__InitializeOnStartup', value: 'true' }
        { name: 'Database__SeedSampleData', value: string(seedSampleData) }
        { name: 'ConnectionStrings__Payroll', value: sqlConnectionString }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: '@Microsoft.KeyVault(SecretUri=${appInsightsSecret.properties.secretUri})' }
        { name: 'Auth__Enabled', value: 'true' }
        { name: 'Auth__Authority', value: apiAuthority }
        { name: 'Auth__Audience', value: apiAudience }
      ]
    }
  }
  dependsOn: [
    acrPull
    kvSecretsUser
  ]
}

resource webAppDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: webApp
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      { category: 'AppServiceHTTPLogs', enabled: true }
      { category: 'AppServiceConsoleLogs', enabled: true }
      { category: 'AppServiceAppLogs', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

// ------------------------------------------------------------------ outputs (consumed by the pipelines)
output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output managedIdentityName string = appIdentity.name
output managedIdentityClientId string = appIdentity.properties.clientId
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output keyVaultName string = keyVault.name
output appInsightsName string = appInsights.name
