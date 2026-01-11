using Microsoft.EntityFrameworkCore;
using PitchedBillingApi.Data;
using PitchedBillingApi.Entities;
using PitchedBillingApi.Models;

namespace PitchedBillingApi.Services;

public interface IInvoiceOrchestrationService
{
    Task<GenerateInvoiceResponse> GenerateInvoiceAsync(GenerateInvoiceRequest request, bool sendEmail = false, bool postToQuickBooks = false);
    Task<GenerateInvoiceResponse> PostInvoiceToQuickBooksAsync(Guid invoiceId);
    Task<InvoiceResponse?> UpdateDraftInvoiceItemsAsync(Guid invoiceId, UpdateInvoiceItemsRequest request);
    Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId);
    Task<List<InvoiceResponse>> GetInvoicesAsync(Guid billingPlanId);
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

    public async Task<GenerateInvoiceResponse> GenerateInvoiceAsync(GenerateInvoiceRequest request, bool sendEmail = false, bool postToQuickBooks = false)
    {
        _logger.LogInformation("Starting DRAFT invoice generation for billing plan {PlanId}", request.BillingPlanId);

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

        // 2a. Validate customer address is complete
        ValidateCustomerAddress(customer);

        // 3. Calculate totals (with dynamic VAT rates per item)
        var invoiceDate = request.InvoiceDate ?? DateTime.UtcNow.Date;
        var dueDate = invoiceDate.AddDays(14);
        var subTotal = billingPlan.Items.Sum(i => i.Quantity * i.Rate);
        var vatTotal = billingPlan.Items.Sum(i =>
        {
            var netAmount = i.Quantity * i.Rate;
            return Math.Round(netAmount * (i.VatRate / 100m), 2);
        });
        var total = subTotal + vatTotal;

        // 4. Generate invoice number and ID
        var invoiceNumber = await GenerateInvoiceNumberAsync();
        var invoiceId = Guid.NewGuid();

        // 5. Create invoice entity with line items (without PDF initially)
        var invoice = new Invoice
        {
            Id = invoiceId,
            BillingPlanId = request.BillingPlanId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            SubTotal = subTotal,
            VatAmount = vatTotal,
            TotalAmount = total,
            Status = InvoiceStatus.Draft, // Set to Draft until PDF is generated
            PdfContent = null, // Will be set after PDF generation
            GeneratedDate = DateTime.UtcNow,
            // Customer Information
            CustomerName = customer.DisplayName,
            CustomerCompanyName = customer.CompanyName,
            CustomerEmail = customer.Email,
            // Bill-To Address
            BillToLine1 = customer.BillAddr?.Line1,
            BillToCity = customer.BillAddr?.City,
            BillToCounty = customer.BillAddr?.State,
            BillToPostCode = customer.BillAddr?.PostalCode,
            BillToCountry = customer.BillAddr?.Country,
            // Reference Fields
            YourReference = request.YourReference,
            OurReference = request.OurReference,
            AccountHandler = request.AccountHandler,
            // Line Items
            Items = billingPlan.Items.Select((item, index) =>
            {
                var netAmount = item.Quantity * item.Rate;
                var vatAmount = Math.Round(netAmount * (item.VatRate / 100m), 2);
                var itemTotal = netAmount + vatAmount;

                return new InvoiceItem
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    ItemDescription = !string.IsNullOrEmpty(item.Description) ? item.Description : item.ItemName,
                    ItemCode = item.QuickBooksItemId,
                    Quantity = item.Quantity,
                    VatRate = item.VatRate,
                    NetAmount = netAmount,
                    VatAmount = vatAmount,
                    TotalAmount = itemTotal,
                    SortOrder = index
                };
            }).ToList()
        };

        _db.Invoices.Add(invoice);

        // 6. Save invoice to database FIRST (so we have a record and can reference it)
        await _db.SaveChangesAsync();
        _logger.LogInformation("Invoice {InvoiceNumber} saved to database with ID {InvoiceId} in Draft status", invoiceNumber, invoiceId);

        // 7. Generate PDF for draft invoice (using our calculated amounts)
        var reportData = new InvoiceReportData
        {
            InvoiceId = invoiceId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            YourReference = request.YourReference,
            OurReference = request.OurReference,
            AccountHandler = request.AccountHandler,
            CustomerName = BuildCustomerName(customer),
            AddressLine1 = customer.BillAddr?.Line1,
            City = customer.BillAddr?.City,
            County = customer.BillAddr?.State,
            PostCode = customer.BillAddr?.PostalCode,
            SubTotal = invoice.SubTotal,
            VatTotal = invoice.VatAmount,
            Total = invoice.TotalAmount,
            LineItems = billingPlan.Items.Select(i => new InvoiceLineItem
            {
                Description = i.ItemName,
                SubDescription = i.Description,
                Quantity = i.Quantity,
                Rate = i.Rate,
                Amount = i.Quantity * i.Rate
            }).ToList()
        };

        var pdfContent = _reportingService.GenerateInvoicePdf(reportData);
        _logger.LogInformation("Invoice PDF generated, size: {Size} bytes", pdfContent.Length);

        // 8. Update invoice with PDF content
        // Note: Status remains Draft unless posted to QuickBooks (then it's Posted or Failed)
        invoice.PdfContent = pdfContent;

        // 9. Send email if requested and customer has email address
        if (sendEmail)
        {
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
                    InvoiceId = invoice.Id,
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
                _logger.LogWarning("Customer {CustomerId} has no email address, cannot send email",
                    billingPlan.QuickBooksCustomerId);
            }
        }
        else
        {
            _logger.LogInformation("Email sending skipped (sendEmail=false)");
        }

        // 10. Save final updates (PDF content, status, email delivery)
        await _db.SaveChangesAsync();
        _logger.LogInformation("DRAFT invoice {InvoiceNumber} finalized with PDF. Use PostInvoiceToQuickBooks to post to QB.", invoiceNumber);

        return new GenerateInvoiceResponse(
            invoice.Id,
            invoiceNumber,
            invoice.TotalAmount,
            null, // Draft invoices don't have a QuickBooks ID yet
            invoice.Items.OrderBy(i => i.SortOrder).Select(i => new InvoiceItemResponse(
                i.Id,
                i.ItemDescription,
                i.Quantity,
                i.NetAmount / i.Quantity,
                i.NetAmount,
                i.VatRate,
                i.VatAmount,
                i.TotalAmount
            )).ToList());
    }

    public async Task<GenerateInvoiceResponse> PostInvoiceToQuickBooksAsync(Guid invoiceId)
    {
        _logger.LogInformation("Starting QuickBooks posting for invoice {InvoiceId}", invoiceId);

        // 1. Fetch invoice with items and billing plan
        var invoice = await _db.Invoices
            .Include(i => i.Items.OrderBy(item => item.SortOrder))
            .Include(i => i.BillingPlan)
                .ThenInclude(p => p.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(i => i.Id == invoiceId)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found");

        // 2. Validate invoice is in Draft status
        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException($"Invoice must be in Draft status to post to QuickBooks. Current status: {invoice.Status}");
        }

        // 3. Fetch customer from QuickBooks
        var customer = await _quickBooksService.GetCustomerAsync(invoice.BillingPlan.QuickBooksCustomerId)
            ?? throw new InvalidOperationException($"Customer {invoice.BillingPlan.QuickBooksCustomerId} not found in QuickBooks");

        // 4. Post to QuickBooks using invoice items (not billing plan items)
        // This ensures any edits to draft descriptions are sent to QuickBooks
        string? quickBooksInvoiceId = null;
        try
        {
            // Match invoice items to billing plan items by sort order to get tax codes
            var invoiceLines = invoice.Items
                .OrderBy(i => i.SortOrder)
                .Zip(invoice.BillingPlan.Items.OrderBy(i => i.SortOrder), (invoiceItem, planItem) => new
                {
                    InvoiceItem = invoiceItem,
                    PlanItem = planItem
                })
                .Select(pair => new InvoiceLine
                {
                    Amount = pair.InvoiceItem.NetAmount,  // Use net amount from invoice item
                    DetailType = "SalesItemLineDetail",
                    Description = !string.IsNullOrEmpty(pair.InvoiceItem.ItemDescription)
                        ? pair.InvoiceItem.ItemDescription
                        : null,
                    SalesItemLineDetail = new SalesItemLineDetail
                    {
                        ItemRef = new Reference { Value = pair.InvoiceItem.ItemCode },
                        Qty = pair.InvoiceItem.Quantity,
                        UnitPrice = pair.InvoiceItem.NetAmount / pair.InvoiceItem.Quantity,
                        TaxCodeRef = !string.IsNullOrEmpty(pair.PlanItem.QuickBooksTaxCodeId)
                            ? new Reference { Value = pair.PlanItem.QuickBooksTaxCodeId }
                            : null
                    }
                })
                .ToList();

            var qbInvoice = new QuickBooksInvoiceCreate
            {
                CustomerRef = new Reference { Value = invoice.BillingPlan.QuickBooksCustomerId },
                DueDate = invoice.DueDate?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd"),
                DocNumber = invoice.InvoiceNumber,
                Line = invoiceLines
            };

            var createdInvoice = await _quickBooksService.CreateInvoiceAsync(qbInvoice);
            quickBooksInvoiceId = createdInvoice.Id;
            invoice.QuickBooksInvoiceId = quickBooksInvoiceId;
            invoice.PostedToQuickBooksDate = DateTime.UtcNow;
            invoice.Status = InvoiceStatus.Posted;

            // Update invoice amounts from QuickBooks response (QB is source of truth)
            invoice.VatAmount = createdInvoice.TxnTaxDetail?.TotalTax ?? 0m;
            invoice.TotalAmount = createdInvoice.TotalAmt;
            invoice.SubTotal = invoice.TotalAmount - invoice.VatAmount;

            // Update line items with QB's calculated amounts
            if (createdInvoice.Line != null && createdInvoice.Line.Any())
            {
                foreach (var qbLine in createdInvoice.Line)
                {
                    // Skip non-sales lines (like subtotal lines)
                    if (qbLine.DetailType != "SalesItemLineDetail" || qbLine.SalesItemLineDetail?.ItemRef?.Value == null)
                        continue;

                    // Match QB line to our invoice item by ItemCode (QuickBooks Item ID)
                    var matchingItem = invoice.Items.FirstOrDefault(i => i.ItemCode == qbLine.SalesItemLineDetail.ItemRef.Value);
                    if (matchingItem != null)
                    {
                        var lineTotal = qbLine.Amount;
                        var lineQty = qbLine.SalesItemLineDetail.Qty;
                        var lineUnitPrice = qbLine.SalesItemLineDetail.UnitPrice;
                        var lineNet = lineQty * lineUnitPrice;
                        var lineVat = lineTotal - lineNet;

                        matchingItem.NetAmount = lineNet;
                        matchingItem.VatAmount = lineVat;
                        matchingItem.TotalAmount = lineTotal;

                        _logger.LogDebug("Updated line item {ItemCode}: Net={Net}, VAT={Vat}, Total={Total}",
                            matchingItem.ItemCode, lineNet, lineVat, lineTotal);
                    }
                }
            }

            _logger.LogInformation("Invoice posted to QuickBooks with ID {QBInvoiceId}. Total: {Total}, VAT: {Vat}, SubTotal: {SubTotal}",
                quickBooksInvoiceId, invoice.TotalAmount, invoice.VatAmount, invoice.SubTotal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post invoice to QuickBooks");
            invoice.ErrorMessage = $"Failed to post to QuickBooks: {ex.Message}";
            invoice.Status = InvoiceStatus.Failed;
            await _db.SaveChangesAsync();
            throw;
        }

        // 5. Save QuickBooks updates to database
        await _db.SaveChangesAsync();
        _logger.LogInformation("Invoice {InvoiceNumber} updated with QuickBooks amounts", invoice.InvoiceNumber);

        // 6. Regenerate PDF with accurate QB amounts
        var reportData = new InvoiceReportData
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate ?? DateTime.UtcNow.AddDays(14),
            YourReference = invoice.YourReference,
            OurReference = invoice.OurReference,
            AccountHandler = invoice.AccountHandler,
            CustomerName = BuildCustomerName(customer),
            AddressLine1 = customer.BillAddr?.Line1,
            City = customer.BillAddr?.City,
            County = customer.BillAddr?.State,
            PostCode = customer.BillAddr?.PostalCode,
            SubTotal = invoice.SubTotal,
            VatTotal = invoice.VatAmount,
            Total = invoice.TotalAmount,
            LineItems = invoice.BillingPlan.Items.Select(i => new InvoiceLineItem
            {
                Description = i.ItemName,
                SubDescription = i.Description,
                Quantity = i.Quantity,
                Rate = i.Rate,
                Amount = i.Quantity * i.Rate
            }).ToList()
        };

        var pdfContent = _reportingService.GenerateInvoicePdf(reportData);
        _logger.LogInformation("Invoice PDF regenerated, size: {Size} bytes", pdfContent.Length);

        // 7. Update invoice with PDF and final status
        invoice.PdfContent = pdfContent;
        invoice.Status = InvoiceStatus.Generated;

        // 8. Save final updates
        await _db.SaveChangesAsync();
        _logger.LogInformation("Invoice {InvoiceNumber} finalized and saved to database", invoice.InvoiceNumber);

        return new GenerateInvoiceResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.TotalAmount,
            quickBooksInvoiceId,
            invoice.Items.OrderBy(i => i.SortOrder).Select(i => new InvoiceItemResponse(
                i.Id,
                i.ItemDescription,
                i.Quantity,
                i.NetAmount / i.Quantity,
                i.NetAmount,
                i.VatRate,
                i.VatAmount,
                i.TotalAmount
            )).ToList());
    }

    public async Task<InvoiceResponse?> UpdateDraftInvoiceItemsAsync(Guid invoiceId, UpdateInvoiceItemsRequest request)
    {
        _logger.LogInformation("Updating draft invoice items for invoice {InvoiceId}", invoiceId);

        // 1. Fetch invoice with items and billing plan
        var invoice = await _db.Invoices
            .Include(i => i.Items.OrderBy(item => item.SortOrder))
            .Include(i => i.BillingPlan)
                .ThenInclude(p => p.Items.OrderBy(i => i.SortOrder))
            .Include(i => i.EmailDeliveries)
            .FirstOrDefaultAsync(i => i.Id == invoiceId)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found");

        // 2. Validate invoice is in Draft status
        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException($"Only draft invoices can be edited. Current status: {invoice.Status}");
        }

        // 3. Update item descriptions
        foreach (var updateRequest in request.Items)
        {
            var item = invoice.Items.FirstOrDefault(i => i.Id == updateRequest.ItemId);
            if (item == null)
            {
                _logger.LogWarning("Invoice item {ItemId} not found in invoice {InvoiceId}", updateRequest.ItemId, invoiceId);
                continue;
            }

            item.ItemDescription = updateRequest.Description;
            _logger.LogInformation("Updated item {ItemId} description to: {Description}", updateRequest.ItemId, updateRequest.Description);
        }

        // 4. Save updates
        await _db.SaveChangesAsync();
        _logger.LogInformation("Saved invoice item updates for invoice {InvoiceId}", invoiceId);

        // 5. Fetch customer from QuickBooks for PDF regeneration
        var customer = await _quickBooksService.GetCustomerAsync(invoice.BillingPlan.QuickBooksCustomerId)
            ?? throw new InvalidOperationException($"Customer {invoice.BillingPlan.QuickBooksCustomerId} not found in QuickBooks");

        // 6. Regenerate PDF with updated descriptions
        var reportData = new InvoiceReportData
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate ?? DateTime.UtcNow.AddDays(14),
            YourReference = invoice.YourReference,
            OurReference = invoice.OurReference,
            AccountHandler = invoice.AccountHandler,
            CustomerName = BuildCustomerName(customer),
            AddressLine1 = customer.BillAddr?.Line1,
            City = customer.BillAddr?.City,
            County = customer.BillAddr?.State,
            PostCode = customer.BillAddr?.PostalCode,
            SubTotal = invoice.SubTotal,
            VatTotal = invoice.VatAmount,
            Total = invoice.TotalAmount,
            LineItems = invoice.Items.OrderBy(i => i.SortOrder).Select(i => new InvoiceLineItem
            {
                Description = i.ItemDescription,
                SubDescription = null,
                Quantity = i.Quantity,
                Rate = i.NetAmount / i.Quantity, // Calculate rate from net amount
                Amount = i.NetAmount
            }).ToList()
        };

        var pdfContent = _reportingService.GenerateInvoicePdf(reportData);
        _logger.LogInformation("Invoice PDF regenerated with updated descriptions, size: {Size} bytes", pdfContent.Length);

        // 7. Update invoice with new PDF
        invoice.PdfContent = pdfContent;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Invoice {InvoiceNumber} PDF updated", invoice.InvoiceNumber);

        return invoice.ToResponse();
    }

    public async Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId)
    {
        var invoice = await _db.Invoices
            .Include(i => i.EmailDeliveries)
            .Include(i => i.BillingPlan)
            .Include(i => i.Items.OrderBy(item => item.SortOrder))
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        return invoice?.ToResponse();
    }

    public async Task<List<InvoiceResponse>> GetInvoicesAsync(Guid billingPlanId)
    {
        var invoices = await _db.Invoices
            .Include(i => i.EmailDeliveries)
            .Include(i => i.Items.OrderBy(item => item.SortOrder))
            .Where(i => i.BillingPlanId == billingPlanId)
            .OrderByDescending(i => i.GeneratedDate)
            .ToListAsync();

        return invoices.Select(i => i.ToResponse()).ToList();
    }

    public async Task<byte[]?> GetInvoicePdfAsync(Guid invoiceId)
    {
        var invoice = await _db.Invoices.FindAsync(invoiceId);
        return invoice?.PdfContent;
    }

    public async Task<ResendInvoiceResponse> ResendInvoiceAsync(Guid invoiceId, string? recipientEmail = null)
    {
        var invoice = await _db.Invoices
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
            InvoiceId = invoiceId,
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
        // Format: HXXXXXX where XXXXXX is sequential
        // Changed from C to H prefix for new invoices
        var prefix = "H";

        var lastInvoice = await _db.Invoices
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

        return $"{prefix}{nextNumber:D6}";
    }

    private static string BuildCustomerName(QuickBooksCustomerDto customer)
    {
        // If company name is different from display name, show both
        if (!string.IsNullOrEmpty(customer.CompanyName) && customer.CompanyName != customer.DisplayName)
        {
            return $"{customer.DisplayName}\n{customer.CompanyName}";
        }

        return customer.DisplayName;
    }

    private static void ValidateCustomerAddress(QuickBooksCustomerDto customer)
    {
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(customer.BillAddr?.Line1))
            missingFields.Add("Address Line 1");

        if (string.IsNullOrWhiteSpace(customer.BillAddr?.City))
            missingFields.Add("City");

        if (string.IsNullOrWhiteSpace(customer.BillAddr?.State))
            missingFields.Add("State/County");

        if (string.IsNullOrWhiteSpace(customer.BillAddr?.PostalCode))
            missingFields.Add("Postal Code");

        if (missingFields.Any())
        {
            var fieldList = string.Join(", ", missingFields);
            throw new InvalidOperationException(
                $"Customer address incomplete in QuickBooks. Please update the customer's billing address in QuickBooks before generating invoices. Missing fields: {fieldList}");
        }
    }
}

// Response DTOs
public record InvoiceResponse(
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
    List<EmailDeliveryResponse> EmailDeliveries,
    List<InvoiceItemResponse> Items);

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
public static class InvoiceExtensions
{
    public static InvoiceResponse ToResponse(this Invoice invoice)
    {
        return new InvoiceResponse(
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
            )).ToList(),
            invoice.Items.OrderBy(i => i.SortOrder).Select(i => new InvoiceItemResponse(
                i.Id,
                i.ItemDescription,
                i.Quantity,
                i.NetAmount / i.Quantity, // Calculate unit rate
                i.NetAmount,
                i.VatRate,
                i.VatAmount,
                i.TotalAmount
            )).ToList());
    }
}
