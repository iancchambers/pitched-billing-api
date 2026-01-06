using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using PitchedBillingApi.Data;
using PitchedBillingApi.Entities;
using PitchedBillingApi.Models;
using PitchedBillingApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure Key Vault (only in production, or when explicitly enabled)
if (!builder.Environment.IsDevelopment())
{
    var keyVaultUri = "https://pitched-billing-api.vault.azure.net/";
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

// Add services to the container
builder.Services.AddOpenApi();

// Configure Entity Framework with SQL Server
var connectionString = builder.Configuration["database-connection"];
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string not found. In Development, add to appsettings.Development.json. In Production, ensure Key Vault secret 'database-connection' exists.");
}

builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    }));

// Register QuickBooks services
builder.Services.AddHttpClient<IQuickBooksAuthService, QuickBooksAuthService>();
builder.Services.AddHttpClient<IQuickBooksService, QuickBooksService>();

// Register Reporting service
builder.Services.AddScoped<IReportingService, ReportingService>();

// Register Mailgun service
builder.Services.AddHttpClient<IMailgunService, MailgunService>();

// Register Invoice orchestration service
builder.Services.AddScoped<IInvoiceOrchestrationService, InvoiceOrchestrationService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.MapOpenApi(); // Serves OpenAPI spec at /openapi/v1.json

app.UseHttpsRedirection();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck");

// ============================================================================
// Billing Plan Endpoints
// ============================================================================

// Create billing plan
app.MapPost("/api/billingplan", async (CreateBillingPlanRequest request, BillingDbContext db) =>
{
    var plan = new BillingPlan
    {
        Id = Guid.NewGuid(),
        PlanName = request.PlanName,
        QuickBooksCustomerId = request.QuickBooksCustomerId,
        Frequency = request.Frequency,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        IsActive = true,
        CreatedDate = DateTime.UtcNow
    };

    db.BillingPlans.Add(plan);
    await db.SaveChangesAsync();

    return Results.Created($"/api/billingplan/{plan.Id}", plan.ToResponse());
})
.WithName("CreateBillingPlan");

// List all billing plans
app.MapGet("/api/billingplan", async (BillingDbContext db, bool? activeOnly) =>
{
    var query = db.BillingPlans.Include(p => p.Items).AsQueryable();

    if (activeOnly == true)
    {
        query = query.Where(p => p.IsActive);
    }

    var plans = await query.OrderByDescending(p => p.CreatedDate).ToListAsync();
    return Results.Ok(plans.Select(p => p.ToResponse()));
})
.WithName("ListBillingPlans");

// Get billing plan by ID
app.MapGet("/api/billingplan/{id:guid}", async (Guid id, BillingDbContext db) =>
{
    var plan = await db.BillingPlans
        .Include(p => p.Items.OrderBy(i => i.SortOrder))
        .FirstOrDefaultAsync(p => p.Id == id);

    return plan is null ? Results.NotFound() : Results.Ok(plan.ToResponse());
})
.WithName("GetBillingPlan");

// Update billing plan
app.MapPut("/api/billingplan/{id:guid}", async (Guid id, UpdateBillingPlanRequest request, BillingDbContext db) =>
{
    var plan = await db.BillingPlans.FindAsync(id);

    if (plan is null)
    {
        return Results.NotFound();
    }

    plan.PlanName = request.PlanName;
    plan.Frequency = request.Frequency;
    plan.StartDate = request.StartDate;
    plan.EndDate = request.EndDate;
    plan.IsActive = request.IsActive;
    plan.ModifiedDate = DateTime.UtcNow;

    await db.SaveChangesAsync();

    return Results.NoContent();
})
.WithName("UpdateBillingPlan");

// Delete billing plan
app.MapDelete("/api/billingplan/{id:guid}", async (Guid id, BillingDbContext db) =>
{
    var plan = await db.BillingPlans.FindAsync(id);

    if (plan is null)
    {
        return Results.NotFound();
    }

    db.BillingPlans.Remove(plan);
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.WithName("DeleteBillingPlan");

// ============================================================================
// Billing Plan Items Endpoints
// ============================================================================

// Add item to billing plan
app.MapPost("/api/billingplan/{id:guid}/items", async (Guid id, CreateBillingPlanItemRequest request, BillingDbContext db) =>
{
    var plan = await db.BillingPlans.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);

    if (plan is null)
    {
        return Results.NotFound();
    }

    var maxSortOrder = plan.Items.Any() ? plan.Items.Max(i => i.SortOrder) : 0;

    var item = new BillingPlanItem
    {
        Id = Guid.NewGuid(),
        BillingPlanId = id,
        QuickBooksItemId = request.QuickBooksItemId,
        ItemName = request.ItemName,
        Quantity = request.Quantity,
        Rate = request.Rate,
        Description = request.Description,
        SortOrder = maxSortOrder + 1
    };

    db.BillingPlanItems.Add(item);
    plan.ModifiedDate = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Created($"/api/billingplan/{id}/items/{item.Id}", item.ToResponse());
})
.WithName("AddBillingPlanItem");

// Get items for billing plan
app.MapGet("/api/billingplan/{id:guid}/items", async (Guid id, BillingDbContext db) =>
{
    var plan = await db.BillingPlans
        .Include(p => p.Items.OrderBy(i => i.SortOrder))
        .FirstOrDefaultAsync(p => p.Id == id);

    if (plan is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(plan.Items.Select(i => i.ToResponse()));
})
.WithName("GetBillingPlanItems");

// Update billing plan item
app.MapPut("/api/billingplan/{id:guid}/items/{itemId:guid}", async (
    Guid id,
    Guid itemId,
    UpdateBillingPlanItemRequest request,
    BillingDbContext db) =>
{
    var item = await db.BillingPlanItems
        .Include(i => i.BillingPlan)
        .FirstOrDefaultAsync(i => i.Id == itemId && i.BillingPlanId == id);

    if (item is null)
    {
        return Results.NotFound();
    }

    item.QuickBooksItemId = request.QuickBooksItemId;
    item.ItemName = request.ItemName;
    item.Quantity = request.Quantity;
    item.Rate = request.Rate;
    item.Description = request.Description;
    item.SortOrder = request.SortOrder;
    item.BillingPlan.ModifiedDate = DateTime.UtcNow;

    await db.SaveChangesAsync();

    return Results.NoContent();
})
.WithName("UpdateBillingPlanItem");

// Delete billing plan item
app.MapDelete("/api/billingplan/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, BillingDbContext db) =>
{
    var item = await db.BillingPlanItems
        .Include(i => i.BillingPlan)
        .FirstOrDefaultAsync(i => i.Id == itemId && i.BillingPlanId == id);

    if (item is null)
    {
        return Results.NotFound();
    }

    db.BillingPlanItems.Remove(item);
    item.BillingPlan.ModifiedDate = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.WithName("DeleteBillingPlanItem");

// ============================================================================
// Invoice Endpoints
// ============================================================================

// Generate invoice for a billing plan
app.MapPost("/api/invoice/generate", async (GenerateInvoiceRequest request, IInvoiceOrchestrationService invoiceService) =>
{
    try
    {
        var result = await invoiceService.GenerateInvoiceAsync(request);
        return Results.Created($"/api/invoice/{result.InvoiceHistoryId}", result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GenerateInvoice");

// Get invoice by ID
app.MapGet("/api/invoice/{id:guid}", async (Guid id, IInvoiceOrchestrationService invoiceService) =>
{
    var invoice = await invoiceService.GetInvoiceAsync(id);
    return invoice is null ? Results.NotFound() : Results.Ok(invoice);
})
.WithName("GetInvoice");

// Get invoice history for a billing plan
app.MapGet("/api/invoice/plan/{planId:guid}/history", async (Guid planId, IInvoiceOrchestrationService invoiceService) =>
{
    var history = await invoiceService.GetInvoiceHistoryAsync(planId);
    return Results.Ok(history);
})
.WithName("GetInvoiceHistory");

// Download invoice PDF
app.MapGet("/api/invoice/{id:guid}/pdf", async (Guid id, IInvoiceOrchestrationService invoiceService) =>
{
    var pdf = await invoiceService.GetInvoicePdfAsync(id);
    if (pdf == null || pdf.Length == 0)
    {
        return Results.NotFound();
    }

    return Results.File(pdf, "application/pdf", $"invoice-{id}.pdf");
})
.WithName("DownloadInvoicePdf");

// Resend invoice email
app.MapPost("/api/invoice/{id:guid}/resend", async (Guid id, ResendRequest? request, IInvoiceOrchestrationService invoiceService) =>
{
    try
    {
        var result = await invoiceService.ResendInvoiceAsync(id, request?.RecipientEmail);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("ResendInvoice");

// ============================================================================
// QuickBooks Auth Endpoints
// ============================================================================

// Start OAuth authorization flow
app.MapGet("/api/quickbooks/auth/authorize", (IQuickBooksAuthService authService) =>
{
    var state = Guid.NewGuid().ToString();
    var authUrl = authService.GetAuthorizationUrl(state);
    return Results.Ok(new { authorizationUrl = authUrl, state });
})
.WithName("QuickBooksAuthorize");

// OAuth callback - exchange code for tokens
app.MapGet("/api/quickbooks/auth/callback", async (
    string code,
    string realmId,
    string state,
    IQuickBooksAuthService authService) =>
{
    try
    {
        var tokens = await authService.ExchangeCodeForTokensAsync(code, realmId);
        return Results.Ok(new
        {
            message = "Successfully connected to QuickBooks",
            realmId,
            expiresIn = tokens.ExpiresIn
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("QuickBooksCallback");

// Check connection status
app.MapGet("/api/quickbooks/auth/status", (IQuickBooksAuthService authService) =>
{
    return Results.Ok(new
    {
        isConnected = authService.IsConnected,
        realmId = authService.RealmId
    });
})
.WithName("QuickBooksStatus");

// Disconnect from QuickBooks (clear tokens)
app.MapPost("/api/quickbooks/auth/disconnect", async (IQuickBooksAuthService authService) =>
{
    // For now, just return status - in production you'd clear stored tokens
    return Results.Ok(new { message = "Disconnected from QuickBooks" });
})
.WithName("QuickBooksDisconnect");

// ============================================================================
// QuickBooks Data Endpoints
// ============================================================================

// Get all customers from QuickBooks
app.MapGet("/api/quickbooks/customers", async (IQuickBooksService qbService) =>
{
    try
    {
        var customers = await qbService.GetCustomersAsync();
        return Results.Ok(customers);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Not connected"))
    {
        return Results.BadRequest(new { error = "Not connected to QuickBooks. Please authorize first." });
    }
})
.WithName("GetQuickBooksCustomers");

// Get a specific customer from QuickBooks
app.MapGet("/api/quickbooks/customers/{customerId}", async (string customerId, IQuickBooksService qbService) =>
{
    try
    {
        var customer = await qbService.GetCustomerAsync(customerId);
        return customer is null ? Results.NotFound() : Results.Ok(customer);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Not connected"))
    {
        return Results.BadRequest(new { error = "Not connected to QuickBooks. Please authorize first." });
    }
})
.WithName("GetQuickBooksCustomer");

// Get all items from QuickBooks
app.MapGet("/api/quickbooks/items", async (IQuickBooksService qbService) =>
{
    try
    {
        var items = await qbService.GetItemsAsync();
        return Results.Ok(items);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Not connected"))
    {
        return Results.BadRequest(new { error = "Not connected to QuickBooks. Please authorize first." });
    }
})
.WithName("GetQuickBooksItems");

// Get a specific item from QuickBooks
app.MapGet("/api/quickbooks/items/{itemId}", async (string itemId, IQuickBooksService qbService) =>
{
    try
    {
        var item = await qbService.GetItemAsync(itemId);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Not connected"))
    {
        return Results.BadRequest(new { error = "Not connected to QuickBooks. Please authorize first." });
    }
})
.WithName("GetQuickBooksItem");

app.Run();
