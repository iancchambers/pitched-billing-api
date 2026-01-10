namespace PitchedBillingApi.Entities;

public class InvoiceItem
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }

    // Item Details
    public string ItemDescription { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty; // QuickBooks Item ID

    // Quantities and Amounts
    public decimal Quantity { get; set; }
    public decimal VatRate { get; set; } // VAT percentage (e.g., 20.0 for 20%)
    public decimal NetAmount { get; set; } // Amount before VAT
    public decimal VatAmount { get; set; } // Calculated VAT
    public decimal TotalAmount { get; set; } // Net + VAT

    public int SortOrder { get; set; }

    // Navigation properties
    public Invoice Invoice { get; set; } = null!;
}
