namespace PitchedBillingApi.Entities;

public class EmailDeliveryStatus
{
    public Guid Id { get; set; }
    public Guid InvoiceHistoryId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public DateTime SentDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public string? MailgunMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }

    // Navigation property
    public InvoiceHistory InvoiceHistory { get; set; } = null!;
}

public enum EmailStatus
{
    Pending = 1,
    Sent = 2,
    Delivered = 3,
    Failed = 4,
    Bounced = 5
}
