using 'main.bicep'

// Placeholder values — replace with your own tenant / group ids before deploying.
// containerImage is supplied by the pipeline at deploy time (-p containerImage=...).

param environmentName = 'prod'
param appName = 'payroll'
param containerImage = 'mcr.microsoft.com/dotnet/samples:aspnetapp' // overridden by CI/CD
param containerRegistryName = 'crpayrollshared'
param containerRegistryResourceGroup = 'rg-payroll-shared'
param sqlAdminGroupName = 'sg-payroll-sql-admins'
param sqlAdminGroupObjectId = '00000000-0000-0000-0000-000000000000'
param apiAuthority = 'https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0'
param apiAudience = 'api://payroll-api'
param seedSampleData = false
param tags = {
  costCenter: 'portfolio-demo'
}
