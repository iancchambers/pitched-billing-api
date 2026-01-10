# Invoice Generation Testing with curl

## Test Results ✅

**Successfully tested on 2026-01-06**

### Latest Test (INV-202601-0003) - ALL FEATURES WORKING! 🎉

- ✅ Invoice generated: `INV-202601-0003`
- ✅ Invoice ID: `8f484b7e-03d9-4b9a-941c-0c3b9c000450`
- ✅ Total Amount: £118.80 (£99.00 + 20% VAT)
- ✅ PDF generated successfully (mock PDF for now)
- ✅ Email sent to: ianchambers@pitched.co.uk
- ✅ **QuickBooks posted successfully!** Invoice ID: `185`
- ✅ Status: Posted
- ✅ Posted to QuickBooks: 2026-01-06T17:31:39

### Fix Applied
Added `minorversion=65` parameter and Accept header to QuickBooks API requests.

## Prerequisites

1. **Start the API**
   ```bash
   cd PitchedBillingApi
   dotnet run
   ```
   The API will run on `http://localhost:5222`

2. **Ensure you have:**
   - A valid billing plan ID in the database ✅
   - QuickBooks configured with valid credentials ✅
   - Mailgun configured for email delivery ✅
   - Database connection string set up ✅

## Test Commands

### 1. Generate an Invoice (Minimal Request)

```bash
curl -X POST http://localhost:5222/api/invoice/generate \
  -H "Content-Type: application/json" \
  -d '{
    "billingPlanId": "00000000-0000-0000-0000-000000000000"
  }'
```

**Replace** `00000000-0000-0000-0000-000000000000` with an actual billing plan GUID from your database.

### 2. Generate an Invoice (Full Request)

```bash
curl -X POST http://localhost:5222/api/invoice/generate \
  -H "Content-Type: application/json" \
  -d '{
    "billingPlanId": "00000000-0000-0000-0000-000000000000",
    "invoiceDate": "2026-01-06T00:00:00Z",
    "yourReference": "PO-12345",
    "ourReference": "PROJECT-ABC",
    "accountHandler": "John Smith"
  }'
```

### 3. Get Invoice by ID

```bash
curl http://localhost:5222/api/invoice/{invoice-id}
```

### 4. Get Invoice History for a Billing Plan

```bash
curl http://localhost:5222/api/invoice/plan/{billing-plan-id}/history
```

### 5. Download Invoice PDF

```bash
curl http://localhost:5222/api/invoice/{invoice-id}/pdf \
  --output invoice.pdf
```

### 6. Resend Invoice Email

```bash
curl -X POST http://localhost:5222/api/invoice/{invoice-id}/resend \
  -H "Content-Type: application/json" \
  -d '{
    "recipientEmail": "alternative@example.com"
  }'
```

## Expected Response (Success)

```json
{
  "invoiceHistoryId": "123e4567-e89b-12d3-a456-426614174000",
  "invoiceNumber": "INV-202601-0001",
  "totalAmount": 1200.00,
  "quickBooksInvoiceId": "123"
}
```

## Expected Response (Error)

```json
{
  "error": "Billing plan not found"
}
```

or

```json
{
  "error": "Billing plan is not active"
}
```

## Testing Workflow

1. **First, get a list of billing plans** (you'll need to query your database or add an endpoint)

2. **Generate an invoice** using a valid billing plan ID

3. **Verify the invoice** by:
   - Checking the response for `invoiceHistoryId` and `invoiceNumber`
   - Downloading the PDF to verify it was generated correctly
   - Checking QuickBooks to see if the invoice was posted
   - Checking your email for the invoice notification

4. **Test resend** functionality if needed

## Troubleshooting

- **404 Not Found**: Check the API is running and the URL is correct
- **400 Bad Request**: Check your JSON format and ensure billing plan ID is a valid GUID
- **500 Internal Server Error**: Check API logs for details (QuickBooks auth, database connection, etc.)

## Notes

- The invoice generation process includes:
  - Fetching billing plan and customer data
  - Calculating totals with 20% VAT
  - Generating a PDF using Telerik Reporting
  - Posting to QuickBooks
  - Sending email via Mailgun

- If QuickBooks posting fails, the invoice will still be saved locally but `quickBooksInvoiceId` will be null

- Invoice numbers follow the format: `INV-YYYYMM-XXXX` (e.g., INV-202601-0001)
