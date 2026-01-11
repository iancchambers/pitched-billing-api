using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using PitchedBillingApi.McpServer.Services;
using PitchedBillingApi.McpServer.Tools;

// Configuration helper
static (string tenantId, string clientId, string apiScope) GetConfiguration()
{
    var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "bfdb0820-807a-4f65-8d05-bee073c61a3f";
    var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? "ba5d30cf-2c5e-4915-bd94-f08c99f200ba";
    var apiScope = Environment.GetEnvironmentVariable("AZURE_API_SCOPE") ?? "api://ba5d30cf-2c5e-4915-bd94-f08c99f200ba/access_as_user";
    return (tenantId, clientId, apiScope);
}

// Check for re-authentication mode (clears cache and re-authenticates)
if (args.Contains("--reauth") || args.Contains("--reauthenticate"))
{
    Console.WriteLine("=".PadRight(70, '='));
    Console.WriteLine("Pitched Billing MCP Server - Re-Authentication");
    Console.WriteLine("=".PadRight(70, '='));
    Console.WriteLine();
    Console.WriteLine("Clearing cached credentials...");

    var config = GetConfiguration();
    var authService = new AzureAuthService(
        tenantId: config.tenantId,
        clientId: config.clientId,
        scopes: new[] { config.apiScope }
    );

    try
    {
        // Clear existing cache
        await authService.ClearCacheAsync();
        Console.WriteLine("✓ Cache cleared");
        Console.WriteLine();

        // Force new authentication
        var token = await authService.GetAccessTokenAsync();

        Console.WriteLine();
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine("✓ Re-authentication successful!");
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine();
        Console.WriteLine("Your new credentials have been cached. Claude Desktop can now use");
        Console.WriteLine("the MCP Server with your updated authentication.");
        Console.WriteLine();
        Console.WriteLine("Token cache location:");
        Console.WriteLine($"  {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PitchedBillingApiMcp")}");
        Console.WriteLine();
        Console.WriteLine("You can now close this window and restart Claude Desktop.");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine("✗ Re-authentication failed!");
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine();
        Environment.Exit(1);
    }

    Environment.Exit(0);
}

// Check for authentication-only mode
if (args.Contains("--authenticate") || args.Contains("--auth"))
{
    Console.WriteLine("=".PadRight(70, '='));
    Console.WriteLine("Pitched Billing MCP Server - Authentication Setup");
    Console.WriteLine("=".PadRight(70, '='));
    Console.WriteLine();

    var config = GetConfiguration();
    var authService = new AzureAuthService(
        tenantId: config.tenantId,
        clientId: config.clientId,
        scopes: new[] { config.apiScope }
    );

    try
    {
        // Force authentication
        var token = await authService.GetAccessTokenAsync();

        Console.WriteLine();
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine("✓ Authentication successful!");
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine();
        Console.WriteLine("Your credentials have been cached. Claude Desktop can now use the");
        Console.WriteLine("MCP Server without requiring authentication prompts.");
        Console.WriteLine();
        Console.WriteLine("Token cache location:");
        Console.WriteLine($"  {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PitchedBillingApiMcp")}");
        Console.WriteLine();
        Console.WriteLine("You can now close this window and start Claude Desktop.");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine("✗ Authentication failed!");
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine();
        Environment.Exit(1);
    }

    Environment.Exit(0);
}

// Normal MCP Server startup
var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (MCP protocol requires stdout for JSON-RPC only)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure Azure AD authentication (device code flow for user authentication)
var configuration = GetConfiguration();
var authenticationService = new AzureAuthService(
    tenantId: configuration.tenantId,
    clientId: configuration.clientId,
    scopes: new[] { configuration.apiScope }
);

builder.Services.AddSingleton(authenticationService);

// Configure HttpClient for PitchedBillingApi with authentication
builder.Services.AddHttpClient("PitchedBillingApi", client =>
{
    // Default to localhost:5222, but this can be overridden via environment variable
    var apiUrl = Environment.GetEnvironmentVariable("PITCHED_API_URL") ?? "http://localhost:5222";
    client.BaseAddress = new Uri(apiUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddHttpMessageHandler(sp =>
{
    // Add delegating handler to automatically inject Bearer token
    return new AuthenticationDelegatingHandler(sp.GetRequiredService<AzureAuthService>());
});

// Register tool classes
builder.Services.AddSingleton<BillingPlanTools>();
builder.Services.AddSingleton<InvoiceTools>();
builder.Services.AddSingleton<QuickBooksTools>();

// Add MCP server with stdio transport
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

await app.RunAsync();
