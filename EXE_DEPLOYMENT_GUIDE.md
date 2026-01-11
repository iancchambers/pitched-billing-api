# MCP Server - EXE Deployment Guide

## ⚠️ Prerequisites - Azure App Registration Configuration

**IMPORTANT:** Before deploying or testing the MCP Server, you MUST configure the Azure App Registration to allow public client flows (device code authentication).

👉 **See [AZURE_APP_REGISTRATION_SETUP.md](AZURE_APP_REGISTRATION_SETUP.md) for complete setup instructions**

### Quick Checklist:
- ✅ "Allow public client flows" enabled in App Registration
- ✅ Mobile and desktop applications platform added (`http://localhost`)
- ✅ API permissions configured with `access_as_user` scope
- ✅ Admin consent granted

**Without this configuration, authentication will fail with error:**
```
AADSTS7000218: The request body must contain the following parameter: 'client_assertion' or 'client_secret'
```

---

## Building the EXE

### Step 1: Publish as Self-Contained EXE

```powershell
cd c:\Users\ianchambers\Repos\pitched-billing-api

dotnet publish PitchedBillingApi.McpServer `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true
```

**Output location:**
```
PitchedBillingApi.McpServer\bin\Release\net10.0\win-x64\publish\PitchedBillingApi.McpServer.exe
```

### Step 2: Test the EXE

```powershell
cd PitchedBillingApi.McpServer\bin\Release\net10.0\win-x64\publish
.\PitchedBillingApi.McpServer.exe --help
```

---

## User Setup Process

### First-Time Setup (Before Claude Desktop)

**Users must authenticate BEFORE using Claude Desktop.** Run this command once:

```powershell
PitchedBillingApi.McpServer.exe --authenticate
```

**What happens:**

```
======================================================================
Pitched Billing MCP Server - Authentication Setup
======================================================================

╔════════════════════════════════════════════════════════════════╗
║          Azure Authentication Required                         ║
╚════════════════════════════════════════════════════════════════╝

To sign in, use a web browser to open the page https://microsoft.com/devicelogin
and enter the code ABC123XY to authenticate.

Waiting for authentication...
```

**Steps:**
1. Open browser to `https://microsoft.com/devicelogin`
2. Enter the code shown
3. Sign in with Azure AD account
4. Consent to permissions
5. Return to terminal

**Success:**

```
======================================================================
✓ Authentication successful!
======================================================================

Your credentials have been cached. Claude Desktop can now use the
MCP Server without requiring authentication prompts.

Token cache location:
  C:\Users\YourName\AppData\Local\PitchedBillingApiMcp

You can now close this window and start Claude Desktop.
```

---

### Re-Authentication (Switching Accounts or Updating Permissions)

If you need to switch to a different Azure AD account, clear corrupted cache, or refresh after group membership changes (e.g., added to Finance group):

```powershell
PitchedBillingApi.McpServer.exe --reauth
```

**What happens:**
- Clears existing cached credentials
- Forces a new device code authentication
- Caches the new token

**When to use:**
- 🔄 Switching to a different Azure AD user
- 🔧 Token cache is corrupted
- 👥 Your group membership changed (e.g., added to Finance group)
- 🧪 Testing with different user accounts

**Example output:**
```
======================================================================
Pitched Billing MCP Server - Re-Authentication
======================================================================

Clearing cached credentials...
✓ Cache cleared

[Device code prompt appears]

======================================================================
✓ Re-authentication successful!
======================================================================

Your new credentials have been cached. Claude Desktop can now use
the MCP Server with your updated authentication.

You can now close this window and restart Claude Desktop.
```

After re-authentication, **restart Claude Desktop** to use the new token.

---

### Configure Claude Desktop

Edit: `%APPDATA%\Claude\claude_desktop_config.json`

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

**Notes:**
- Use **double backslashes** (`\\`) in Windows paths
- Update `PITCHED_API_URL` to your actual API URL
- Path can be anywhere accessible to the user

### Start Claude Desktop

1. Close Claude Desktop completely (if running)
2. Start Claude Desktop
3. MCP Server loads automatically
4. Uses cached token (no prompts)

---

## Command-Line Options

### Authentication Commands

```powershell
# Authenticate (long form)
PitchedBillingApi.McpServer.exe --authenticate

# Authenticate (short form)
PitchedBillingApi.McpServer.exe --auth
```

### Environment Variables

Can be set in system environment or passed via Claude Desktop config:

| Variable | Description | Default |
|----------|-------------|---------|
| `PITCHED_API_URL` | Billing API URL | `http://localhost:5222` |
| `AZURE_TENANT_ID` | Azure AD Tenant | `bfdb0820-807a-4f65-8d05-bee073c61a3f` |
| `AZURE_CLIENT_ID` | App Registration ID | `ba5d30cf-2c5e-4915-bd94-f08c99f200ba` |
| `AZURE_API_SCOPE` | API scope | `api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba/access_as_user` |

---

## Installation Options

### Option 1: Manual Installation

1. Create folder: `C:\Program Files\PitchedBilling\`
2. Copy `PitchedBillingApi.McpServer.exe` to folder
3. Users run `--authenticate` from that folder
4. Update Claude Desktop config with full path

### Option 2: User Profile Installation

1. Create folder: `%LOCALAPPDATA%\Programs\PitchedBilling\`
2. Copy EXE to folder
3. No admin rights needed
4. Each user has their own copy

### Option 3: MSI Installer (Advanced)

Create an MSI installer that:
1. Installs EXE to Program Files
2. Adds to PATH
3. Launches authentication setup wizard
4. Configures Claude Desktop automatically

**Tools to create MSI:**
- WiX Toolset
- Advanced Installer
- Visual Studio Installer Projects

---

## Token Management

### Token Cache Location

```
%LOCALAPPDATA%\PitchedBillingApiMcp\msal_token_cache.json
```

**Windows path example:**
```
C:\Users\IanChambers\AppData\Local\PitchedBillingApiMcp\msal_token_cache.json
```

### Token Lifecycle

- **Access Token**: Expires after ~1 hour
- **Refresh Token**: Expires after 100 days of inactivity
- **Auto-Refresh**: Handled automatically by MCP Server
- **Re-authentication**: Only needed if cache deleted or refresh token expires

### Force Re-authentication

If authentication fails or token is corrupted:

```powershell
# Delete token cache
Remove-Item "$env:LOCALAPPDATA\PitchedBillingApiMcp\msal_token_cache.json"

# Re-authenticate
PitchedBillingApi.McpServer.exe --authenticate
```

---

## Deployment Scenarios

### Scenario 1: Development Team (5-10 users)

**Recommendation:** Git repository + dotnet run

Users clone the repo and run:
```powershell
dotnet run --project PitchedBillingApi.McpServer -- --authenticate
```

**Pros:**
- Easy updates (git pull)
- Developers already have .NET SDK
- Full source code access

**Cons:**
- Requires .NET SDK installed
- More complex for non-developers

### Scenario 2: Business Users (10-50 users)

**Recommendation:** Self-contained EXE + shared folder

1. Build self-contained EXE
2. Place in network share: `\\server\apps\PitchedBilling\PitchedBillingApi.McpServer.exe`
3. Users run authentication once
4. Claude Desktop config points to network share

**Pros:**
- No .NET SDK required
- Centralized updates (replace EXE on share)
- Easy for non-technical users

**Cons:**
- Network dependency
- Slower startup from network share

### Scenario 3: Enterprise (50+ users)

**Recommendation:** MSI Installer + Group Policy

1. Create MSI installer
2. Deploy via Group Policy or SCCM
3. Silent install with authentication deferred to first run
4. Claude Desktop config via Group Policy Preferences

**Pros:**
- Automated deployment
- Consistent installation across organization
- IT-managed updates

**Cons:**
- More complex initial setup
- Requires IT infrastructure

---

## Troubleshooting EXE Deployment

### "The application to execute does not exist"

**Cause:** Path in `claude_desktop_config.json` is incorrect

**Solution:**
- Check path uses double backslashes: `C:\\Program Files\\...`
- Verify EXE exists at that location
- Use absolute path, not relative

### "Authentication Required" prompt not appearing

**Cause:** Running from Claude Desktop instead of terminal

**Solution:**
1. Open PowerShell or Command Prompt
2. Navigate to EXE location
3. Run: `.\PitchedBillingApi.McpServer.exe --authenticate`
4. Complete authentication in browser
5. Then start Claude Desktop

### "401 Unauthorized" errors after authentication

**Possible causes:**
- User not in Finance group
- Token expired and refresh failed
- API URL incorrect

**Solution:**
```powershell
# Check current token cache
dir "$env:LOCALAPPDATA\PitchedBillingApiMcp\"

# Re-authenticate
Remove-Item "$env:LOCALAPPDATA\PitchedBillingApiMcp\msal_token_cache.json"
.\PitchedBillingApi.McpServer.exe --authenticate

# Verify Finance group membership in Azure AD
```

### EXE won't start from Claude Desktop

**Check Claude Desktop logs:**
```
%APPDATA%\Claude\logs\
```

**Common issues:**
- Antivirus blocking EXE
- Missing dependencies (shouldn't happen with self-contained)
- Environment variables not set correctly

**Solution:**
1. Test EXE standalone: `.\PitchedBillingApi.McpServer.exe --authenticate`
2. If works standalone, issue is with Claude Desktop config
3. Check logs for specific error messages

---

## Distribution Checklist

Before distributing to users:

- [ ] Build self-contained EXE (win-x64)
- [ ] Test EXE on clean machine (no .NET SDK)
- [ ] Verify `--authenticate` flag works
- [ ] Create installation instructions
- [ ] Provide Claude Desktop config template
- [ ] Document API URL for production
- [ ] Test with Finance group member
- [ ] Test with non-Finance group member (should get 403)
- [ ] Create troubleshooting guide
- [ ] Set up support process

---

## Example Deployment Email

```
Subject: Pitched Billing - Claude Desktop Integration Setup

Hi Team,

We've set up Claude Desktop to work with our Pitched Billing API. Here's how to get started:

1. INSTALL THE MCP SERVER
   - Copy attached PitchedBillingApi.McpServer.exe to:
     C:\Program Files\PitchedBilling\
   - (You may need admin rights to copy to Program Files)

2. AUTHENTICATE
   - Open PowerShell
   - Run: cd "C:\Program Files\PitchedBilling"
   - Run: .\PitchedBillingApi.McpServer.exe --authenticate
   - Follow the device code prompts in your browser
   - Sign in with your company Azure AD account

3. CONFIGURE CLAUDE DESKTOP
   - Open: %APPDATA%\Claude\claude_desktop_config.json
   - Add the configuration from attached claude_config.json

4. START CLAUDE DESKTOP
   - Close Claude Desktop completely
   - Reopen Claude Desktop
   - You can now ask Claude about billing plans, invoices, etc.

IMPORTANT:
- You must be a member of the Finance group in Azure AD
- Run the --authenticate step BEFORE configuring Claude Desktop
- If you get authentication errors, run the --authenticate step again

Need help? Contact IT Support or check the troubleshooting guide.

Best regards,
IT Team
```

---

## Advanced: Silent Authentication (Service Principal)

For automated scenarios where user authentication isn't possible:

**Not recommended for this use case** because:
- Loses individual user audit trail
- All actions appear as service principal
- Doesn't align with "user authenticates as themselves" requirement

If you need this anyway, contact the development team for service principal setup instructions.

---

## Summary

Your EXE deployment process:

1. **Build**: `dotnet publish` creates self-contained EXE
2. **Distribute**: Copy EXE to user machines or network share
3. **First-time setup**: Users run `--authenticate` flag once
4. **Configure**: Add EXE path to Claude Desktop config
5. **Use**: Claude Desktop starts MCP Server automatically with cached token

The `--authenticate` flag ensures users can pre-authenticate in a terminal before Claude Desktop tries to use it!
