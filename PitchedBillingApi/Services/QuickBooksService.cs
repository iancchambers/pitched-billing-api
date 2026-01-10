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
    Task<QuickBooksCustomer> CreateCustomerAsync(QuickBooksCustomerCreate customer);
    Task<List<QuickBooksItemDto>> GetItemsAsync();
    Task<QuickBooksItemDto?> GetItemAsync(string itemId);
    Task<QuickBooksInvoice> CreateInvoiceAsync(QuickBooksInvoiceCreate invoice);
    Task<TaxInfo?> GetTaxInfoAsync(string taxCodeId);
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
            FormatAddress(c.BillAddr),
            c.BillAddr
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
            FormatAddress(customer.BillAddr),
            customer.BillAddr
        );
    }

    public async Task<QuickBooksCustomer> CreateCustomerAsync(QuickBooksCustomerCreate customer)
    {
        var realmId = _authService.RealmId
            ?? throw new InvalidOperationException("Not connected to QuickBooks");

        var accessToken = await _authService.GetValidAccessTokenAsync();
        var url = $"{_baseUrl}/{realmId}/customer?minorversion=65";

        var jsonContent = JsonSerializer.Serialize(customer, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger.LogInformation("Creating customer in QuickBooks: {DisplayName}", customer.DisplayName);
        _logger.LogDebug("QuickBooks customer request URL: {Url}", url);
        _logger.LogDebug("QuickBooks customer request body: {Body}", jsonContent);

        var response = await _httpClient.SendAsync(request);

        _logger.LogInformation("QuickBooks response status: {StatusCode}", response.StatusCode);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("QuickBooks token expired, refreshing...");
            await _authService.RefreshAccessTokenAsync();
            return await CreateCustomerAsync(customer);
        }

        var content = await response.Content.ReadAsStringAsync();
        _logger.LogDebug("QuickBooks response content: {Content}", content);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to create customer in QuickBooks: Status={Status}, Content={Error}",
                response.StatusCode, content);
            throw new InvalidOperationException($"Failed to create customer (HTTP {response.StatusCode}): {content}");
        }

        var result = JsonSerializer.Deserialize<QuickBooksCustomerResponse>(content)
            ?? throw new InvalidOperationException($"Failed to deserialize customer response. Content: {content}");

        _logger.LogInformation("Customer created in QuickBooks: {CustomerId}", result.Customer.Id);

        return result.Customer;
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
            i.Type,
            i.SalesTaxCodeRef?.Value
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
            item.Type,
            item.SalesTaxCodeRef?.Value
        );
    }

    public async Task<QuickBooksInvoice> CreateInvoiceAsync(QuickBooksInvoiceCreate invoice)
    {
        var realmId = _authService.RealmId
            ?? throw new InvalidOperationException("Not connected to QuickBooks");

        var accessToken = await _authService.GetValidAccessTokenAsync();
        var url = $"{_baseUrl}/{realmId}/invoice?minorversion=65";

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
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger.LogInformation("Creating invoice in QuickBooks for customer {CustomerId}",
            invoice.CustomerRef.Value);
        _logger.LogInformation("QuickBooks invoice request URL: {Url}", url);
        _logger.LogInformation("QuickBooks invoice request body: {Body}", jsonContent);

        var response = await _httpClient.SendAsync(request);

        _logger.LogInformation("QuickBooks response status: {StatusCode}", response.StatusCode);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("QuickBooks token expired, refreshing...");
            await _authService.RefreshAccessTokenAsync();
            return await CreateInvoiceAsync(invoice);
        }

        var content = await response.Content.ReadAsStringAsync();
        _logger.LogDebug("QuickBooks response content: {Content}", content);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to create invoice in QuickBooks: Status={Status}, Content={Error}",
                response.StatusCode, content);
            throw new InvalidOperationException($"Failed to create invoice (HTTP {response.StatusCode}): {content}");
        }

        var result = JsonSerializer.Deserialize<QuickBooksInvoiceResponse>(content)
            ?? throw new InvalidOperationException($"Failed to deserialize invoice response. Content: {content}");

        _logger.LogInformation("Invoice created in QuickBooks: {InvoiceId}", result.Invoice.Id);

        return result.Invoice;
    }

    public async Task<TaxInfo?> GetTaxInfoAsync(string taxCodeId)
    {
        var realmId = _authService.RealmId
            ?? throw new InvalidOperationException("Not connected to QuickBooks");

        var accessToken = await _authService.GetValidAccessTokenAsync();
        var url = $"{_baseUrl}/{realmId}/taxcode/{taxCodeId}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authService.RefreshAccessTokenAsync();
            return await GetTaxInfoAsync(taxCodeId);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Tax code {TaxCodeId} not found in QuickBooks", taxCodeId);
            return null;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        _logger.LogDebug("Tax code response: {Content}", content);

        using var doc = JsonDocument.Parse(content);
        var taxCodeJson = doc.RootElement.GetProperty("TaxCode").GetRawText();
        var taxCode = JsonSerializer.Deserialize<QuickBooksTaxCode>(taxCodeJson);

        if (taxCode == null)
        {
            _logger.LogWarning("Failed to deserialize tax code {TaxCodeId}", taxCodeId);
            return null;
        }

        // Calculate total tax rate from all tax rate details
        decimal totalRate = 0m;

        if (taxCode.SalesTaxRateList?.TaxRateDetail != null)
        {
            foreach (var rateDetail in taxCode.SalesTaxRateList.TaxRateDetail)
            {
                if (rateDetail.TaxRateRef?.Value != null)
                {
                    var taxRate = await GetTaxRateAsync(rateDetail.TaxRateRef.Value);
                    if (taxRate != null)
                    {
                        totalRate += taxRate.RateValue;
                        _logger.LogDebug("Added tax rate {RateName}: {RateValue}%",
                            taxRate.Name, taxRate.RateValue);
                    }
                }
            }
        }

        _logger.LogInformation("Tax code {TaxCodeId} ({TaxCodeName}) has total rate: {TotalRate}%",
            taxCode.Id, taxCode.Name, totalRate);

        return new TaxInfo(taxCode.Id, taxCode.Name, totalRate);
    }

    private async Task<QuickBooksTaxRate?> GetTaxRateAsync(string taxRateId)
    {
        var realmId = _authService.RealmId
            ?? throw new InvalidOperationException("Not connected to QuickBooks");

        var accessToken = await _authService.GetValidAccessTokenAsync();
        var url = $"{_baseUrl}/{realmId}/taxrate/{taxRateId}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authService.RefreshAccessTokenAsync();
            return await GetTaxRateAsync(taxRateId);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Tax rate {TaxRateId} not found in QuickBooks", taxRateId);
            return null;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var taxRateJson = doc.RootElement.GetProperty("TaxRate").GetRawText();
        return JsonSerializer.Deserialize<QuickBooksTaxRate>(taxRateJson);
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
