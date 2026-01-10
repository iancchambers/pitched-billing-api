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

    [JsonPropertyName("SalesTaxCodeRef")]
    public Reference? SalesTaxCodeRef { get; set; }
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

    [JsonPropertyName("DocNumber")]
    public string? DocNumber { get; set; }

    [JsonPropertyName("PrivateNote")]
    public string? PrivateNote { get; set; }

    [JsonPropertyName("CustomerMemo")]
    public MemoRef? CustomerMemo { get; set; }

    [JsonPropertyName("GlobalTaxCalculation")]
    public string? GlobalTaxCalculation { get; set; }

    [JsonPropertyName("TxnTaxDetail")]
    public TxnTaxDetail? TxnTaxDetail { get; set; }
}

public class TxnTaxDetail
{
    [JsonPropertyName("TxnTaxCodeRef")]
    public Reference? TxnTaxCodeRef { get; set; }

    [JsonPropertyName("TotalTax")]
    public decimal? TotalTax { get; set; }
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
    public decimal? Amount { get; set; }

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

    [JsonPropertyName("TaxCodeRef")]
    public Reference? TaxCodeRef { get; set; }
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

    [JsonPropertyName("TxnTaxDetail")]
    public TxnTaxDetail? TxnTaxDetail { get; set; }

    [JsonPropertyName("Line")]
    public List<InvoiceLineResponse>? Line { get; set; }
}

// Invoice line response (what QB returns)
public class InvoiceLineResponse
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("LineNum")]
    public int? LineNum { get; set; }

    [JsonPropertyName("Amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("DetailType")]
    public string? DetailType { get; set; }

    [JsonPropertyName("SalesItemLineDetail")]
    public SalesItemLineDetailResponse? SalesItemLineDetail { get; set; }

    [JsonPropertyName("Description")]
    public string? Description { get; set; }
}

public class SalesItemLineDetailResponse
{
    [JsonPropertyName("ItemRef")]
    public Reference? ItemRef { get; set; }

    [JsonPropertyName("Qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("UnitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("TaxCodeRef")]
    public Reference? TaxCodeRef { get; set; }

    [JsonPropertyName("TaxInclusiveAmt")]
    public decimal? TaxInclusiveAmt { get; set; }
}

// DTOs for API responses
public record QuickBooksCustomerDto(
    string Id,
    string DisplayName,
    string? CompanyName,
    string? Email,
    string? BillingAddress,
    Address? BillAddr);

public record QuickBooksItemDto(
    string Id,
    string Name,
    string? Description,
    decimal? UnitPrice,
    string Type,
    string? SalesTaxCodeId);

// Customer creation model
public class QuickBooksCustomerCreate
{
    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("CompanyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("PrimaryEmailAddr")]
    public EmailAddress? PrimaryEmailAddr { get; set; }

    [JsonPropertyName("BillAddr")]
    public Address? BillAddr { get; set; }
}

// Customer response wrapper
public class QuickBooksCustomerResponse
{
    [JsonPropertyName("Customer")]
    public QuickBooksCustomer Customer { get; set; } = new();
}

// Customer request models
public record CreateQuickBooksCustomerRequest(
    string DisplayName,
    string? CompanyName,
    string? Email,
    AddressRequest? BillingAddress);

public record AddressRequest(
    string? Line1,
    string? City,
    string? State,
    string? PostalCode,
    string? Country);

// TaxCode model
public class QuickBooksTaxCode
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string? Description { get; set; }

    [JsonPropertyName("Active")]
    public bool Active { get; set; }

    [JsonPropertyName("Taxable")]
    public bool Taxable { get; set; }

    [JsonPropertyName("TaxGroup")]
    public bool TaxGroup { get; set; }

    [JsonPropertyName("SalesTaxRateList")]
    public TaxRateList? SalesTaxRateList { get; set; }

    [JsonPropertyName("PurchaseTaxRateList")]
    public TaxRateList? PurchaseTaxRateList { get; set; }
}

public class TaxRateList
{
    [JsonPropertyName("TaxRateDetail")]
    public List<TaxRateDetail>? TaxRateDetail { get; set; }
}

public class TaxRateDetail
{
    [JsonPropertyName("TaxRateRef")]
    public Reference? TaxRateRef { get; set; }

    [JsonPropertyName("TaxTypeApplicable")]
    public string? TaxTypeApplicable { get; set; }

    [JsonPropertyName("TaxOrder")]
    public int? TaxOrder { get; set; }
}

// TaxRate model
public class QuickBooksTaxRate
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string? Description { get; set; }

    [JsonPropertyName("Active")]
    public bool Active { get; set; }

    [JsonPropertyName("RateValue")]
    public decimal RateValue { get; set; }

    [JsonPropertyName("AgencyRef")]
    public Reference? AgencyRef { get; set; }
}

// Tax code response wrapper
public class QuickBooksTaxCodeResponse
{
    [JsonPropertyName("TaxCode")]
    public QuickBooksTaxCode TaxCode { get; set; } = new();
}

// Tax rate response wrapper
public class QuickBooksTaxRateResponse
{
    [JsonPropertyName("TaxRate")]
    public QuickBooksTaxRate TaxRate { get; set; } = new();
}

// Tax information DTO
public record TaxInfo(
    string TaxCodeId,
    string TaxCodeName,
    decimal TaxRate);
