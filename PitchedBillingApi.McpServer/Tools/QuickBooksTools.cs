using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace PitchedBillingApi.McpServer.Tools;

[McpServerToolType]
public class QuickBooksTools
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<QuickBooksTools> _logger;

    public QuickBooksTools(IHttpClientFactory httpClientFactory, ILogger<QuickBooksTools> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [McpServerTool, Description("Get the QuickBooks OAuth authorization URL")]
    public async Task<string> GetAuthorizationUrl()
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.GetAsync("/api/quickbooks/auth/authorize");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting QuickBooks authorization URL");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Check if QuickBooks is connected and view token status")]
    public async Task<string> GetAuthStatus()
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.GetAsync("/api/quickbooks/auth/status");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting QuickBooks auth status");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Disconnect from QuickBooks by revoking tokens")]
    public async Task<string> Disconnect()
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.PostAsync("/api/quickbooks/auth/disconnect", null);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from QuickBooks");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Get all QuickBooks customers")]
    public async Task<string> ListCustomers()
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.GetAsync("/api/quickbooks/customers");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing QuickBooks customers");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Get a specific QuickBooks customer by ID")]
    public async Task<string> GetCustomer(
        [Description("The QuickBooks customer ID")] string customerId)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.GetAsync($"/api/quickbooks/customers/{customerId}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting QuickBooks customer {CustomerId}", customerId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Create a new QuickBooks customer - Returns 501 Not Implemented. Customer creation must be done directly in QuickBooks as it is the source of truth. This API only supports billing plan setup for existing customers.")]
    public async Task<string> CreateCustomer(
        [Description("Display name for the customer")] string displayName,
        [Description("Optional company name")] string? companyName = null,
        [Description("Optional first name")] string? givenName = null,
        [Description("Optional last name")] string? familyName = null,
        [Description("Optional email address")] string? primaryEmail = null,
        [Description("Optional phone number")] string? primaryPhone = null,
        [Description("Optional billing address line 1")] string? addressLine1 = null,
        [Description("Optional billing address line 2")] string? addressLine2 = null,
        [Description("Optional billing address city")] string? addressCity = null,
        [Description("Optional billing address state/province code")] string? addressCountrySubDivisionCode = null,
        [Description("Optional billing address postal code")] string? addressPostalCode = null,
        [Description("Optional billing address country")] string? addressCountry = null)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        var address = (addressLine1 != null || addressLine2 != null || addressCity != null ||
                      addressCountrySubDivisionCode != null || addressPostalCode != null || addressCountry != null)
            ? new
            {
                Line1 = addressLine1,
                Line2 = addressLine2,
                City = addressCity,
                CountrySubDivisionCode = addressCountrySubDivisionCode,
                PostalCode = addressPostalCode,
                Country = addressCountry
            }
            : null;

        var request = new
        {
            DisplayName = displayName,
            CompanyName = companyName,
            GivenName = givenName,
            FamilyName = familyName,
            PrimaryEmailAddr = primaryEmail,
            PrimaryPhone = primaryPhone,
            BillAddr = address
        };

        try
        {
            var response = await client.PostAsJsonAsync("/api/quickbooks/customers", request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating QuickBooks customer");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Get all QuickBooks items (products/services)")]
    public async Task<string> ListItems()
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.GetAsync("/api/quickbooks/items");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing QuickBooks items");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Get a specific QuickBooks item by ID")]
    public async Task<string> GetItem(
        [Description("The QuickBooks item ID")] string itemId)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.GetAsync($"/api/quickbooks/items/{itemId}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting QuickBooks item {ItemId}", itemId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool, Description("Get tax code information from QuickBooks including the calculated tax rate")]
    public async Task<string> GetTaxInfo(
        [Description("The QuickBooks tax code ID")] string taxCodeId)
    {
        var client = _httpClientFactory.CreateClient("PitchedBillingApi");

        try
        {
            var response = await client.GetAsync($"/api/quickbooks/taxcodes/{taxCodeId}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting QuickBooks tax code {TaxCodeId}", taxCodeId);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
