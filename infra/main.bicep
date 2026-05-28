targetScope = 'resourceGroup'

@description('Base name for resources (lowercase alphanumeric, 3-20 chars).')
param environmentName string = 'intranet'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('PostgreSQL administrator login.')
param postgresAdminLogin string = 'intranetadmin'

@secure()
@description('PostgreSQL administrator password.')
param postgresAdminPassword string

var uniqueSuffix = uniqueString(resourceGroup().id, location)
var namePrefix = '${environmentName}-${uniqueSuffix}'
var tags = {
  application: 'intranet'
  environment: 'dev'
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    location: location
    namePrefix: namePrefix
    tags: tags
  }
}

module postgres 'modules/postgresql.bicep' = {
  name: 'postgresql'
  params: {
    location: location
    namePrefix: namePrefix
    adminLogin: postgresAdminLogin
    adminPassword: postgresAdminPassword
    tags: tags
  }
}

module app 'modules/app-service.bicep' = {
  name: 'app-service'
  params: {
    location: location
    namePrefix: namePrefix
    tags: tags
    applicationInsightsConnectionString: ''
    postgresConnectionString: postgres.outputs.connectionString
  }
}

module keyVaultAccess 'modules/key-vault-access.bicep' = {
  name: 'key-vault-access'
  params: {
    keyVaultName: keyVault.outputs.keyVaultName
    webAppPrincipalId: app.outputs.webAppPrincipalId
    postgresConnectionString: postgres.outputs.connectionString
  }
}

output webAppUrl string = app.outputs.webAppUrl
output webAppName string = app.outputs.webAppName
output postgresServerName string = postgres.outputs.serverName
output keyVaultName string = keyVault.outputs.keyVaultName
output resourceGroupName string = resourceGroup().name
