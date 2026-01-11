using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
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

// Configure Azure AD authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// Configure authorization with group-based policies
var financeGroupId = builder.Configuration["Authorization:FinanceGroupId"];
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("FinancePolicy", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("groups", financeGroupId!);
    });
});

// Configure CORS for MCP server
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // Allow localhost for development
        var allowedOrigins = new List<string> { "http://localhost", "https://localhost" };

        // Allow Container App URL for production
        var containerAppUrl = Environment.GetEnvironmentVariable("MCP_SERVER_URL");
        if (!string.IsNullOrEmpty(containerAppUrl))
        {
            allowedOrigins.Add(containerAppUrl);
        }

        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

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
app.UseCors();
app.UseAuthentication();

// DEBUG: Log token claims in Development mode
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("=== TOKEN CLAIMS DEBUG ===");
            logger.LogInformation("User: {User}", context.User.Identity.Name);

            foreach (var claim in context.User.Claims)
            {
                logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
            }

            var financeGroupId = builder.Configuration["Authorization:FinanceGroupId"];
            var hasFinanceGroup = context.User.Claims.Any(c => c.Type == "groups" && c.Value == financeGroupId);
            logger.LogInformation("Has Finance Group ({GroupId}): {HasGroup}", financeGroupId, hasFinanceGroup);
            logger.LogInformation("=== END TOKEN CLAIMS ===");
        }
        await next();
    });
}

app.UseAuthorization();

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
.WithName("CreateBillingPlan")
.RequireAuthorization("FinancePolicy");

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
.WithName("ListBillingPlans")
.RequireAuthorization("FinancePolicy");

// Get billing plan by ID
app.MapGet("/api/billingplan/{id:guid}", async (Guid id, BillingDbContext db) =>
{
    var plan = await db.BillingPlans
        .Include(p => p.Items.OrderBy(i => i.SortOrder))
        .FirstOrDefaultAsync(p => p.Id == id);

    return plan is null ? Results.NotFound() : Results.Ok(plan.ToResponse());
})
.WithName("GetBillingPlan")
.RequireAuthorization("FinancePolicy");

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
.WithName("UpdateBillingPlan")
.RequireAuthorization("FinancePolicy");

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
.WithName("DeleteBillingPlan")
.RequireAuthorization("FinancePolicy");

// ============================================================================
// Billing Plan Items Endpoints
// ============================================================================

// Add item to billing plan
app.MapPost("/api/billingplan/{id:guid}/items", async (Guid id, CreateBillingPlanItemRequest request, BillingDbContext db, IQuickBooksService qbService) =>
{
    var plan = await db.BillingPlans.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);

    if (plan is null)
    {
        return Results.NotFound();
    }

    // Fetch QuickBooks item to get tax code
    var qbItem = await qbService.GetItemAsync(request.QuickBooksItemId);
    if (qbItem is null)
    {
        return Results.BadRequest(new { error = $"QuickBooks item {request.QuickBooksItemId} not found" });
    }

    // Get tax code and VAT rate from QuickBooks item
    string taxCodeId = qbItem.SalesTaxCodeId ?? string.Empty;
    decimal vatRate = 0m;

    if (!string.IsNullOrEmpty(taxCodeId))
    {
        var taxInfo = await qbService.GetTaxInfoAsync(taxCodeId);
        if (taxInfo is not null)
        {
            vatRate = taxInfo.TaxRate;
        }
    }

    var maxSortOrder = plan.Items.Any() ? plan.Items.Max(i => i.SortOrder) : 0;

    var item = new BillingPlanItem
    {
        Id = Guid.NewGuid(),
        BillingPlanId = id,
        QuickBooksItemId = request.QuickBooksItemId,
        ItemName = qbItem.Name, // Automatically fetch from QuickBooks
        Quantity = request.Quantity,
        Rate = request.Rate,
        Description = request.Description,
        SortOrder = maxSortOrder + 1,
        FromDate = request.FromDate,
        ToDate = request.ToDate,
        QuickBooksTaxCodeId = taxCodeId,
        VatRate = vatRate
    };

    db.BillingPlanItems.Add(item);
    plan.ModifiedDate = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Created($"/api/billingplan/{id}/items/{item.Id}", item.ToResponse());
})
.WithName("AddBillingPlanItem")
.RequireAuthorization("FinancePolicy");

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
.WithName("GetBillingPlanItems")
.RequireAuthorization("FinancePolicy");

// Update billing plan item
app.MapPut("/api/billingplan/{id:guid}/items/{itemId:guid}", async (
    Guid id,
    Guid itemId,
    UpdateBillingPlanItemRequest request,
    BillingDbContext db,
    IQuickBooksService qbService) =>
{
    var item = await db.BillingPlanItems
        .Include(i => i.BillingPlan)
        .FirstOrDefaultAsync(i => i.Id == itemId && i.BillingPlanId == id);

    if (item is null)
    {
        return Results.NotFound();
    }

    // Fetch QuickBooks item to get tax code
    var qbItem = await qbService.GetItemAsync(request.QuickBooksItemId);
    if (qbItem is null)
    {
        return Results.BadRequest(new { error = $"QuickBooks item {request.QuickBooksItemId} not found" });
    }

    // Get tax code and VAT rate from QuickBooks item
    string taxCodeId = qbItem.SalesTaxCodeId ?? string.Empty;
    decimal vatRate = 0m;

    if (!string.IsNullOrEmpty(taxCodeId))
    {
        var taxInfo = await qbService.GetTaxInfoAsync(taxCodeId);
        if (taxInfo is not null)
        {
            vatRate = taxInfo.TaxRate;
        }
    }

    item.QuickBooksItemId = request.QuickBooksItemId;
    item.ItemName = qbItem.Name; // Automatically fetch from QuickBooks
    item.Quantity = request.Quantity;
    item.Rate = request.Rate;
    item.Description = request.Description;
    item.SortOrder = request.SortOrder;
    item.FromDate = request.FromDate;
    item.ToDate = request.ToDate;
    item.QuickBooksTaxCodeId = taxCodeId;
    item.VatRate = vatRate;
    item.BillingPlan.ModifiedDate = DateTime.UtcNow;

    await db.SaveChangesAsync();

    return Results.NoContent();
})
.WithName("UpdateBillingPlanItem")
.RequireAuthorization("FinancePolicy");

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
.WithName("DeleteBillingPlanItem")
.RequireAuthorization("FinancePolicy");

// ============================================================================
// Invoice Endpoints
// ============================================================================

// Generate invoice for a billing plan (creates draft invoice, does NOT post to QuickBooks)
app.MapPost("/api/invoice/generate", async (GenerateInvoiceRequest request, IInvoiceOrchestrationService invoiceService) =>
{
    try
    {
        // Always create draft invoices - use separate endpoint to post to QuickBooks
        var result = await invoiceService.GenerateInvoiceAsync(request, request.SendEmail);
        return Results.Created($"/api/invoice/{result.InvoiceId}", result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GenerateInvoice")
.RequireAuthorization("FinancePolicy");

// Get invoice by ID
app.MapGet("/api/invoice/{id:guid}", async (Guid id, IInvoiceOrchestrationService invoiceService) =>
{
    var invoice = await invoiceService.GetInvoiceAsync(id);
    return invoice is null ? Results.NotFound() : Results.Ok(invoice);
})
.WithName("GetInvoice")
.RequireAuthorization("FinancePolicy");

// Get invoice history for a billing plan
app.MapGet("/api/invoice/plan/{planId:guid}/history", async (Guid planId, IInvoiceOrchestrationService invoiceService) =>
{
    var history = await invoiceService.GetInvoicesAsync(planId);
    return Results.Ok(history);
})
.WithName("GetInvoices")
.RequireAuthorization("FinancePolicy");

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
.WithName("DownloadInvoicePdf")
.RequireAuthorization("FinancePolicy");

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
.WithName("ResendInvoice")
.RequireAuthorization("FinancePolicy");

// Post draft invoice to QuickBooks
app.MapPost("/api/invoice/{id:guid}/post-to-quickbooks", async (Guid id, IInvoiceOrchestrationService invoiceService) =>
{
    try
    {
        var result = await invoiceService.PostInvoiceToQuickBooksAsync(id);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("PostInvoiceToQuickBooks")
.RequireAuthorization("FinancePolicy");

// Update draft invoice item descriptions
app.MapPut("/api/invoice/{id:guid}/items", async (Guid id, UpdateInvoiceItemsRequest request, IInvoiceOrchestrationService invoiceService) =>
{
    try
    {
        var result = await invoiceService.UpdateDraftInvoiceItemsAsync(id, request);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UpdateDraftInvoiceItems")
.RequireAuthorization("FinancePolicy");

// ============================================================================
// QuickBooks Auth Endpoints
// ============================================================================

// Start OAuth authorization flow
app.MapGet("/api/quickbooks/auth/authorize", async (IQuickBooksAuthService authService) =>
{
    var state = Guid.NewGuid().ToString();
    var authUrl = await authService.GetAuthorizationUrlAsync(state);
    return Results.Ok(new { authorizationUrl = authUrl, state });
})
.WithName("QuickBooksAuthorize")
.RequireAuthorization("FinancePolicy");

// OAuth callback - exchange code for tokens
// NOTE: This endpoint must be publicly accessible (no auth) because QuickBooks calls it
app.MapGet("/api/quickbooks/auth/callback", async (
    string code,
    string realmId,
    string state,
    IQuickBooksAuthService authService,
    ILogger<Program> logger) =>
{
    try
    {
        // SECURITY: Validate state token to prevent CSRF attacks
        var isStateValid = await authService.ValidateAndConsumeStateAsync(state);
        if (!isStateValid)
        {
            logger.LogWarning("QuickBooks OAuth callback received invalid or expired state: {State}", state);
            return Results.BadRequest(new
            {
                error = "Invalid or expired authorization request. Please try connecting to QuickBooks again."
            });
        }

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
        logger.LogError(ex, "Error processing QuickBooks OAuth callback");
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("QuickBooksCallback")
.AllowAnonymous(); // QuickBooks doesn't have an Azure AD token

// Check connection status
app.MapGet("/api/quickbooks/auth/status", (IQuickBooksAuthService authService) =>
{
    return Results.Ok(new
    {
        isConnected = authService.IsConnected,
        realmId = authService.RealmId
    });
})
.WithName("QuickBooksStatus")
.RequireAuthorization("FinancePolicy");

// Disconnect from QuickBooks (clear tokens)
app.MapPost("/api/quickbooks/auth/disconnect", async (IQuickBooksAuthService authService) =>
{
    // For now, just return status - in production you'd clear stored tokens
    return Results.Ok(new { message = "Disconnected from QuickBooks" });
})
.WithName("QuickBooksDisconnect")
.RequireAuthorization("FinancePolicy");

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
.WithName("GetQuickBooksCustomers")
.RequireAuthorization("FinancePolicy");

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
.WithName("GetQuickBooksCustomer")
.RequireAuthorization("FinancePolicy");

// Create a customer in QuickBooks - NOT IMPLEMENTED
app.MapPost("/api/quickbooks/customers", (CreateQuickBooksCustomerRequest request) =>
{
    return Results.Json(new
    {
        error = "Customer creation must be done directly in QuickBooks as it is the source of truth. This API only supports billing plan setup for existing customers."
    }, statusCode: 501);
})
.WithName("CreateQuickBooksCustomer")
.RequireAuthorization("FinancePolicy");

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
.WithName("GetQuickBooksItems")
.RequireAuthorization("FinancePolicy");

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
.WithName("GetQuickBooksItem")
.RequireAuthorization("FinancePolicy");

// Get tax code information from QuickBooks
app.MapGet("/api/quickbooks/taxcodes/{taxCodeId}", async (string taxCodeId, IQuickBooksService qbService) =>
{
    try
    {
        var taxInfo = await qbService.GetTaxInfoAsync(taxCodeId);
        return taxInfo is null ? Results.NotFound() : Results.Ok(taxInfo);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Not connected"))
    {
        return Results.BadRequest(new { error = "Not connected to QuickBooks. Please authorize first." });
    }
})
.WithName("GetQuickBooksTaxCode")
.RequireAuthorization("FinancePolicy");

app.Run();
