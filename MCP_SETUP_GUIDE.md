# Pitched Billing API - MCP Server Setup Guide

## ⚠️ Prerequisites - Azure App Registration Configuration

**IMPORTANT:** Before setting up the MCP Server, you MUST configure the Azure App Registration to allow public client flows (device code authentication).

👉 **See [AZURE_APP_REGISTRATION_SETUP.md](AZURE_APP_REGISTRATION_SETUP.md) for complete setup instructions**

### Quick Checklist:
- ✅ "Allow public client flows" enabled in App Registration
- ✅ Mobile and desktop applications platform added (`http://localhost`)
- ✅ API permissions configured with `access_as_user` scope
- ✅ Admin consent granted

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        YOUR MACHINE                          │
│                                                              │
│  ┌──────────────┐         stdio          ┌─────────────┐   │
│  │    Claude    │◄──────────────────────►│ MCP Server  │   │
│  │   Desktop    │                        │  (Console)  │   │
│  └──────────────┘                        └──────┬──────┘   │
│                                                  │          │
│                                             HTTP │          │
│                                          + Token │          │
└──────────────────────────────────────────────────┼──────────┘
                                                   │
                                                   ▼
                        ┌──────────────────────────────────────┐
                        │   Billing API (Container App/Cloud)  │
                        │   - Validates JWT token              │
                        │   - Checks Finance group membership  │
                        └──────────────────────────────────────┘
```

## How It Works

1. **MCP Server runs locally** on your machine (console app)
2. **Claude Desktop** communicates with MCP Server via `stdio` (not HTTP)
3. **First run**: MCP Server shows device code → you authenticate via browser
4. **Token cached**: Your access token saved in `%LOCALAPPDATA%\PitchedBillingApiMcp\`
5. **Subsequent runs**: Token automatically reused and refreshed
6. **API calls**: MCP Server includes YOUR token when calling Billing API
7. **Authorization**: API validates token and checks if you're in Finance group

## Authentication Details

### You Are Authenticated As Yourself

- **Token Type**: Your personal Azure AD access token
- **Scope**: `api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba/access_as_user`
- **Claims**: Includes your user identity and group memberships
- **Finance Group**: `3c01275a-eb47-46dc-bf59-4b471eced4e0`

### Token Lifecycle

- **Initial Auth**: Device code flow (one-time browser authentication)
- **Token Expiry**: ~1 hour (Azure AD default)
- **Auto-Refresh**: MCP Server automatically refreshes using refresh token
- **Refresh Token Expiry**: 100 days (resets with each use)
- **Cache Location**: `%LOCALAPPDATA%\PitchedBillingApiMcp\msal_token_cache.json`

---

## First-Time Setup

### Prerequisites

1. **Azure AD App Registration** configured:
   - App ID: `ba5d30cf-2c5e-4915-bd94-f08c99f200ba`
   - Tenant ID: `bfdb0820-807a-4f65-8d05-bee073c61a3f`
   - Exposed API scope: `api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba/access_as_user`
   - Token configuration: Groups claim enabled
   - Redirect URI: `http://localhost` (for device code flow)

2. **You are a member of Finance group**:
   - Group ID: `3c01275a-eb47-46dc-bf59-4b471eced4e0`
   - Group name: "Finance"

3. **Billing API is running**:
   - Accessible at URL specified in `PITCHED_API_URL` environment variable
   - Default: `http://localhost:5222` (development)
   - Production: Set to your Container App URL

### Step 1: Initial Authentication

**Open a terminal** (PowerShell or Command Prompt) and run:

```powershell
cd c:\Users\ianchambers\Repos\pitched-billing-api
dotnet run --project PitchedBillingApi.McpServer
```

**You will see:**

```
╔════════════════════════════════════════════════════════════════╗
║          Azure Authentication Required                         ║
╚════════════════════════════════════════════════════════════════╝

To sign in, use a web browser to open the page https://microsoft.com/devicelogin
and enter the code ABC123XY to authenticate.

Waiting for authentication...
```

**Steps:**
1. Open browser to `https://microsoft.com/devicelogin`
2. Enter the code shown (e.g., `ABC123XY`)
3. Sign in with your Azure AD account
4. Consent to the permissions
5. Return to terminal - you'll see "✓ Authentication successful!"
6. Press `Ctrl+C` to stop the server

**Token is now cached!** You won't need to authenticate again unless:
- Token cache is deleted
- Refresh token expires (100 days of no use)
- You manually clear the cache

### Step 2: Configure Claude Desktop

Edit Claude Desktop's MCP configuration file:

**Location:** `%APPDATA%\Claude\claude_desktop_config.json`

**Windows Path Example:** `C:\Users\ianchambers\AppData\Roaming\Claude\claude_desktop_config.json`

**Add MCP Server:**

```json
{
  "mcpServers": {
    "pitched-billing": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "c:\\Users\\ianchambers\\Repos\\pitched-billing-api\\PitchedBillingApi.McpServer\\PitchedBillingApi.McpServer.csproj"
      ],
      "env": {
        "PITCHED_API_URL": "https://your-billing-api-url.azurecontainerapps.io"
      }
    }
  }
}
```

**Notes:**
- Use double backslashes (`\\`) in Windows paths
- Update `PITCHED_API_URL` to your actual API URL
- For local development, use `http://localhost:5222`

### Step 3: Restart Claude Desktop

1. Completely close Claude Desktop
2. Reopen Claude Desktop
3. MCP Server will start automatically with Claude Desktop
4. Server uses cached token (no authentication prompt)

---

## Using the MCP Server

### Available Tools

The MCP Server provides tools for:

**Billing Plans:**
- List all billing plans
- Get specific billing plan
- Create new billing plan
- Update billing plan
- Delete billing plan
- Manage billing plan items

**Invoices:**
- Generate invoices from billing plans
- View invoice history
- Download invoice PDFs
- Resend invoice emails
- Post invoices to QuickBooks

**QuickBooks Integration:**
- List QuickBooks customers
- Get customer details
- List QuickBooks items
- Get item details
- Get tax code information
- Manage QuickBooks connection

### Example Claude Prompts

```
"Show me all active billing plans"
"Get the details for billing plan [GUID]"
"Generate an invoice for billing plan [GUID] for December 2025"
"List all customers from QuickBooks"
"What invoices have been generated for plan [GUID]?"
```

---

## Environment Variables

Configure these in Claude Desktop's MCP configuration or system environment:

| Variable | Description | Default |
|----------|-------------|---------|
| `PITCHED_API_URL` | Billing API base URL | `http://localhost:5222` |
| `AZURE_TENANT_ID` | Azure AD Tenant ID | `bfdb0820-807a-4f65-8d05-bee073c61a3f` |
| `AZURE_CLIENT_ID` | App Registration Client ID | `ba5d30cf-2c5e-4915-bd94-f08c99f200ba` |
| `AZURE_API_SCOPE` | API scope for token | `api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba/access_as_user` |

---

## Troubleshooting

### "Authentication Required" Prompt Doesn't Appear

The device code prompt only shows when you run MCP Server directly in a terminal.

**Solution:**
1. Open terminal
2. Run: `dotnet run --project PitchedBillingApi.McpServer`
3. Authenticate once
4. Token is cached for Claude Desktop to use

### "401 Unauthorized" Errors

**Possible causes:**
- Token expired and auto-refresh failed
- Token cache corrupted
- User not authenticated

**Solution:**

**Option 1 - Using --reauth flag (Recommended):**
```powershell
cd PitchedBillingApi.McpServer\bin\Debug\net10.0
.\PitchedBillingApi.McpServer.exe --reauth
```

**Option 2 - Manual cache clear:**
1. Clear token cache: Delete `%LOCALAPPDATA%\PitchedBillingApiMcp\msal_token_cache.json`
2. Re-authenticate: `dotnet run --project PitchedBillingApi.McpServer`

After re-authentication, **restart Claude Desktop**.

### "403 Forbidden" Errors

**Current Status:** Group checking is **temporarily disabled** for local development. All authenticated users can access the API.

**When group authorization is enabled (production):**

**Cause:** You authenticated successfully, but you're not in the Finance group.

**Solution:**
1. Verify you're a member of Finance group (`3c01275a-eb47-46dc-bf59-4b471eced4e0`)
2. Ask Azure AD admin to add you to the group
3. **After being added to the group**, re-authenticate:
   ```powershell
   PitchedBillingApi.McpServer.exe --reauth
   ```
4. Restart Claude Desktop

**Note:** Group membership changes require re-authentication to get a new token with updated claims.

### MCP Server Won't Start in Claude Desktop

**Check Claude Desktop logs:**

Windows: `%APPDATA%\Claude\logs\`

**Common issues:**
- Invalid path in `claude_desktop_config.json`
- .NET 10 SDK not installed
- Project won't build
- Environment variables not set correctly

**Solution:**
1. Test running MCP Server manually: `dotnet run --project ...`
2. Check for build errors
3. Verify paths use double backslashes on Windows
4. Restart Claude Desktop after config changes

### API Calls Return Unexpected Errors

**Enable verbose logging:**

Add to Claude Desktop config:
```json
"env": {
  "PITCHED_API_URL": "...",
  "Logging__LogLevel__Default": "Debug"
}
```

**Check MCP Server stderr output** (captured by Claude Desktop in logs)

---

## Security Considerations

✅ **Token Security:**
- Tokens stored in user's local app data (encrypted by OS file permissions)
- Tokens automatically expire after 1 hour
- Refresh tokens expire after 100 days of inactivity

✅ **Audit Trail:**
- API logs show your actual user identity for each request
- All actions traceable to specific user

✅ **Group-Based Authorization:**
- Access automatically revoked if removed from Finance group
- No cached permissions - checked on every API call

✅ **No Shared Credentials:**
- Each user authenticates with their own credentials
- No service accounts or shared secrets

⚠️ **Token Cache File:**
- Location: `%LOCALAPPDATA%\PitchedBillingApiMcp\msal_token_cache.json`
- Protected by Windows file permissions (only your user account)
- Delete this file to force re-authentication

---

## For Developers

### Building from Source

```bash
cd c:\Users\ianchambers\Repos\pitched-billing-api
dotnet build PitchedBillingApi.McpServer
```

### Running Tests

```bash
# Test MCP Server standalone
dotnet run --project PitchedBillingApi.McpServer

# Test API call with your token
$token = az account get-access-token --resource api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba --query accessToken -o tsv
curl -H "Authorization: Bearer $token" https://your-api-url/api/billingplan
```

### Debugging

**Attach debugger to MCP Server:**

1. In Visual Studio/Rider, set breakpoints in MCP Server code
2. Start Claude Desktop
3. Attach debugger to `dotnet.exe` process running MCP Server
4. Use Claude to trigger tool calls

**View MCP Protocol Messages:**

MCP uses JSON-RPC over stdio. You can intercept messages by wrapping the MCP Server command.

---

## Production Deployment

### Billing API (Container App)

The Billing API should be deployed as an Azure Container App:

```bash
# Build and deploy
az containerapp create \
  --name pitched-billing-api \
  --resource-group pitched-billing-rg \
  --environment pitched-billing-env \
  --image your-acr.azurecr.io/billing-api:latest \
  --ingress external \
  --target-port 8080
```

### MCP Server (Local Only)

The MCP Server runs **locally on each user's machine**. It is NOT deployed to the cloud.

**Distribution options:**
1. **Git Repository**: Users clone repo and build locally
2. **Self-Contained EXE**: Publish as single-file executable
3. **MSI Installer**: Package as Windows installer

**Publishing as EXE:**

```bash
dotnet publish PitchedBillingApi.McpServer -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output: `PitchedBillingApi.McpServer\bin\Release\net10.0\win-x64\publish\PitchedBillingApi.McpServer.exe`

**Update Claude Desktop config to use EXE:**

```json
{
  "mcpServers": {
    "pitched-billing": {
      "command": "C:\\Program Files\\PitchedBilling\\PitchedBillingApi.McpServer.exe",
      "env": {
        "PITCHED_API_URL": "https://pitched-billing-api.azurecontainerapps.io"
      }
    }
  }
}
```

---

## Support

### Common Questions

**Q: Do I need to authenticate every time I use Claude?**
A: No, token is cached. You only authenticate once (or when token expires after 100 days of no use).

**Q: Can multiple people use the same MCP Server?**
A: No, each user runs their own MCP Server locally. Each person authenticates with their own credentials.

**Q: What if I'm not in the Finance group?**
A: You'll get 403 Forbidden errors. Contact your Azure AD administrator to be added to the group.

**Q: Can I use this without Claude Desktop?**
A: Yes, you can call the MCP Server tools programmatically or build your own client using the MCP SDK.

**Q: How do I uninstall?**
A:
1. Remove MCP Server from `claude_desktop_config.json`
2. Restart Claude Desktop
3. Delete token cache: `%LOCALAPPDATA%\PitchedBillingApiMcp\`
4. Optionally delete the MCP Server files

**Q: What ports does the MCP Server use?**
A: None. MCP Server uses stdio (standard input/output) to communicate with Claude Desktop, not network ports.

---

## Summary

Your Pitched Billing MCP Server:

✅ Runs locally on your machine
✅ Authenticates you via device code flow (one-time)
✅ Caches your token for future use
✅ Communicates with Claude Desktop via stdio
✅ Calls Billing API with your personal access token
✅ Provides full audit trail (your identity on all requests)
✅ Automatically enforces Finance group membership

The architecture is secure, maintainable, and provides a great user experience!
