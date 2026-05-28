using 'main.bicep'

param environmentName = 'intranet'
param location = 'eastus2'
param postgresAdminLogin = 'intranetadmin'
// Override at deploy time: --parameters postgresAdminPassword=<secure-password>
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD', 'ChangeMe-Dev-Only-123!')
