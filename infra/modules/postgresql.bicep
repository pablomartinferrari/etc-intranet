param location string
param namePrefix string
param adminLogin string
@secure()
param adminPassword string
param tags object

var serverName = take('${namePrefix}-pg', 63)

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2022-12-01' = {
  name: serverName
  location: location
  tags: tags
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
  }
}

resource allowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2022-12-01' = {
  parent: postgresServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2022-12-01' = {
  parent: postgresServer
  name: 'intranet'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

var connectionString = 'Host=${postgresServer.properties.fullyQualifiedDomainName};Port=5432;Database=intranet;Username=${adminLogin};Password=${adminPassword};SSL Mode=Require;Trust Server Certificate=true'

output serverName string = postgresServer.name
output fqdn string = postgresServer.properties.fullyQualifiedDomainName
output connectionString string = connectionString
