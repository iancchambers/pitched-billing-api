namespace PitchedBillingApi.Entities;

public class InvoiceHistory
{
    public Guid Id { get; set; }
    public Guid BillingPlanId { get; set; }
    public string? QuickBooksInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public byte[]? PdfContent { get; set; }
    public DateTime? PostedToQuickBooksDate { get; set; }
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public BillingPlan BillingPlan { get; set; } = null!;
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
