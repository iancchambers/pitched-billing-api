using System.ComponentModel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace PitchedBillingApi.McpServer.Tools;

[McpServerToolType]
public class BillingPlanTools
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BillingPlanTools> _logger;

    public BillingPlanTools(IHttpClientFactory httpClientFactory, ILogger<BillingPlanTools> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [McpServerTool, Description("Get all billing plans with optional filter for active plans only")]
    public async Task<string> ListBillingPlans(
        [Description("If true, returns only active plans. If false or omitted, returns all plans")] bool? activeOnly = null)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");
        var url = activeOnly.HasValue ? $"/api/billingplan?activeOnly={activeOnly.Value}" : "/api/billingplan";

        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing billing plans");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Get a specific billing plan by ID")]
    public async Task<string> GetBillingPlan(
        [Description("The GUID of the billing plan")] string id)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.GetAsync($"/api/billingplan/{id}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting billing plan {Id}", id);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Create a new billing plan")]
    public async Task<string> CreateBillingPlan(
        [Description("Name of the billing plan")] string planName,
        [Description("QuickBooks customer ID")] string quickBooksCustomerId,
        [Description("Billing frequency: 1=Monthly, 2=Quarterly, 3=Annually, 4=TwoYearly")] int frequency,
        [Description("Start date (ISO 8601 format)")] string startDate,
        [Description("End date (ISO 8601 format)")] string endDate)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        var request = new
        {
            PlanName = planName,
            QuickBooksCustomerId = quickBooksCustomerId,
            Frequency = frequency,
            StartDate = startDate,
            EndDate = endDate
        };

        try
        {
            var response = await client.PostAsJsonAsync("/api/billingplan", request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating billing plan");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Update an existing billing plan")]
    public async Task<string> UpdateBillingPlan(
        [Description("The GUID of the billing plan to update")] string id,
        [Description("Updated start date (ISO 8601 format)")] string startDate,
        [Description("Updated end date (ISO 8601 format)")] string endDate,
        [Description("Updated name of the billing plan")] string? planName = null,
        [Description("Updated billing frequency: 1=Monthly, 2=Quarterly, 3=Annually, 4=TwoYearly")] int? frequency = null,
        [Description("Whether the plan is active")] bool? isActive = null)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        var request = new
        {
            PlanName = planName,
            Frequency = frequency,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = isActive
        };

        try
        {
            var response = await client.PutAsJsonAsync($"/api/billingplan/{id}", request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating billing plan {Id}", id);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Delete a billing plan")]
    public async Task<string> DeleteBillingPlan(
        [Description("The GUID of the billing plan to delete")] string id)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.DeleteAsync($"/api/billingplan/{id}");
            response.EnsureSuccessStatusCode();
            return JsonSerializer.Serialize(new { success = true, message = "Billing plan deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting billing plan {Id}", id);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Get all items for a billing plan")]
    public async Task<string> ListBillingPlanItems(
        [Description("The GUID of the billing plan")] string planId)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.GetAsync($"/api/billingplan/{planId}/items");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing billing plan items for {PlanId}", planId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Add an item to a billing plan. Tax code and VAT rate are fetched from QuickBooks. Common examples: description='Domain Registration for keepwifehappy.co.uk (09/01/2026 to 08/01/2028)', description='Domain Renewal for example.com (01/01/2026 to 01/01/2027)', description='Hosting for mysite.co.uk (01/01/2026 to 31/12/2026)'.")]
    public async Task<string> AddBillingPlanItem(
        [Description("The GUID of the billing plan")] string planId,
        [Description("QuickBooks item ID")] string quickBooksItemId,
        [Description("Full formatted description including item type, domain, and date range (e.g., 'Domain Registration for keepwifehappy.co.uk (09/01/2026 to 08/01/2028)')")] string description,
        [Description("Quantity")] decimal quantity,
        [Description("Rate per unit")] decimal rate,
        [Description("From date (ISO 8601 format) - when this item becomes active")] string fromDate,
        [Description("To date (ISO 8601 format) - when this item becomes inactive")] string toDate,
        [Description("Sort order (default: 0)")] int? sortOrder = null)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        var request = new
        {
            QuickBooksItemId = quickBooksItemId,
            Quantity = quantity,
            Rate = rate,
            Description = description,
            FromDate = fromDate,
            ToDate = toDate
        };

        try
        {
            var response = await client.PostAsJsonAsync($"/api/billingplan/{planId}/items", request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to billing plan {PlanId}", planId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Update an item in a billing plan. Tax code and VAT rate are fetched from QuickBooks. Common examples: description='Domain Registration for keepwifehappy.co.uk (09/01/2026 to 08/01/2028)', description='Domain Renewal for example.com (01/01/2026 to 01/01/2027)', description='Hosting for mysite.co.uk (01/01/2026 to 31/12/2026)'.")]
    public async Task<string> UpdateBillingPlanItem(
        [Description("The GUID of the billing plan")] string planId,
        [Description("The GUID of the item to update")] string itemId,
        [Description("Updated QuickBooks item ID")] string quickBooksItemId,
        [Description("Full formatted description including item type, domain, and date range (e.g., 'Domain Registration for keepwifehappy.co.uk (09/01/2026 to 08/01/2028)')")] string description,
        [Description("Updated quantity")] decimal quantity,
        [Description("Updated rate")] decimal rate,
        [Description("Updated from date (ISO 8601 format)")] string fromDate,
        [Description("Updated to date (ISO 8601 format)")] string toDate,
        [Description("Updated sort order")] int? sortOrder = null)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        var request = new
        {
            QuickBooksItemId = quickBooksItemId,
            Quantity = quantity,
            Rate = rate,
            Description = description,
            FromDate = fromDate,
            ToDate = toDate,
            SortOrder = sortOrder ?? 0
        };

        try
        {
            var response = await client.PutAsJsonAsync($"/api/billingplan/{planId}/items/{itemId}", request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item {ItemId} in billing plan {PlanId}", itemId, planId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Delete an item from a billing plan")]
    public async Task<string> DeleteBillingPlanItem(
        [Description("The GUID of the billing plan")] string planId,
        [Description("The GUID of the item to delete")] string itemId)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.DeleteAsync($"/api/billingplan/{planId}/items/{itemId}");
            response.EnsureSuccessStatusCode();
            return JsonSerializer.Serialize(new { success = true, message = "Billing plan item deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item {ItemId} from billing plan {PlanId}", itemId, planId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
