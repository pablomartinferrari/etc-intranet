@description('Deploy knowledge-base Azure resources (Postgres + Blob). Ollama GPU VM is provisioned separately — see docs/knowledge-base-azure.md')
param location string = resourceGroup().location
param namePrefix string
param postgresAdminLogin string
@secure()
param postgresAdminPassword string
param tags object = {}

module knowledgePostgres 'knowledge-postgresql.bicep' = {
  name: 'knowledge-postgresql'
  params: {
    location: location
    namePrefix: namePrefix
    adminLogin: postgresAdminLogin
    adminPassword: postgresAdminPassword
    tags: tags
  }
}

module knowledgeStorage 'knowledge-storage.bicep' = {
  name: 'knowledge-storage'
  params: {
    location: location
    namePrefix: namePrefix
    tags: tags
  }
}

output knowledgePostgresConnectionString string = knowledgePostgres.outputs.connectionString
output knowledgeStorageConnectionString string = knowledgeStorage.outputs.connectionString
output knowledgePostgresFqdn string = knowledgePostgres.outputs.fqdn
