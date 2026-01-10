namespace PitchedBillingApi.Entities;

public class Invoice
{
    public Guid Id { get; set; }
    public Guid BillingPlanId { get; set; }
    public string? QuickBooksInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public byte[]? PdfContent { get; set; }
    public DateTime? PostedToQuickBooksDate { get; set; }
    public string? ErrorMessage { get; set; }

    // Customer Information
    public string? CustomerName { get; set; }
    public string? CustomerCompanyName { get; set; }
    public string? CustomerEmail { get; set; }

    // Bill-To Address
    public string? BillToLine1 { get; set; }
    public string? BillToCity { get; set; }
    public string? BillToCounty { get; set; }
    public string? BillToPostCode { get; set; }
    public string? BillToCountry { get; set; }

    // Reference Fields
    public string? YourReference { get; set; }
    public string? OurReference { get; set; }
    public string? AccountHandler { get; set; }

    // Navigation properties
    public BillingPlan BillingPlan { get; set; } = null!;
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<EmailDeliveryStatus> EmailDeliveries { get; set; } = new List<EmailDeliveryStatus>();
}

public enum InvoiceStatus
{
    Draft = 1,
    Generated = 2,
    Posted = 3,
    Failed = 4,
    Cancelled = 5
}
