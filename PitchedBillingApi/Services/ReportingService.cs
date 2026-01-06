using Telerik.Reporting;
using Telerik.Reporting.Processing;
using Telerik.Reporting.XmlSerialization;
using PitchedBillingApi.Models;

namespace PitchedBillingApi.Services;

public interface IReportingService
{
    byte[] GenerateInvoicePdf(InvoiceReportData data);
}

public class ReportingService : IReportingService
{
    private readonly ILogger<ReportingService> _logger;
    private readonly IWebHostEnvironment _environment;

    public ReportingService(ILogger<ReportingService> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public byte[] GenerateInvoicePdf(InvoiceReportData data)
    {
        _logger.LogInformation("Generating invoice PDF for invoice {InvoiceNumber}", data.InvoiceNumber);

        // Load TRDX template
        var reportPath = Path.Combine(_environment.ContentRootPath, "Reports", "PitchedInvoice.trdx");

        if (!File.Exists(reportPath))
        {
            throw new FileNotFoundException($"Report template not found: {reportPath}");
        }

        Telerik.Reporting.Report report;
        using (var fs = new FileStream(reportPath, FileMode.Open, FileAccess.Read))
        {
            var xmlSerializer = new ReportXmlSerializer();
            report = (Telerik.Reporting.Report)xmlSerializer.Deserialize(fs);
        }

        // Set data source
        report.DataSource = new[] { data };

        // Render to PDF
        var reportProcessor = new ReportProcessor();
        var reportSource = new InstanceReportSource { ReportDocument = report };
        var renderingResult = reportProcessor.RenderReport("PDF", reportSource, null);

        if (renderingResult.Errors.Length > 0)
        {
            foreach (var error in renderingResult.Errors)
            {
                _logger.LogError("Report rendering error: {Error}", error);
            }
            throw new InvalidOperationException("Failed to render invoice PDF");
        }

        _logger.LogInformation("Invoice PDF generated successfully, size: {Size} bytes", renderingResult.DocumentBytes.Length);

        return renderingResult.DocumentBytes;
    }
}
