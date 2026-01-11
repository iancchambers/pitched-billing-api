# MCP Server - Azure Container App Deployment Guide

## Overview

The MCP Server runs as an Azure Container App and uses SSO token passthrough from Claude Desktop. Users authenticate with their corporate Azure AD credentials through Claude, and the MCP server forwards their token to the Billing API for authorization.

## Authentication Flow

```
User (SSO) → Claude Desktop → MCP Server (Container App) → Billing API
             [passes token]    [forwards token]            [validates + checks Finance group]
```

## Prerequisites

1. Azure Subscription
2. Azure Container Registry (ACR)
3. Resource Group for Container Apps
4. Billing API deployed and accessible
5. Azure AD App Registration configured (already done: `ba5d30cf-2c5e-4915-bd94-f08c99f200ba`)

## Step 1: Create Azure Container Registry

```bash
# Create resource group (if not exists)
az group create --name pitched-billing-rg --location eastus

# Create container registry
az acr create \
  --resource-group pitched-billing-rg \
  --name pitchedbillingmcp \
  --sku Basic \
  --admin-enabled true
```

## Step 2: Build and Push Docker Image

```bash
# Navigate to repo root
cd c:\Users\ianchambers\Repos\pitched-billing-api

# Login to ACR
az acr login --name pitchedbillingmcp

# Build and push image
docker build -f PitchedBillingApi.McpServer/Dockerfile -t pitchedbillingmcp.azurecr.io/mcp-server:latest .
docker push pitchedbillingmcp.azurecr.io/mcp-server:latest
```

## Step 3: Create Container App Environment

```bash
# Create Container Apps environment
az containerapp env create \
  --name pitched-billing-env \
  --resource-group pitched-billing-rg \
  --location eastus
```

## Step 4: Deploy Container App

```bash
# Get ACR credentials
ACR_USERNAME=$(az acr credential show --name pitchedbillingmcp --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name pitchedbillingmcp --query passwords[0].value -o tsv)

# Create container app
az containerapp create \
  --name pitched-billing-mcp \
  --resource-group pitched-billing-rg \
  --environment pitched-billing-env \
  --image pitchedbillingmcp.azurecr.io/mcp-server:latest \
  --target-port 8080 \
  --ingress external \
  --registry-server pitchedbillingmcp.azurecr.io \
  --registry-username $ACR_USERNAME \
  --registry-password $ACR_PASSWORD \
  --env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    AZURE_TENANT_ID=bfdb0820-807a-4f65-8d05-bee073c61a3f \
    AZURE_CLIENT_ID=ba5d30cf-2c5e-4915-bd94-f08c99f200ba \
    PITCHED_API_URL=https://your-billing-api-url.azurewebsites.net \
    ASPNETCORE_URLS=http://+:8080 \
  --min-replicas 1 \
  --max-replicas 3
```

## Step 5: Get Container App URL

```bash
# Get the FQDN
az containerapp show \
  --name pitched-billing-mcp \
  --resource-group pitched-billing-rg \
  --query properties.configuration.ingress.fqdn \
  --output tsv
```

Example output: `https://pitched-billing-mcp.kindbeach-12345678.eastus.azurecontainerapps.io`

## Step 6: Configure Billing API CORS

Set environment variable on the Billing API Container App:

```bash
az containerapp update \
  --name pitched-billing-api \
  --resource-group pitched-billing-rg \
  --set-env-vars MCP_SERVER_URL=https://pitched-billing-mcp.kindbeach-12345678.eastus.azurecontainerapps.io
```

## Step 7: Configure Claude Desktop

Update your Claude Desktop configuration file:

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "pitched-billing": {
      "url": "https://pitched-billing-mcp.kindbeach-12345678.eastus.azurecontainerapps.io/mcp"
    }
  }
}
```

## Step 8: Test the Integration

1. **Open Claude Desktop** and sign in with your Azure AD account (must be member of Finance group)
2. **Ask Claude**: "List all billing plans"
3. Claude will call the MCP server with your SSO token
4. MCP server forwards token to API
5. API validates token and checks Finance group membership
6. Results are returned to Claude

## Monitoring & Logs

```bash
# View logs
az containerapp logs show \
  --name pitched-billing-mcp \
  --resource-group pitched-billing-rg \
  --follow

# View metrics
az containerapp show \
  --name pitched-billing-mcp \
  --resource-group pitched-billing-rg
```

## Updating the Container App

```bash
# Rebuild and push image
docker build -f PitchedBillingApi.McpServer/Dockerfile -t pitchedbillingmcp.azurecr.io/mcp-server:latest .
docker push pitchedbillingmcp.azurecr.io/mcp-server:latest

# Container App will auto-update, or force restart
az containerapp revision restart \
  --name pitched-billing-mcp \
  --resource-group pitched-billing-rg
```

## Security Notes

- **No credentials stored** - Users authenticate via Claude Desktop SSO
- **Token passthrough** - MCP server never stores tokens
- **Group-based auth** - API validates Finance group membership on every request
- **HTTPS only** - Container App ingress is configured for HTTPS
- **Audit trail** - Each API call is logged with actual user identity

## Troubleshooting

### 401 Unauthorized
- Verify user is signed into Claude Desktop with Azure AD
- Check user is member of Finance group (`3c01275a-eb47-46dc-bf59-4b471eced4e0`)
- Verify App Registration has correct scopes exposed

### 403 Forbidden
- User authenticated but not in Finance group
- Add user to Finance group in Azure AD

### CORS Errors
- Verify `MCP_SERVER_URL` environment variable is set on Billing API
- Check CORS configuration in Billing API Program.cs

### Container App Not Starting
- Check logs: `az containerapp logs show --name pitched-billing-mcp --resource-group pitched-billing-rg`
- Verify environment variables are set correctly
- Check container image exists in ACR
