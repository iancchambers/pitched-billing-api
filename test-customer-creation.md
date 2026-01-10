# QuickBooks Customer Creation Testing

## Test Results ✅

**Successfully tested on 2026-01-06**

### Test 1: Customer with Company Name and Email
- ✅ Customer created: `Test Customer 1767722617`
- ✅ Customer ID: `69`
- ✅ Company Name: `Test Company Ltd`
- ✅ Email: `test@example.com`

### Test 2: Customer with Full Address
- ✅ Customer created: `Full Address Customer 1767722647`
- ✅ Customer ID: `70`
- ✅ Company Name: `Full Address Ltd`
- ✅ Email: `billing@fulladdress.com`
- ✅ Billing Address: `123 Main Street, London, Greater London SW1A 1AA`

**All customer creation features working!** 🎉

## Prerequisites

1. **Start the API**
   ```bash
   cd PitchedBillingApi
   dotnet run
   ```
   The API will run on `http://localhost:5222`

2. **Ensure QuickBooks is connected** - Check auth status first

## Test Commands

### 1. Create a Customer (Minimal - DisplayName only)

```bash
curl -X POST http://localhost:5222/api/quickbooks/customers \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "Test Customer Ltd"
  }'
```

### 2. Create a Customer (With Company Name and Email)

```bash
curl -X POST http://localhost:5222/api/quickbooks/customers \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "Acme Corporation",
    "companyName": "Acme Corp",
    "email": "billing@acme.com"
  }'
```

### 3. Create a Customer (Full Details with Address)

```bash
curl -X POST http://localhost:5222/api/quickbooks/customers \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "Example Ltd",
    "companyName": "Example Limited",
    "email": "accounts@example.com",
    "billingAddress": {
      "line1": "123 High Street",
      "city": "London",
      "state": "Greater London",
      "postalCode": "SW1A 1AA",
      "country": "UK"
    }
  }'
```

### 4. Verify Customer Was Created

After creating a customer, use the returned ID to fetch it:

```bash
curl http://localhost:5222/api/quickbooks/customers/{customer-id}
```

Or list all customers to see the new one:

```bash
curl http://localhost:5222/api/quickbooks/customers
```

## Expected Response (Success)

```json
{
  "id": "123",
  "displayName": "Example Ltd",
  "companyName": "Example Limited",
  "email": "accounts@example.com",
  "billingAddress": "123 High Street, London, Greater London SW1A 1AA"
}
```

## Expected Response (Error - Not Connected)

```json
{
  "error": "Not connected to QuickBooks. Please authorize first."
}
```

## QuickBooks API Notes

- **DisplayName is required** - This is the only mandatory field
- DisplayName must be unique within QuickBooks
- If you get a duplicate error, try a different DisplayName
- The customer is created in the QuickBooks sandbox environment

## Troubleshooting

- **400 Bad Request**: Check JSON format and ensure DisplayName is provided
- **401 Unauthorized**: QuickBooks auth may have expired - reconnect via `/api/quickbooks/auth/authorize`
- **500 Internal Server Error**: Check API logs for QuickBooks API error details
