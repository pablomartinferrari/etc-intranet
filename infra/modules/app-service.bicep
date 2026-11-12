param location string
param namePrefix string
param tags object
param applicationInsightsConnectionString string
@secure()
param postgresConnectionString string

var planName = '${namePrefix}-plan'
var appName = take('${namePrefix}-api', 60)

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  tags: union(tags, {
    'azd-service-name': 'api'
  })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      healthCheckPath: '/health/live'
      appSettings: concat([
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ConnectionStrings__IntranetDb'
          value: postgresConnectionString
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'ASPNETCORE_URLS'
          value: 'http://+:8080'
        }
        {
          name: 'WEBSITE_LOGGING_ENABLED'
          value: '1'
        }
      ], empty(applicationInsightsConnectionString) ? [] : [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
      ])
    }
  }
}

output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output webAppPrincipalId string = webApp.identity.principalId
