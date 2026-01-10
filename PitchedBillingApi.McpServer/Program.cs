using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using PitchedBillingApi.McpServer.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (MCP protocol requires stdout for JSON-RPC only)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure HttpClient for PitchedBillingApi
builder.Services.AddHttpClient("PitchedBillingApi", client =>
{
    // Default to localhost:5222, but this can be overridden via environment variable
    var apiUrl = Environment.GetEnvironmentVariable("PITCHED_API_URL") ?? "http://localhost:5222";
    client.BaseAddress = new Uri(apiUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Register tool classes
builder.Services.AddSingleton<BillingPlanTools>();
builder.Services.AddSingleton<InvoiceTools>();
builder.Services.AddSingleton<QuickBooksTools>();

// Add MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

await app.RunAsync();
