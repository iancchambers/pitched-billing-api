# Claude Desktop MCP Server Setup Guide

This guide will help you connect the Pitched Billing API MCP Server to Claude Desktop.

## Prerequisites

1. Claude Desktop installed on your Windows machine
2. .NET 10 Runtime installed
3. The PitchedBillingApi running on `http://localhost:5222`

## Configuration Steps

### 1. Locate Claude Desktop Configuration File

The Claude Desktop configuration file is located at:
```
%APPDATA%\Claude\claude_desktop_config.json
```

Full path example:
```
C:\Users\<YourUsername>\AppData\Roaming\Claude\claude_desktop_config.json
```

### 2. Edit the Configuration File

Open `claude_desktop_config.json` in a text editor and add the MCP server configuration:

```json
{
  "mcpServers": {
    "pitched-billing": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\ianchambers\\Repos\\pitched-billing-api\\PitchedBillingApi.McpServer\\PitchedBillingApi.McpServer.csproj"
      ],
      "env": {
        "PITCHED_API_URL": "http://localhost:5222"
      }
    }
  }
}
```

**Important Notes:**
- Replace the project path with the actual path on your system
- Use double backslashes (`\\`) in Windows paths
- The `PITCHED_API_URL` environment variable can be changed to point to a different API instance

### 3. Alternative: Using the Compiled Executable

If you prefer to run the compiled executable instead of `dotnet run`:

```json
{
  "mcpServers": {
    "pitched-billing": {
      "command": "C:\\Users\\ianchambers\\Repos\\pitched-billing-api\\PitchedBillingApi.McpServer\\bin\\Debug\\net10.0\\PitchedBillingApi.McpServer.exe",
      "args": [],
      "env": {
        "PITCHED_API_URL": "http://localhost:5222"
      }
    }
  }
}
```

### 4. Restart Claude Desktop

After saving the configuration file:
1. Close Claude Desktop completely
2. Restart Claude Desktop

### 5. Verify the Connection

In Claude Desktop, you should now be able to use the Pitched Billing API tools. Try asking:

- "List all billing plans"
- "Get QuickBooks customers"
- "Generate an invoice for billing plan [plan-id]"

## Available Tools

### Billing Plan Tools
- `list_billing_plans` - Get all billing plans (optionally filter for active only)
- `get_billing_plan` - Get a specific billing plan by ID
- `create_billing_plan` - Create a new billing plan
- `update_billing_plan` - Update an existing billing plan
- `delete_billing_plan` - Delete a billing plan
- `list_billing_plan_items` - Get all items for a billing plan
- `add_billing_plan_item` - Add an item to a billing plan
- `update_billing_plan_item` - Update an item in a billing plan
- `delete_billing_plan_item` - Delete an item from a billing plan

### Invoice Tools
- `generate_invoice` - Generate a new invoice for a billing plan
- `get_invoice` - Get details of a specific invoice
- `get_invoice_history` - Get all invoices for a billing plan
- `download_invoice_pdf` - Download the PDF for an invoice (returns base64)
- `resend_invoice_email` - Resend an invoice email to a customer

### QuickBooks Tools
- `get_authorization_url` - Get the QuickBooks OAuth authorization URL
- `get_auth_status` - Check if QuickBooks is connected
- `disconnect` - Disconnect from QuickBooks
- `list_customers` - Get all QuickBooks customers
- `get_customer` - Get a specific QuickBooks customer
- `create_customer` - Create a new QuickBooks customer
- `list_items` - Get all QuickBooks items (products/services)
- `get_item` - Get a specific QuickBooks item

## Troubleshooting

### MCP Server Not Showing Up

1. Check that the path in `claude_desktop_config.json` is correct
2. Verify the configuration file is valid JSON (use a JSON validator)
3. Make sure Claude Desktop was fully restarted
4. Check Claude Desktop logs for errors

### Tools Not Working

1. Ensure the PitchedBillingApi is running on the configured URL
2. Check the `PITCHED_API_URL` environment variable is correct
3. Verify the API is accessible from your machine

### Finding Claude Desktop Logs

Claude Desktop logs are typically located at:
```
%APPDATA%\Claude\logs\
```

## Environment Variables

You can customize the API URL by changing the `PITCHED_API_URL` environment variable in the configuration:

```json
"env": {
  "PITCHED_API_URL": "https://your-api-server.com"
}
```

## Security Notes

- The MCP server communicates with the API over HTTP by default
- If using in production, ensure you're using HTTPS
- The API should be secured with appropriate authentication
- Never commit the `claude_desktop_config.json` file to version control

## Example Usage in Claude Desktop

Once configured, you can interact with the billing system naturally:

**Example 1: Listing Billing Plans**
```
User: Show me all active billing plans
Claude: [Uses list_billing_plans tool with activeOnly=true]
```

**Example 2: Creating a Billing Plan**
```
User: Create a new monthly billing plan for customer 123 named "Standard Support" starting on 2026-02-01
Claude: [Uses create_billing_plan tool with the specified parameters]
```

**Example 3: Generating an Invoice**
```
User: Generate an invoice for billing plan <guid>
Claude: [Uses generate_invoice tool]
```

## Next Steps

- Ensure the PitchedBillingApi is running before using the MCP server
- Test each tool category to verify they're working correctly
- Monitor the API logs for any errors or issues
