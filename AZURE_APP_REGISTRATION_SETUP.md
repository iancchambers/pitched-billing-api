# Azure App Registration Setup for MCP Server

## Error: AADSTS7000218 - Missing client_assertion or client_secret

If you see this error when running `--authenticate`:
```
AADSTS7000218: The request body must contain the following parameter: 'client_assertion' or 'client_secret'
```

This means your Azure App Registration is not configured to allow **public client flows** (device code flow).

## Required Configuration Steps

### 1. Enable Public Client Flows

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** → **App registrations**
3. Find your app: **Pitched Billing API** (App ID: `ba5d30cf-2c5e-4915-bd94-f08c99f200ba`)
4. Click **Authentication** in the left menu
5. Scroll down to **Advanced settings** section
6. Find **Allow public client flows**
7. Set the toggle to **Yes**
8. Click **Save** at the top

### 2. Add Mobile and Desktop Applications Platform

1. Still in **Authentication** section
2. Click **+ Add a platform**
3. Select **Mobile and desktop applications**
4. Check the box for: `http://localhost`
5. Click **Configure**

### 3. Configure Group Claims (CRITICAL for Finance Group Authorization)

**⚠️ This is required for the Finance group authorization to work!**

Currently, the API has group checking **disabled** for local development. To enable it for production:

1. Click **Token configuration** in the left menu
2. Click **+ Add groups claim**
3. Select **Security groups**
4. Under **Customize token properties by type**, check:
   - ✅ **Group ID** (for all three: ID, Access, SAML)
5. Click **Add**

This ensures that when users authenticate, their group memberships (including the Finance group) are included in the token as `groups` claims.

**After configuring this, re-enable group checking in Program.cs:**
```csharp
// In Program.cs, uncomment this line:
policy.RequireClaim("groups", financeGroupId!);
```

### 4. Verify API Permissions

1. Click **API permissions** in the left menu
2. Ensure you have:
   - **Microsoft Graph** → `User.Read` (Delegated)
   - **Pitched Billing API** (your own API) → `access_as_user` (Delegated)
3. If missing, click **+ Add a permission**:
   - For your API: Click **My APIs** → Select **Pitched Billing API** → **Delegated permissions** → Check `access_as_user`
4. Click **Grant admin consent for [Your Organization]** at the top (requires admin rights)

### 5. Expose an API Scope (Already Done)

Your API should already have this configured:
- **Application ID URI**: `api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba`
- **Scope name**: `access_as_user`
- **Who can consent**: Admins and users

## Configuration Summary

| Setting | Value |
|---------|-------|
| **App Registration Name** | Pitched Billing API |
| **Application (client) ID** | ba5d30cf-2c5e-4915-bd94-f08c99f200ba |
| **Directory (tenant) ID** | bfdb0820-807a-4f65-8d05-bee073c61a3f |
| **Application ID URI** | api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba |
| **API Scope** | api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba/access_as_user |
| **Allow public client flows** | ✅ Yes (Required!) |
| **Group claims** | ✅ Security groups - Group ID (Required for authorization!) |
| **Supported account types** | Single tenant |
| **Finance Group ID** | 3c01275a-eb47-46dc-bf59-4b471eced4e0 |

## Testing After Configuration

After making these changes, test authentication:

```powershell
# Run from the build output directory
cd PitchedBillingApi.McpServer\bin\Debug\net10.0
.\PitchedBillingApi.McpServer.exe --authenticate
```

You should see:
1. Device code prompt with URL and code
2. Open browser to https://microsoft.com/devicelogin
3. Enter the code shown
4. Sign in with your Azure AD account
5. See "✓ Authentication successful!" message

## Troubleshooting

### "User is not in Finance group" error
- Verify your account is a member of the Finance group (ID: 3c01275a-eb47-46dc-bf59-4b471eced4e0)
- In Azure Portal: **Azure Active Directory** → **Groups** → **Finance** → **Members**

### "Unauthorized" when calling API
- Ensure the API is configured to validate tokens from your tenant
- Check `appsettings.json` in PitchedBillingApi has correct TenantId
- Verify Finance group ID matches in both API and Azure AD

### Token cache issues
Delete the token cache and re-authenticate:
```powershell
Remove-Item -Path "$env:LOCALAPPDATA\PitchedBillingApiMcp\msal_token_cache.json" -Force
.\PitchedBillingApi.McpServer.exe --authenticate
```

## Security Notes

- **Device Code Flow** is secure for local desktop applications
- Users authenticate with their own Azure AD credentials
- Tokens are cached locally and encrypted by MSAL
- No client secrets are embedded in the application
- Each user gets their own token based on their group membership
- Tokens automatically refresh every hour
- Refresh tokens are valid for 100 days (reset with each use)
