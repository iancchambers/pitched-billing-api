# Telerik Report Parameter Setup

## Overview

The invoice PDF generation now passes the `InvoiceHistoryId` as a parameter to your Telerik report, allowing you to design the report in Telerik Report Designer and fetch data directly from the database.

## Parameter Passed to Report

The report receives one parameter:

- **Name**: `InvoiceId`
- **Type**: `System.Guid`
- **Value**: The GUID of the invoice record in the `InvoiceHistories` table (InvoiceHistory.Id)

## How to Use in Telerik Report Designer

### 1. Define the Report Parameter

In your TRDX file, add a report parameter:

```xml
<ReportParameters>
  <ReportParameter Name="InvoiceId" Text="Invoice ID" Type="System.Guid" Visible="False" />
</ReportParameters>
```

### 2. Create a SQL Data Source

Add a SQL data source that uses the parameter to fetch invoice data:

**Connection String**: Use the same connection string from appsettings
```
data source=pitcheddev.database.windows.net;initial catalog=pitchedbilling;persist security info=True;user id=pitched_dev;password=L0veC0de!2018;multipleactiveresultsets=True;application name=EntityFramework;Encrypt=False;TrustServerCertificate=True
```

**SQL Query** (with line items):
```sql
SELECT
    ih.Id,
    ih.InvoiceNumber,
    ih.InvoiceDate,
    ih.DueDate,
    ih.TotalAmount,
    bp.PlanName,
    bp.QuickBooksCustomerId,
    bpi.ItemName,
    bpi.Description,
    bpi.Quantity,
    bpi.Rate,
    (bpi.Quantity * bpi.Rate) AS LineTotal,
    bpi.SortOrder
FROM InvoiceHistories ih
INNER JOIN BillingPlans bp ON ih.BillingPlanId = bp.Id
INNER JOIN BillingPlanItems bpi ON bp.Id = bpi.BillingPlanId
WHERE ih.Id = @InvoiceId
ORDER BY bpi.SortOrder
```

**Note**: InvoiceHistory does NOT store individual line items. Line items are retrieved from the BillingPlan that was used to generate the invoice.

**Parameter Mapping**:
- SQL Parameter: `@InvoiceId`
- Report Parameter: `=Parameters.InvoiceId`

### 3. Bind Report Elements to Data

Once you have the data source configured, you can bind TextBoxes and other elements to fields:

- `=Fields.InvoiceNumber`
- `=Fields.InvoiceDate`
- `=Fields.TotalAmount`
- etc.

## Database Schema Reference

### InvoiceHistories Table
- `Id` (Guid) - Primary key
- `BillingPlanId` (Guid) - Foreign key to BillingPlans
- `InvoiceNumber` (string) - Format: INV-YYYYMM-XXXX
- `InvoiceDate` (DateTime)
- `DueDate` (DateTime)
- `TotalAmount` (decimal)
- `Status` (int) - InvoiceStatus enum
- `QuickBooksInvoiceId` (string, nullable)
- `PdfContent` (byte[])
- `ErrorMessage` (string, nullable)
- `GeneratedDate` (DateTime)
- `PostedToQuickBooksDate` (DateTime, nullable)

### BillingPlans Table
- `Id` (Guid)
- `PlanName` (string)
- `QuickBooksCustomerId` (string)
- `Frequency` (int) - BillingFrequency enum
- `StartDate` (DateTime)
- `EndDate` (DateTime, nullable)
- `IsActive` (bool)
- `CreatedDate` (DateTime)
- `ModifiedDate` (DateTime, nullable)

### BillingPlanItems Table
- `Id` (Guid)
- `BillingPlanId` (Guid) - Foreign key
- `QuickBooksItemId` (string)
- `ItemName` (string)
- `Quantity` (decimal)
- `Rate` (decimal)
- `Description` (string, nullable)
- `SortOrder` (int)

## Testing

Once you've designed your report with proper data sources:

1. Save the TRDX file to `PitchedBillingApi/Reports/PitchedInvoice.trdx`
2. Restart the API if needed
3. Generate an invoice:

```bash
curl -X POST http://localhost:5222/api/invoice/generate \
  -H "Content-Type: application/json" \
  -d '{"billingPlanId": "551b0cc7-1556-46a6-983d-905674c71900"}'
```

4. Download the PDF to verify:

```bash
curl http://localhost:5222/api/invoice/{invoice-id}/pdf --output invoice.pdf
```

## Notes

- The report parameter is set automatically by the API - you don't need to pass it in the request
- Make sure your SQL data source query is efficient
- You can add additional SQL data sources for line items, customer details, etc.
- Use the Report Designer's preview feature to test with a sample GUID
