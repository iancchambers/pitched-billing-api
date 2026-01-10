namespace PitchedBillingApi.Entities;

public class BillingPlan
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string QuickBooksCustomerId { get; set; } = string.Empty;
    public BillingFrequency Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    // Navigation properties
    public ICollection<BillingPlanItem> Items { get; set; } = new List<BillingPlanItem>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public enum BillingFrequency
{
    Monthly = 1,
    Quarterly = 2,
    Annually = 3,
    TwoYearly = 4
}
