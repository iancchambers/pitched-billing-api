using Microsoft.EntityFrameworkCore;
using PitchedBillingApi.Data;
using PitchedBillingApi.Entities;
using PitchedBillingApi.Models;

namespace PitchedBillingApi.Services;

public interface IInvoiceOrchestrationService
{
    Task<GenerateInvoiceResponse> GenerateInvoiceAsync(GenerateInvoiceRequest request);
    Task<InvoiceHistoryResponse?> GetInvoiceAsync(Guid invoiceId);
    Task<List<InvoiceHistoryResponse>> GetInvoiceHistoryAsync(Guid billingPlanId);
    Task<byte[]?> GetInvoicePdfAsync(Guid invoiceId);
    Task<ResendInvoiceResponse> ResendInvoiceAsync(Guid invoiceId, string? recipientEmail = null);
}

public class InvoiceOrchestrationService : IInvoiceOrchestrationService
{
    private readonly BillingDbContext _db;
    private readonly IQuickBooksService _quickBooksService;
    private readonly IReportingService _reportingService;
    private readonly IMailgunService _mailgunService;
    private readonly ILogger<InvoiceOrchestrationService> _logger;

    public InvoiceOrchestrationService(
        BillingDbContext db,
        IQuickBooksService quickBooksService,
        IReportingService reportingService,
        IMailgunService mailgunService,
        ILogger<InvoiceOrchestrationService> logger)
    {
        _db = db;
        _quickBooksService = quickBooksService;
        _reportingService = reportingService;
        _mailgunService = mailgunService;
        _logger = logger;
    }

    public async Task<GenerateInvoiceResponse> GenerateInvoiceAsync(GenerateInvoiceRequest request)
    {
        _logger.LogInformation("Starting invoice generation for billing plan {PlanId}", request.BillingPlanId);

        // 1. Fetch billing plan with items
        var billingPlan = await _db.BillingPlans
            .Include(p => p.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == request.BillingPlanId)
            ?? throw new InvalidOperationException($"Billing plan {request.BillingPlanId} not found");

        if (!billingPlan.IsActive)
        {
            throw new InvalidOperationException("Cannot generate invoice for inactive billing plan");
        }

        // 2. Fetch customer from QuickBooks
        var customer = await _quickBooksService.GetCustomerAsync(billingPlan.QuickBooksCustomerId)
            ?? throw new InvalidOperationException($"Customer {billingPlan.QuickBooksCustomerId} not found in QuickBooks");

        // 3. Calculate totals
        var invoiceDate = request.InvoiceDate ?? DateTime.UtcNow.Date;
        var dueDate = invoiceDate.AddDays(14);
        var subTotal = billingPlan.Items.Sum(i => i.Quantity * i.Rate);
        var vatTotal = Math.Round(subTotal * 0.20m, 2);
        var total = subTotal + vatTotal;

        // 4. Generate invoice number
        var invoiceNumber = await GenerateInvoiceNumberAsync();

        // 5. Build report data
        var reportData = new InvoiceReportData
        {
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            YourReference = request.YourReference,
            OurReference = request.OurReference,
            AccountHandler = request.AccountHandler,
            CustomerName = BuildCustomerAddress(customer),
            SubTotal = subTotal,
            VatTotal = vatTotal,
            Total = total,
            LineItems = billingPlan.Items.Select(i => new InvoiceLineItem
            {
                Description = i.ItemName,
                SubDescription = i.Description,
                Quantity = i.Quantity,
                Rate = i.Rate,
                Amount = i.Quantity * i.Rate
            }).ToList()
        };

        // 6. Generate PDF
        var pdfContent = _reportingService.GenerateInvoicePdf(reportData);

        // 7. Create invoice history record
        var invoiceHistory = new InvoiceHistory
        {
            Id = Guid.NewGuid(),
            BillingPlanId = request.BillingPlanId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            TotalAmount = total,
            Status = InvoiceStatus.Generated,
            PdfContent = pdfContent,
            GeneratedDate = DateTime.UtcNow
        };

        _db.InvoiceHistories.Add(invoiceHistory);

        // 8. Post to QuickBooks
        string? quickBooksInvoiceId = null;
        try
        {
            var qbInvoice = new QuickBooksInvoiceCreate
            {
                CustomerRef = new Reference { Value = billingPlan.QuickBooksCustomerId },
                DueDate = dueDate.ToString("yyyy-MM-dd"),
                Line = billingPlan.Items.Select(i => new InvoiceLine
                {
                    Amount = i.Quantity * i.Rate,
                    DetailType = "SalesItemLineDetail",
                    Description = i.Description ?? i.ItemName,
                    SalesItemLineDetail = new SalesItemLineDetail
                    {
                        ItemRef = new Reference { Value = i.QuickBooksItemId },
                        Qty = i.Quantity,
                        UnitPrice = i.Rate
                    }
                }).ToList()
            };

            var createdInvoice = await _quickBooksService.CreateInvoiceAsync(qbInvoice);
            quickBooksInvoiceId = createdInvoice.Id;
            invoiceHistory.QuickBooksInvoiceId = quickBooksInvoiceId;
            invoiceHistory.PostedToQuickBooksDate = DateTime.UtcNow;
            invoiceHistory.Status = InvoiceStatus.Posted;

            _logger.LogInformation("Invoice posted to QuickBooks with ID {QBInvoiceId}", quickBooksInvoiceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post invoice to QuickBooks");
            invoiceHistory.ErrorMessage = $"Failed to post to QuickBooks: {ex.Message}";
        }

        // 9. Send email if customer has email address
        if (!string.IsNullOrEmpty(customer.Email))
        {
            var emailResult = await _mailgunService.SendInvoiceEmailAsync(
                customer.Email,
                customer.DisplayName,
                invoiceNumber,
                total,
                pdfContent);

            var emailDelivery = new EmailDeliveryStatus
            {
                Id = Guid.NewGuid(),
                InvoiceHistoryId = invoiceHistory.Id,
                RecipientEmail = customer.Email,
                SentDate = DateTime.UtcNow,
                Status = emailResult.Success ? EmailStatus.Sent : EmailStatus.Failed,
                MailgunMessageId = emailResult.MessageId,
                ErrorMessage = emailResult.ErrorMessage
            };

            _db.EmailDeliveryStatuses.Add(emailDelivery);

            _logger.LogInformation("Email {Status} to {Email}",
                emailResult.Success ? "sent" : "failed", customer.Email);
        }
        else
        {
            _logger.LogWarning("Customer {CustomerId} has no email address, skipping email",
                billingPlan.QuickBooksCustomerId);
        }

        await _db.SaveChangesAsync();

        return new GenerateInvoiceResponse(
            invoiceHistory.Id,
            invoiceNumber,
            total,
            quickBooksInvoiceId);
    }

    public async Task<InvoiceHistoryResponse?> GetInvoiceAsync(Guid invoiceId)
    {
        var invoice = await _db.InvoiceHistories
            .Include(i => i.EmailDeliveries)
            .Include(i => i.BillingPlan)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        return invoice?.ToResponse();
    }

    public async Task<List<InvoiceHistoryResponse>> GetInvoiceHistoryAsync(Guid billingPlanId)
    {
        var invoices = await _db.InvoiceHistories
            .Include(i => i.EmailDeliveries)
            .Where(i => i.BillingPlanId == billingPlanId)
            .OrderByDescending(i => i.GeneratedDate)
            .ToListAsync();

        return invoices.Select(i => i.ToResponse()).ToList();
    }

    public async Task<byte[]?> GetInvoicePdfAsync(Guid invoiceId)
    {
        var invoice = await _db.InvoiceHistories.FindAsync(invoiceId);
        return invoice?.PdfContent;
    }

    public async Task<ResendInvoiceResponse> ResendInvoiceAsync(Guid invoiceId, string? recipientEmail = null)
    {
        var invoice = await _db.InvoiceHistories
            .Include(i => i.BillingPlan)
            .FirstOrDefaultAsync(i => i.Id == invoiceId)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found");

        if (invoice.PdfContent == null || invoice.PdfContent.Length == 0)
        {
            throw new InvalidOperationException("Invoice PDF content not found");
        }

        // If no email provided, fetch from QuickBooks
        if (string.IsNullOrEmpty(recipientEmail))
        {
            var customer = await _quickBooksService.GetCustomerAsync(invoice.BillingPlan.QuickBooksCustomerId);
            recipientEmail = customer?.Email
                ?? throw new InvalidOperationException("No recipient email provided and customer has no email address");
        }

        var emailResult = await _mailgunService.SendInvoiceEmailAsync(
            recipientEmail,
            "Customer",
            invoice.InvoiceNumber,
            invoice.TotalAmount,
            invoice.PdfContent);

        var emailDelivery = new EmailDeliveryStatus
        {
            Id = Guid.NewGuid(),
            InvoiceHistoryId = invoiceId,
            RecipientEmail = recipientEmail,
            SentDate = DateTime.UtcNow,
            Status = emailResult.Success ? EmailStatus.Sent : EmailStatus.Failed,
            MailgunMessageId = emailResult.MessageId,
            ErrorMessage = emailResult.ErrorMessage
        };

        _db.EmailDeliveryStatuses.Add(emailDelivery);
        await _db.SaveChangesAsync();

        return new ResendInvoiceResponse(
            emailResult.Success,
            recipientEmail,
            emailResult.ErrorMessage);
    }

    private async Task<string> GenerateInvoiceNumberAsync()
    {
        // Format: INV-YYYYMM-XXXX where XXXX is sequential within the month
        var yearMonth = DateTime.UtcNow.ToString("yyyyMM");
        var prefix = $"INV-{yearMonth}-";

        var lastInvoice = await _db.InvoiceHistories
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        var nextNumber = 1;
        if (lastInvoice != null && lastInvoice.InvoiceNumber.Length > prefix.Length)
        {
            var lastNumberStr = lastInvoice.InvoiceNumber[prefix.Length..];
            if (int.TryParse(lastNumberStr, out var lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"{prefix}{nextNumber:D4}";
    }

    private static string BuildCustomerAddress(QuickBooksCustomerDto customer)
    {
        var lines = new List<string> { customer.DisplayName };

        if (!string.IsNullOrEmpty(customer.CompanyName) && customer.CompanyName != customer.DisplayName)
        {
            lines.Add(customer.CompanyName);
        }

        if (!string.IsNullOrEmpty(customer.BillingAddress))
        {
            lines.Add(customer.BillingAddress);
        }

        return string.Join("\n", lines);
    }
}

// Response DTOs
public record InvoiceHistoryResponse(
    Guid Id,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime? DueDate,
    decimal TotalAmount,
    string Status,
    string? QuickBooksInvoiceId,
    DateTime GeneratedDate,
    DateTime? PostedToQuickBooksDate,
    string? ErrorMessage,
    List<EmailDeliveryResponse> EmailDeliveries);

public record EmailDeliveryResponse(
    string RecipientEmail,
    string Status,
    DateTime SentDate,
    DateTime? DeliveredDate,
    string? ErrorMessage);

public record ResendInvoiceResponse(
    bool Success,
    string RecipientEmail,
    string? ErrorMessage);

// Extension method for entity to response conversion
public static class InvoiceHistoryExtensions
{
    public static InvoiceHistoryResponse ToResponse(this InvoiceHistory invoice)
    {
        return new InvoiceHistoryResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.TotalAmount,
            invoice.Status.ToString(),
            invoice.QuickBooksInvoiceId,
            invoice.GeneratedDate,
            invoice.PostedToQuickBooksDate,
            invoice.ErrorMessage,
            invoice.EmailDeliveries.Select(e => new EmailDeliveryResponse(
                e.RecipientEmail,
                e.Status.ToString(),
                e.SentDate,
                e.DeliveredDate,
                e.ErrorMessage
            )).ToList());
    }
}
