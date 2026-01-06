using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PitchedBillingApi.Models;

namespace PitchedBillingApi.Services;

public interface IQuickBooksService
{
    Task<List<QuickBooksCustomerDto>> GetCustomersAsync();
    Task<QuickBooksCustomerDto?> GetCustomerAsync(string customerId);
    Task<List<QuickBooksItemDto>> GetItemsAsync();
    Task<QuickBooksItemDto?> GetItemAsync(string itemId);
    Task<QuickBooksInvoice> CreateInvoiceAsync(QuickBooksInvoiceCreate invoice);
}

public class QuickBooksService : IQuickBooksService
{
    private readonly HttpClient _httpClient;
    private readonly IQuickBooksAuthService _authService;
    private readonly ILogger<QuickBooksService> _logger;
    private readonly string _baseUrl;

    public QuickBooksService(
        HttpClient httpClient,
        IQuickBooksAuthService authService,
        IConfiguration configuration,
        ILogger<QuickBooksService> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
        _baseUrl = configuration["quickbooks-base-url"]
            ?? throw new InvalidOperationException("QuickBooks base URL not configured");
    }

    public async Task<List<QuickBooksCustomerDto>> GetCustomersAsync()
    {
        var realmId = _authService.RealmId
            ?? throw new InvalidOperationException("Not connected to QuickBooks");

        var accessToken = await _authService.GetValidAccessTokenAsync();
        var url = $"{_baseUrl}/{realmId}/query?query=SELECT * FROM Customer WHERE Active = true";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Token expired, refreshing and retrying...");
            await _authService.RefreshAccessTokenAsync();
            return await GetCustomersAsync();
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<QuickBooksQueryResponse<QuickBooksCustomer>>(content);

        var customers = result?.QueryResponse.Customer ?? new List<QuickBooksCustomer>();

        return customers.Select(c => new QuickBooksCustomerDto(
            c.Id,
            c.DisplayName,
            c.CompanyName,
            c.PrimaryEmailAddr?.Address,
            FormatAddress(c.BillAddr)
        )).ToList();
    }

    public async Task<QuickBooksCustomerDto?> GetCustomerAsync(string customerId)
    {
        var realmId = _authService.RealmId
            ?? throw new InvalidOperationException("Not connected to QuickBooks");

        var accessToken = await _authService.GetValidAccessTokenAsync();
        var url = $"{_baseUrl}/{realmId}/customer/{customerId}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authService.RefreshAccessTokenAsync();
            return await GetCustomerAsync(customerId);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        // The response wraps customer in a "Customer" property
        using var doc = JsonDocument.Parse(content);
        var customerJson = doc.RootElement.GetProperty("Customer").GetRawText();
        var customer = JsonSerializer.Deserialize<QuickBooksCustomer>(customerJson);

        if (customer == null) return null;

        return new QuickBooksCustomerDto(
            customer.Id,
            customer.DisplayName,
            customer.CompanyName,
            customer.PrimaryEmailAddr?.Address,
            FormatAddress(customer.BillAddr)
        );
    }

    public async Task<List<QuickBooksItemDto>> GetItemsAsync()
    {
        var realmId = _authService.RealmId
            ?? throw new InvalidOperationException("Not connected to QuickBooks");

        var accessToken = await _authService.GetValidAccessTokenAsync();
        var url = $"{_baseUrl}/{realmId}/query?query=SELECT * FROM Item WHERE Active = true";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authService.RefreshAccessTokenAsync();
            return await GetItemsAsync();
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<QuickBooksQueryResponse<QuickBooksItem>>(content);

        var items = result?.QueryResponse.Item ?? new List<QuickBooksItem>();

        return items.Select(i => new QuickBooksItemDto(
            i.Id,
            i.Name,
            i.Description,
            i.UnitPrice,
            i.Type
        )).ToList();
    }

    public async Task<QuickBooksItemDto?> GetItemAsync(string itemId)
    {
        var realmId = _authService.RealmId
            ?? throw new InvalidOperationException("Not connected to QuickBooks");

        var accessToken = await _authService.GetValidAccessTokenAsync();
        var url = $"{_baseUrl}/{realmId}/item/{itemId}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authService.RefreshAccessTokenAsync();
            return await GetItemAsync(itemId);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var itemJson = doc.RootElement.GetProperty("Item").GetRawText();
        var item = JsonSerializer.Deserialize<QuickBooksItem>(itemJson);

        if (item == null) return null;

        return new QuickBooksItemDto(
            item.Id,
            item.Name,
            item.Description,
            item.UnitPrice,
            item.Type
        );
    }

    public async Task<QuickBooksInvoice> CreateInvoiceAsync(QuickBooksInvoiceCreate invoice)
    {
        var realmId = _authService.RealmId
            ?? throw new InvalidOperationException("Not connected to QuickBooks");

        var accessToken = await _authService.GetValidAccessTokenAsync();
        var url = $"{_baseUrl}/{realmId}/invoice";

        var jsonContent = JsonSerializer.Serialize(invoice, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // Keep exact property names
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        _logger.LogInformation("Creating invoice in QuickBooks for customer {CustomerId}",
            invoice.CustomerRef.Value);

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authService.RefreshAccessTokenAsync();
            return await CreateInvoiceAsync(invoice);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to create invoice in QuickBooks: {Error}", errorContent);
            throw new InvalidOperationException($"Failed to create invoice: {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<QuickBooksInvoiceResponse>(content)
            ?? throw new InvalidOperationException("Failed to deserialize invoice response");

        _logger.LogInformation("Invoice created in QuickBooks: {InvoiceId}", result.Invoice.Id);

        return result.Invoice;
    }

    private static string? FormatAddress(Address? addr)
    {
        if (addr == null) return null;

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(addr.Line1)) parts.Add(addr.Line1);
        if (!string.IsNullOrEmpty(addr.City)) parts.Add(addr.City);
        if (!string.IsNullOrEmpty(addr.State)) parts.Add(addr.State);
        if (!string.IsNullOrEmpty(addr.PostalCode)) parts.Add(addr.PostalCode);

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }
}
