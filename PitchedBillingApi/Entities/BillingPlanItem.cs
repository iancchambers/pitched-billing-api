namespace PitchedBillingApi.Entities;

public class BillingPlanItem
{
    public Guid Id { get; set; }
    public Guid BillingPlanId { get; set; }
    public string QuickBooksItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    // Navigation property
    public BillingPlan BillingPlan { get; set; } = null!;
}
