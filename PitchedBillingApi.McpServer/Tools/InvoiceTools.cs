using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace PitchedBillingApi.McpServer.Tools;

[McpServerToolType]
public class InvoiceTools
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InvoiceTools> _logger;

    public InvoiceTools(IHttpClientFactory httpClientFactory, ILogger<InvoiceTools> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [McpServerTool, Description("Generate a DRAFT invoice for a billing plan. This creates the invoice in the database but does NOT post it to QuickBooks. Use PostInvoiceToQuickBooks to confirm and post to QuickBooks after reviewing. By default, does NOT send email.")]
    public async Task<string> GenerateInvoice(
        [Description("The GUID of the billing plan")] string billingPlanId,
        [Description("Optional invoice date (ISO 8601 format). Defaults to current date")] string? invoiceDate = null,
        [Description("Optional customer reference")] string? yourReference = null,
        [Description("Optional internal reference")] string? ourReference = null,
        [Description("Optional account handler name")] string? accountHandler = null,
        [Description("Whether to automatically send email to customer. Default is false")] bool sendEmail = false)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        var request = new
        {
            BillingPlanId = billingPlanId,
            InvoiceDate = invoiceDate,
            YourReference = yourReference,
            OurReference = ourReference,
            AccountHandler = accountHandler,
            SendEmail = sendEmail
        };

        try
        {
            var response = await client.PostAsJsonAsync("/api/invoice/generate", request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating invoice for billing plan {BillingPlanId}", billingPlanId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Get details of a specific invoice")]
    public async Task<string> GetInvoice(
        [Description("The GUID of the invoice")] string invoiceId)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.GetAsync($"/api/invoice/{invoiceId}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invoice {InvoiceId}", invoiceId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Get all invoices for a billing plan")]
    public async Task<string> GetInvoices(
        [Description("The GUID of the billing plan")] string billingPlanId)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.GetAsync($"/api/invoice/plan/{billingPlanId}/history");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invoice history for billing plan {BillingPlanId}", billingPlanId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Get the direct URL to download/view the PDF for an invoice (fast, no validation)")]
    public Task<string> GetInvoicePdfUrl(
        [Description("The GUID of the invoice")] string invoiceId)
    {
        // Get the API base URL from environment or use default
        var apiUrl = Environment.GetEnvironmentVariable("PITCHED_API_URL") ?? "http://localhost:5222";
        var pdfUrl = $"{apiUrl}/api/invoice/{invoiceId}/pdf";

        var result = JsonSerializer.Serialize(new
        {
            success = true,
            invoiceId = invoiceId,
            pdfUrl = pdfUrl,
            message = "Open this URL in a browser to view or download the PDF. The PDF is generated on-demand from the database."
        });

        return Task.FromResult(result);
    }

    [McpServerTool, Description("Download the PDF for an invoice with validation. Returns a direct link that can be opened in a browser.")]
    public async Task<string> DownloadInvoicePdf(
        [Description("The GUID of the invoice")] string invoiceId)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            // Verify the invoice exists
            var response = await client.GetAsync($"/api/invoice/{invoiceId}");
            response.EnsureSuccessStatusCode();

            // Get the API base URL from environment or use default
            var apiUrl = Environment.GetEnvironmentVariable("PITCHED_API_URL") ?? "http://localhost:5222";
            var pdfUrl = $"{apiUrl}/api/invoice/{invoiceId}/pdf";

            return JsonSerializer.Serialize(new
            {
                success = true,
                invoiceId = invoiceId,
                pdfUrl = pdfUrl,
                message = "Open this URL in a browser to view or download the PDF. The PDF is generated on-demand from the database."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PDF URL for invoice {InvoiceId}", invoiceId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Resend an invoice email to a customer")]
    public async Task<string> ResendInvoiceEmail(
        [Description("The GUID of the invoice")] string invoiceId,
        [Description("Optional email address to send to. If not provided, uses the customer's default email")] string? recipientEmail = null)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        var request = new
        {
            RecipientEmail = recipientEmail
        };

        try
        {
            var response = await client.PostAsJsonAsync($"/api/invoice/{invoiceId}/resend", request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending invoice email for {InvoiceId}", invoiceId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Update line item descriptions on a draft invoice. This allows you to review and modify descriptions before posting to QuickBooks. The PDF is automatically regenerated with the updated descriptions. Only works on Draft invoices.")]
    public async Task<string> UpdateDraftInvoiceItems(
        [Description("The GUID of the draft invoice")] string invoiceId,
        [Description("JSON array of item updates in format: [{\"itemId\": \"guid\", \"description\": \"new description\"}]")] string itemsJson)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            // Parse the items JSON
            var items = JsonSerializer.Deserialize<List<UpdateInvoiceItemRequest>>(itemsJson);
            if (items == null || items.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = "No items provided for update" });
            }

            var request = new { Items = items };

            var response = await client.PutAsJsonAsync($"/api/invoice/{invoiceId}/items", request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating draft invoice items for {InvoiceId}", invoiceId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Post a draft invoice to QuickBooks. This confirms the invoice and creates it in QuickBooks. The invoice must be in Draft status. After posting, the PDF is regenerated with accurate QuickBooks amounts.")]
    public async Task<string> PostInvoiceToQuickBooks(
        [Description("The GUID of the draft invoice to post to QuickBooks")] string invoiceId)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.PostAsync($"/api/invoice/{invoiceId}/post-to-quickbooks", null);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting invoice {InvoiceId} to QuickBooks", invoiceId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}

// Helper class for MCP JSON deserialization
public record UpdateInvoiceItemRequest(Guid ItemId, string Description);
