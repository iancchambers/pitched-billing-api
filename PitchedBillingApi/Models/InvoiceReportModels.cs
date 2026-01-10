namespace PitchedBillingApi.Models;

/// <summary>
/// Data model for generating invoice PDF reports
/// </summary>
public class InvoiceReportData
{
    // Invoice ID (for report parameter lookup)
    public Guid InvoiceId { get; set; }

    // Invoice Header
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string? YourReference { get; set; }
    public string? OurReference { get; set; }
    public string? Contact { get; set; }
    public string? AccountHandler { get; set; }

    // Customer Details
    public string CustomerName { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? County { get; set; }
    public string? PostCode { get; set; }

    // Invoice Description
    public string? InvoiceTitle { get; set; }

    // Line Items
    public List<InvoiceLineItem> LineItems { get; set; } = new();

    // Totals
    public decimal SubTotal { get; set; }
    public decimal VatRate { get; set; } = 20m;
    public decimal VatTotal { get; set; }
    public decimal Total { get; set; }

    // Payment Terms
    public int PaymentTermsDays { get; set; } = 14;
    public DateTime DueDate { get; set; }

    // Payment Details
    public string BankName { get; set; } = "HSBC";
    public string SortCode { get; set; } = "40-44-34";
    public string AccountNumber { get; set; } = "51865293";

    // Company Details (Footer)
    public string CompanyEmail { get; set; } = "contact@pitched.co.uk";
    public string CompanyPhone { get; set; } = "01726 418 118";
    public string CompanyWebsite { get; set; } = "www.pitched.co.uk";
    public string CompanyAddress { get; set; } = "Summerleaze, St Ingunger Country Offices, Lanivet, Cornwall, PL30 5HS";
    public string CompanyRegistration { get; set; } = "Pitched is the trading name of Pitched Applications Limited, a company incorporated in England and Wales with registered number 10753229 and VAT registered number 271174219.";
}

public class InvoiceLineItem
{
    public string Description { get; set; } = string.Empty;
    public string? SubDescription { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Rate { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// Request to generate an invoice
/// </summary>
public record GenerateInvoiceRequest(
    Guid BillingPlanId,
    DateTime? InvoiceDate = null,
    string? YourReference = null,
    string? OurReference = null,
    string? AccountHandler = null,
    bool SendEmail = false);

/// <summary>
/// Response from invoice generation
/// </summary>
public record GenerateInvoiceResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal TotalAmount,
    string? QuickBooksInvoiceId,
    List<InvoiceItemResponse> Items);

/// <summary>
/// Invoice item for response
/// </summary>
public record InvoiceItemResponse(
    Guid ItemId,
    string Description,
    decimal Quantity,
    decimal Rate,
    decimal NetAmount,
    decimal VatRate,
    decimal VatAmount,
    decimal TotalAmount);

/// <summary>
/// Request to resend an invoice email
/// </summary>
public record ResendRequest(string? RecipientEmail);

/// <summary>
/// Request to update invoice item descriptions on a draft invoice
/// </summary>
public record UpdateInvoiceItemRequest(
    Guid ItemId,
    string Description);

/// <summary>
/// Request to update multiple invoice items
/// </summary>
public record UpdateInvoiceItemsRequest(
    List<UpdateInvoiceItemRequest> Items);
