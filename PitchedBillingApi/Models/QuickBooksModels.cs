using System.Text.Json.Serialization;

namespace PitchedBillingApi.Models;

// Configuration
public class QuickBooksConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? RealmId { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}

// OAuth responses
public class QuickBooksTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("x_refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; }
}

// API responses
public class QuickBooksQueryResponse<T>
{
    [JsonPropertyName("QueryResponse")]
    public QueryResponseData<T> QueryResponse { get; set; } = new();
}

public class QueryResponseData<T>
{
    [JsonPropertyName("Customer")]
    public List<T>? Customer { get; set; }

    [JsonPropertyName("Item")]
    public List<T>? Item { get; set; }

    [JsonPropertyName("startPosition")]
    public int StartPosition { get; set; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; }
}

// Customer model
public class QuickBooksCustomer
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("CompanyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("PrimaryEmailAddr")]
    public EmailAddress? PrimaryEmailAddr { get; set; }

    [JsonPropertyName("BillAddr")]
    public Address? BillAddr { get; set; }

    [JsonPropertyName("Active")]
    public bool Active { get; set; }

    [JsonPropertyName("SyncToken")]
    public string SyncToken { get; set; } = string.Empty;
}

public class EmailAddress
{
    [JsonPropertyName("Address")]
    public string? Address { get; set; }
}

public class Address
{
    [JsonPropertyName("Line1")]
    public string? Line1 { get; set; }

    [JsonPropertyName("City")]
    public string? City { get; set; }

    [JsonPropertyName("CountrySubDivisionCode")]
    public string? State { get; set; }

    [JsonPropertyName("PostalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("Country")]
    public string? Country { get; set; }
}

// Item model
public class QuickBooksItem
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string? Description { get; set; }

    [JsonPropertyName("UnitPrice")]
    public decimal? UnitPrice { get; set; }

    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("Active")]
    public bool Active { get; set; }

    [JsonPropertyName("SyncToken")]
    public string SyncToken { get; set; } = string.Empty;
}

// Invoice model for posting
public class QuickBooksInvoiceCreate
{
    [JsonPropertyName("CustomerRef")]
    public Reference CustomerRef { get; set; } = new();

    [JsonPropertyName("Line")]
    public List<InvoiceLine> Line { get; set; } = new();

    [JsonPropertyName("DueDate")]
    public string? DueDate { get; set; }

    [JsonPropertyName("PrivateNote")]
    public string? PrivateNote { get; set; }

    [JsonPropertyName("CustomerMemo")]
    public MemoRef? CustomerMemo { get; set; }
}

public class Reference
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class MemoRef
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class InvoiceLine
{
    [JsonPropertyName("Amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("DetailType")]
    public string DetailType { get; set; } = "SalesItemLineDetail";

    [JsonPropertyName("SalesItemLineDetail")]
    public SalesItemLineDetail? SalesItemLineDetail { get; set; }

    [JsonPropertyName("Description")]
    public string? Description { get; set; }
}

public class SalesItemLineDetail
{
    [JsonPropertyName("ItemRef")]
    public Reference ItemRef { get; set; } = new();

    [JsonPropertyName("Qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("UnitPrice")]
    public decimal UnitPrice { get; set; }
}

// Invoice response
public class QuickBooksInvoiceResponse
{
    [JsonPropertyName("Invoice")]
    public QuickBooksInvoice Invoice { get; set; } = new();
}

public class QuickBooksInvoice
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("DocNumber")]
    public string? DocNumber { get; set; }

    [JsonPropertyName("TotalAmt")]
    public decimal TotalAmt { get; set; }

    [JsonPropertyName("SyncToken")]
    public string SyncToken { get; set; } = string.Empty;
}

// DTOs for API responses
public record QuickBooksCustomerDto(
    string Id,
    string DisplayName,
    string? CompanyName,
    string? Email,
    string? BillingAddress);

public record QuickBooksItemDto(
    string Id,
    string Name,
    string? Description,
    decimal? UnitPrice,
    string Type);
