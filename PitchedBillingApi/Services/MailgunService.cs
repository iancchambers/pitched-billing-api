using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PitchedBillingApi.Services;

public interface IMailgunService
{
    Task<MailgunSendResult> SendInvoiceEmailAsync(
        string recipientEmail,
        string recipientName,
        string invoiceNumber,
        decimal totalAmount,
        byte[] pdfContent,
        string? customMessage = null);
}

public class MailgunService : IMailgunService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MailgunService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly string _domain;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public MailgunService(
        HttpClient httpClient,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<MailgunService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _environment = environment;

        var apiKey = configuration["mailgun-api-key"]
            ?? throw new InvalidOperationException("mailgun-api-key not configured");
        _domain = configuration["mailgun-domain"]
            ?? throw new InvalidOperationException("mailgun-domain not configured");
        _fromEmail = configuration["mailgun-from-email"] ?? $"invoices@{_domain}";
        _fromName = configuration["mailgun-from-name"] ?? "Pitched Billing";

        // Configure Basic Auth with API key
        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{apiKey}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        _httpClient.BaseAddress = new Uri("https://api.eu.mailgun.net/v3/");
    }

    public async Task<MailgunSendResult> SendInvoiceEmailAsync(
        string recipientEmail,
        string recipientName,
        string invoiceNumber,
        decimal totalAmount,
        byte[] pdfContent,
        string? customMessage = null)
    {
        _logger.LogInformation("Sending invoice {InvoiceNumber} to {Email}", invoiceNumber, recipientEmail);

        try
        {
            using var content = new MultipartFormDataContent();

            // Required fields
            content.Add(new StringContent($"{_fromName} <{_fromEmail}>"), "from");
            content.Add(new StringContent(recipientEmail), "to");
            content.Add(new StringContent($"Invoice {invoiceNumber} from Pitched"), "subject");

            // Email body from template
            var htmlBody = await LoadAndProcessTemplateAsync(recipientName, invoiceNumber, totalAmount, customMessage);
            content.Add(new StringContent(htmlBody), "html");

            // PDF attachment
            var pdfContentData = new ByteArrayContent(pdfContent);
            pdfContentData.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(pdfContentData, "attachment", $"Invoice-{invoiceNumber}.pdf");

            var response = await _httpClient.PostAsync($"{_domain}/messages", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<MailgunResponse>(responseBody);
                _logger.LogInformation("Invoice email sent successfully. MessageId: {MessageId}", result?.Id);

                return new MailgunSendResult
                {
                    Success = true,
                    MessageId = result?.Id ?? string.Empty
                };
            }
            else
            {
                _logger.LogError("Failed to send invoice email. Status: {Status}, Response: {Response}",
                    response.StatusCode, responseBody);

                return new MailgunSendResult
                {
                    Success = false,
                    ErrorMessage = $"Mailgun API returned {response.StatusCode}: {responseBody}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending invoice email to {Email}", recipientEmail);

            return new MailgunSendResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<string> LoadAndProcessTemplateAsync(
        string recipientName,
        string invoiceNumber,
        decimal totalAmount,
        string? customMessage)
    {
        var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "InvoiceEmail.html");

        if (!File.Exists(templatePath))
        {
            _logger.LogWarning("Email template not found at {Path}, using fallback", templatePath);
            return GenerateFallbackHtml(recipientName, invoiceNumber, totalAmount, customMessage);
        }

        var template = await File.ReadAllTextAsync(templatePath);

        // Replace placeholders
        var message = customMessage ?? "Please find attached your invoice.";

        return template
            .Replace("|*INVOICE_NUMBER*|", invoiceNumber)
            .Replace("|*RECIPIENT_NAME*|", recipientName)
            .Replace("|*MESSAGE*|", message)
            .Replace("|*TOTAL_AMOUNT*|", $"£{totalAmount:N2}");
    }

    private static string GenerateFallbackHtml(
        string recipientName,
        string invoiceNumber,
        decimal totalAmount,
        string? customMessage)
    {
        var message = customMessage ?? "Please find attached your invoice.";

        return $@"<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ color: #2c5aa0; margin-bottom: 20px; }}
        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <h2 class=""header"">Invoice {invoiceNumber}</h2>
        <p>Dear {recipientName},</p>
        <p>{message}</p>
        <p><strong>Amount Due:</strong> £{totalAmount:N2}</p>
        <p>If you have any questions regarding this invoice, please don't hesitate to contact us.</p>
        <div class=""footer"">
            <p><strong>Pitched</strong><br>
            Email: contact@pitched.co.uk<br>
            Phone: 01726 418 118<br>
            Web: www.pitched.co.uk</p>
        </div>
    </div>
</body>
</html>";
    }
}

public class MailgunSendResult
{
    public bool Success { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

internal class MailgunResponse
{
    public string? Id { get; set; }
    public string? Message { get; set; }
}
