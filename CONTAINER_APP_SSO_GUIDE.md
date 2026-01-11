# Azure Container App with Claude Desktop SSO Integration

## Overview

This guide explains how to configure Claude Desktop to work with your Azure Container Apps using SSO token passthrough. Since you're logged into Claude with corporate SSO, we can leverage that authentication instead of implementing device code flow.

## Architecture

```
User (Azure AD SSO)
    ↓
Claude Desktop (has user's access token)
    ↓ [HTTP + Bearer Token]
Container App "Gateway" (token passthrough)
    ↓ [HTTP + same Bearer Token]
Billing API (validates token + Finance group)
```

## Key Concept: Token Passthrough

When you're logged into Claude Desktop with SSO:
1. Claude Desktop has your Azure AD access token
2. When it calls external services, it can include your token in the `Authorization` header
3. Your container app forwards that token to the Billing API
4. Billing API validates the token and checks Finance group membership

**Result:** Each user authenticates as themselves, real audit trail, automatic group-based authorization

---

## Implementation Options

### Option A: MCP SDK-based (Recommended for Future)

Wait for MCP SDK to support HTTP/SSE transport (currently in preview). When available:
- Full MCP protocol support
- Tool discovery
- Native Claude Desktop integration

### Option B: Simple HTTP Gateway (Works Today)

Create a lightweight ASP.NET Core API that:
- Accepts HTTP requests from Claude Desktop
- Extracts Bearer token from Authorization header
- Forwards requests to Billing API with same token
- Returns JSON responses

**This is the simplest approach and works with Claude Desktop's existing HTTP capabilities.**

---

## Configuration Steps for Option B (HTTP Gateway)

### 1. Create Simple Gateway API

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("BillingAPI", client =>
{
    var apiUrl = Environment.GetEnvironmentVariable("BILLING_API_URL");
    client.BaseAddress = new Uri(apiUrl);
})
.AddHttpMessageHandler<TokenForwardingHandler>();

builder.Services.AddTransient<TokenForwardingHandler>();

var app = builder.Build();

// Proxy endpoints
app.MapGet("/api/billingplans", async (IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("BillingAPI");
    return await client.GetFromJsonAsync<object>("/api/billingplan");
});

app.MapGet("/api/billingplans/{id}", async (string id, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("BillingAPI");
    return await client.GetFromJsonAsync<object>($"/api/billingplan/{id}");
});

// ... more endpoints ...

app.Run();
```

```csharp
// TokenForwardingHandler.cs
public class TokenForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TokenForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authHeader = _httpContextAccessor.HttpContext
            ?.Request.Headers["Authorization"].FirstOrDefault();

        if (!string.IsNullOrEmpty(authHeader))
        {
            request.Headers.Add("Authorization", authHeader);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

### 2. Deploy to Azure Container Apps

```bash
# Build and push
docker build -t acr.azurecr.io/billing-gateway:latest .
docker push acr.azurecr.io/billing-gateway:latest

# Deploy
az containerapp create \
  --name billing-gateway \
  --resource-group pitched-billing-rg \
  --environment pitched-billing-env \
  --image acr.azurecr.io/billing-gateway:latest \
  --target-port 8080 \
  --ingress external \
  --env-vars BILLING_API_URL=https://your-billing-api.azurewebsites.net
```

### 3. Update Billing API CORS

```bash
az containerapp update \
  --name pitched-billing-api \
  --resource-group pitched-billing-rg \
  --set-env-vars MCP_SERVER_URL=https://billing-gateway.azurecontainerapps.io
```

### 4. Configure Claude Desktop

Two configuration approaches:

**A. Direct API Calls (if Claude supports it):**
```json
{
  "externalAPIs": {
    "pitchedBilling": {
      "baseUrl": "https://billing-gateway.azurecontainerapps.io",
      "authentication": "sso-passthrough"
    }
  }
}
```

**B. Custom Instructions to Claude:**
Simply tell Claude about your API:

```
You have access to a billing API at https://billing-gateway.azurecontainerapps.io

Available endpoints:
- GET /api/billingplans - List all billing plans
- GET /api/billingplans/{id} - Get specific plan
- POST /api/invoices/generate - Generate invoice
- GET /api/invoices/plan/{planId} - Get invoice history
- GET /api/quickbooks/customers - List QuickBooks customers

When making requests, include my SSO token in the Authorization header.
```

---

## Azure AD App Registration Configuration

### Required Configuration

1. **Expose an API**:
   - Add scope: `api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba/access_as_user`
   - Scope name: `access_as_user`
   - Who can consent: Admins and users
   - Admin consent display name: "Access billing API as user"
   - Admin consent description: "Allows the app to access the billing API on behalf of the signed-in user"

2. **Token Configuration**:
   - Add groups claim
   - Select: Security groups
   - Token types: Access tokens

3. **API Permissions** (for Claude Desktop):
   - Add permission → My APIs → Pitched Billing API
   - Delegated permissions: `access_as_user`
   - Grant admin consent

4. **Authentication**:
   - Platform: Single-page application or Web (depending on Claude Desktop's auth method)
   - Redirect URIs: (provided by Claude Desktop configuration)

### Test Token

To test your token configuration:

```bash
# Get token for your user
az account get-access-token --resource api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba

# Decode at jwt.ms to verify:
# - aud claim: api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba
# - groups claim: includes 3c01275a-eb47-46dc-bf59-4b471eced4e0
```

---

## Testing the Flow

### 1. Test Gateway (without Claude)

```bash
# Get your SSO token
TOKEN=$(az account get-access-token --resource api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba --query accessToken -o tsv)

# Call gateway
curl -H "Authorization: Bearer $TOKEN" \
  https://billing-gateway.azurecontainerapps.io/api/billingplans
```

### 2. Verify Token Forwarding

Check logs to ensure:
- Gateway receives token from Claude Desktop
- Gateway forwards token to Billing API
- API validates token successfully

```bash
# Gateway logs
az containerapp logs show --name billing-gateway --resource-group pitched-billing-rg --follow

# API logs
az containerapp logs show --name pitched-billing-api --resource-group pitched-billing-rg --follow
```

### 3. Test with Different Users

- **User in Finance group**: Should get 200 OK
- **User NOT in Finance group**: Should get 403 Forbidden
- **No token**: Should get 401 Unauthorized

---

## Security Benefits

✅ **Real User Identity**: Each request is made with actual user's token
✅ **Audit Trail**: Logs show which user performed each action
✅ **Group-Based Access**: Automatic enforcement via Finance group membership
✅ **No Stored Credentials**: Gateway never stores tokens
✅ **Token Expiration**: Tokens automatically expire (typically 1 hour)
✅ **Revocation**: If user removed from Finance group, access revoked immediately

---

## Alternative: Service Principal (If User SSO Not Available)

If Claude Desktop doesn't support user SSO passthrough, use a service principal:

```bash
# Create service principal for gateway
az ad sp create-for-rbac --name "Billing Gateway SP"

# Add to Finance group
az ad group member add \
  --group 3c01275a-eb47-46dc-bf59-4b471eced4e0 \
  --member-id <service-principal-object-id>

# Store credentials in Container App
az containerapp update \
  --name billing-gateway \
  --set-env-vars \
    AZURE_CLIENT_ID=<sp-client-id> \
    AZURE_CLIENT_SECRET=<sp-secret> \
    AZURE_TENANT_ID=bfdb0820-807a-4f65-8d05-bee073c61a3f
```

Then gateway uses client credentials flow to get its own token.

**Downside**: All requests appear to come from the service principal, losing individual user audit trail.

---

## Recommended Next Steps

1. **Confirm Claude Desktop SSO Support**: Check if Claude Desktop can pass user's SSO token to external APIs
2. **Choose Implementation**: HTTP Gateway (Option B) is simplest and works today
3. **Deploy Gateway**: Small ASP.NET Core app that proxies requests
4. **Test Flow**: Verify token passthrough works correctly
5. **Configure CORS**: Ensure Billing API allows requests from Gateway
6. **Document for Users**: Explain which Claude prompts trigger which API calls

---

## Questions to Answer

Before implementing, clarify with Claude/Anthropic:

1. Does Claude Desktop support passing user's SSO token to external HTTP APIs?
2. What configuration format does Claude Desktop use for external API integrations?
3. Is there a specific authentication header format required?
4. Does Claude support the MCP protocol over HTTP/SSE yet?

Once these are answered, the implementation becomes straightforward!
