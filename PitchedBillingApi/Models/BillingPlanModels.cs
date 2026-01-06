using PitchedBillingApi.Entities;

namespace PitchedBillingApi.Models;

// Request models
public record CreateBillingPlanRequest(
    string PlanName,
    string QuickBooksCustomerId,
    BillingFrequency Frequency,
    DateTime StartDate,
    DateTime? EndDate);

public record UpdateBillingPlanRequest(
    string PlanName,
    BillingFrequency Frequency,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive);

public record CreateBillingPlanItemRequest(
    string QuickBooksItemId,
    string ItemName,
    decimal Quantity,
    decimal Rate,
    string? Description);

public record UpdateBillingPlanItemRequest(
    string QuickBooksItemId,
    string ItemName,
    decimal Quantity,
    decimal Rate,
    string? Description,
    int SortOrder);

// Response models
public record BillingPlanResponse(
    Guid Id,
    string PlanName,
    string QuickBooksCustomerId,
    BillingFrequency Frequency,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    DateTime CreatedDate,
    DateTime? ModifiedDate,
    List<BillingPlanItemResponse> Items);

public record BillingPlanItemResponse(
    Guid Id,
    string QuickBooksItemId,
    string ItemName,
    decimal Quantity,
    decimal Rate,
    string? Description,
    int SortOrder,
    decimal LineTotal);

// Mapping extension methods
public static class BillingPlanMappings
{
    public static BillingPlanResponse ToResponse(this BillingPlan plan)
    {
        return new BillingPlanResponse(
            plan.Id,
            plan.PlanName,
            plan.QuickBooksCustomerId,
            plan.Frequency,
            plan.StartDate,
            plan.EndDate,
            plan.IsActive,
            plan.CreatedDate,
            plan.ModifiedDate,
            plan.Items.Select(i => i.ToResponse()).ToList());
    }

    public static BillingPlanItemResponse ToResponse(this BillingPlanItem item)
    {
        return new BillingPlanItemResponse(
            item.Id,
            item.QuickBooksItemId,
            item.ItemName,
            item.Quantity,
            item.Rate,
            item.Description,
            item.SortOrder,
            item.Quantity * item.Rate);
    }
}
